namespace DistributedFileStorage.Messaging;

public interface IMessageBusHandle
{
    void RegisterHandlersAsync(IMessageBus messageBus);
}
