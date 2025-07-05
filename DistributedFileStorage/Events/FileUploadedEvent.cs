namespace DistributedFileStorage.Events;

public sealed record FileUploadedEvent(
    string FileName,
    int Revision,
    string NodeId,
    string ContentBase64
) : FileEvent;
