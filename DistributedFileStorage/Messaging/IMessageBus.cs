namespace DistributedFileStorage.Messaging;

public interface IMessageBus
{
    Task PublishAsync<TRequest>(TRequest message,
        string exchangeName,
        string exchangeType,
        string queueName) where TRequest : class; // Fire and forget
    Task<TResponse> PublishAsync<TRequest, TResponse>(TRequest message,
        string exchangeName,
        string exchangeType,
        string queueName) where TRequest : class; // Wait for result
    Task SubscribeAsync<TRequest>(
        Func<TRequest, Task> handler,
        string exchangeName,
        string exchangeType,
        string queueName,
        ushort prefetchCount) where TRequest : class; // Subscription
    Task SubscribeAsync<TRequest, TResponse>(
        Func<TRequest, Task<TResponse>> handler,
        string exchangeName,
        string exchangeType,
        string queueName,
        ushort prefetchCount) where TRequest : class; // Subscription with response
}
