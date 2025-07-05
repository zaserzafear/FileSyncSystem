using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;

namespace DistributedFileStorage.Messaging;

public class RabbitMqMessageBus : IMessageBus, IAsyncDisposable
{
    private readonly ILogger<RabbitMqMessageBus> _logger;
    private readonly IConnection _connection;
    private IChannel _replyChannel = default!;
    private string _replyQueueName = default!;
    private readonly ConcurrentDictionary<string, TaskCompletionSource<string>> _responseHandlers = new();
    private static readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);

    private RabbitMqMessageBus(
        ILogger<RabbitMqMessageBus> logger,
        IConnection connection)
    {
        _logger = logger;
        _connection = connection;
    }

    public static async Task<RabbitMqMessageBus> CreateAsync(
        IConnection connection,
        ILogger<RabbitMqMessageBus> logger)
    {
        var bus = new RabbitMqMessageBus(logger, connection);
        await bus.InitAsync();
        return bus;
    }

    private async Task InitAsync()
    {
        _replyChannel = await _connection.CreateChannelAsync();
        var queue = await _replyChannel.QueueDeclareAsync();
        _replyQueueName = queue.QueueName;

        var rpcConsumer = new AsyncEventingBasicConsumer(_replyChannel);
        rpcConsumer.ReceivedAsync += async (model, ea) =>
        {
            var correlationId = ea.BasicProperties?.CorrelationId;
            if (string.IsNullOrEmpty(correlationId))
            {
                _logger.LogWarning("Received message without CorrelationId.");
                return;
            }

            if (_responseHandlers.TryRemove(correlationId, out var tcs))
            {
                var response = Encoding.UTF8.GetString(ea.Body.ToArray());
                tcs.TrySetResult(response);
            }
            else
            {
                _logger.LogWarning("No handler found for CorrelationId: {CorrelationId}", correlationId);
            }

            await ((AsyncEventingBasicConsumer)model).Channel.BasicAckAsync(ea.DeliveryTag, false);
        };

        await _replyChannel.BasicConsumeAsync(_replyQueueName, false, rpcConsumer);
    }

    public async ValueTask DisposeAsync()
    {
        _logger.LogInformation("Disposing RabbitMQ channels.");

        if (_replyChannel != null)
        {
            try
            {
                await _replyChannel.CloseAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error closing reply channel.");
            }

            _replyChannel.Dispose();
        }
    }

    private async Task DeclareExchangeAndQueueAsync(IChannel channel, string exchangeName, string exchangeType, string queueName)
    {
        await channel.ExchangeDeclareAsync(exchange: exchangeName, type: exchangeType, durable: true);
        await channel.QueueDeclareAsync(queue: queueName, durable: true, exclusive: false, autoDelete: false);
        await channel.QueueBindAsync(queue: queueName, exchange: exchangeName, routingKey: queueName);
    }

    public async Task PublishAsync<TRequest>(TRequest message, string exchangeName, string exchangeType, string queueName) where TRequest : class
    {
        try
        {
            var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(message, _jsonOptions));
            _logger.LogDebug("Publishing message to exchange {Exchange}, queue {Queue}", exchangeName, queueName);

            using var channel = await _connection.CreateChannelAsync();
            await DeclareExchangeAndQueueAsync(channel, exchangeName, exchangeType, queueName);

            var properties = new BasicProperties
            {
                Persistent = true
            };

            await channel.BasicPublishAsync(
                exchange: exchangeName,
                routingKey: queueName,
                mandatory: true,
                basicProperties: properties,
                body: body);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish message to queue: {Queue}", queueName);
            throw;
        }
    }

    public async Task<TResponse> PublishAsync<TRequest, TResponse>(
    TRequest message,
    string exchangeName,
    string exchangeType,
    string queueName) where TRequest : class
    {
        var correlationId = Guid.NewGuid().ToString();
        var tcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!_responseHandlers.TryAdd(correlationId, tcs))
        {
            throw new InvalidOperationException($"CorrelationId collision: {correlationId}");
        }

        try
        {
            var json = JsonSerializer.Serialize(message, _jsonOptions);
            var body = Encoding.UTF8.GetBytes(json);

            using var channel = await _connection.CreateChannelAsync();

            // Declare exchange and queue, and bind
            await DeclareExchangeAndQueueAsync(channel, exchangeName, exchangeType, queueName);

            var props = new BasicProperties
            {
                ReplyTo = _replyQueueName,
                CorrelationId = correlationId,
                Persistent = true
            };

            _logger.LogDebug("Publishing RPC message to exchange {Exchange}, queue {Queue} with CorrelationId {CorrelationId}", exchangeName, queueName, correlationId);

            await channel.BasicPublishAsync(
                exchange: exchangeName,
                routingKey: queueName,
                mandatory: true,
                basicProperties: props,
                body: body);

            var responseJson = await tcs.Task;
            return JsonSerializer.Deserialize<TResponse>(responseJson, _jsonOptions)!;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish RPC message to exchange {Exchange}, queue {Queue}", exchangeName, queueName);
            throw;
        }
        finally
        {
            _responseHandlers.TryRemove(correlationId, out _);
        }
    }

    public async Task SubscribeAsync<TRequest>(
    Func<TRequest, Task> handler,
    string exchangeName,
    string exchangeType,
    string queueName,
    ushort prefetchCount) where TRequest : class
    {
        var messageType = queueName ?? $"{GetTypeNameGenerator.GetTypeName<TRequest>()}.fireforget";
        _logger.LogInformation("Subscribing to exchange {Exchange} and queue {Queue} for message type {MessageType}", exchangeName, queueName, messageType);

        var channel = await _connection.CreateChannelAsync();

        await channel.BasicQosAsync(0, prefetchCount, false);
        await DeclareExchangeAndQueueAsync(channel, exchangeName, exchangeType, messageType);

        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.ReceivedAsync += async (model, ea) =>
        {
            try
            {
                var json = Encoding.UTF8.GetString(ea.Body.ToArray());
                var message = JsonSerializer.Deserialize<TRequest>(json, _jsonOptions);

                if (message == null)
                {
                    _logger.LogWarning("Received null or invalid message on queue: {Queue}", messageType);
                    return;
                }

                _ = Task.Run(() => handler(message));
                await ((AsyncEventingBasicConsumer)model).Channel.BasicAckAsync(deliveryTag: ea.DeliveryTag, multiple: false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling message from queue: {Queue}", messageType);
            }
        };

        await channel.BasicConsumeAsync(queue: messageType, autoAck: false, consumer: consumer);
    }

    public async Task SubscribeAsync<TRequest, TResponse>(
    Func<TRequest, Task<TResponse>> handler,
    string exchangeName,
    string exchangeType,
    string queueName,
    ushort prefetchCount) where TRequest : class
    {
        var messageType = queueName ?? $"{GetTypeNameGenerator.GetTypeName<TRequest>()}.rpc";
        _logger.LogInformation("Subscribing (RPC) to exchange {Exchange} and queue {Queue} for message type {MessageType}", exchangeName, queueName, messageType);

        var channel = await _connection.CreateChannelAsync();

        await channel.BasicQosAsync(0, prefetchCount, false);
        await DeclareExchangeAndQueueAsync(channel, exchangeName, exchangeType, messageType);

        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.ReceivedAsync += async (model, ea) =>
        {
            var props = ea.BasicProperties;
            var replyProps = new BasicProperties
            {
                CorrelationId = props?.CorrelationId
            };

            try
            {
                var json = Encoding.UTF8.GetString(ea.Body.ToArray());
                var request = JsonSerializer.Deserialize<TRequest>(json, _jsonOptions);

                if (request == null)
                {
                    _logger.LogWarning("RPC request deserialization failed: {MessageType}", messageType);
                    return;
                }

                _ = Task.Run(async () =>
                {
                    try
                    {
                        var response = await handler(request);
                        var responseBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(response, _jsonOptions));

                        await channel.BasicPublishAsync(
                            exchange: exchangeName,
                            routingKey: props?.ReplyTo ?? string.Empty,
                            mandatory: true,
                            basicProperties: replyProps,
                            body: responseBytes);

                        await channel.BasicAckAsync(ea.DeliveryTag, false);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error while handling RPC request on queue: {MessageType}", messageType);
                    }
                });

                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while handling RPC request on queue: {MessageType}", messageType);
                await Task.CompletedTask;
            }
        };

        await channel.BasicConsumeAsync(queue: messageType, autoAck: false, consumer: consumer);
    }
}
