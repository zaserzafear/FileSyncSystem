namespace DistributedFileStorage.Messaging;

public class MessageBusHandlerRegistrar : IHostedService
{
    private readonly IMessageBus _messageBus;
    private readonly IEnumerable<IMessageBusHandle> _handlers;

    public MessageBusHandlerRegistrar(IMessageBus messageBus, IEnumerable<IMessageBusHandle> handlers)
    {
        _messageBus = messageBus;
        _handlers = handlers;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        foreach (var handler in _handlers)
        {
            handler.RegisterHandlersAsync(_messageBus);
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
