namespace DistributedFileStorage.Events;

public sealed record FileDeletedEvent(
    string FileName,
    int Revision
) : FileEvent;
