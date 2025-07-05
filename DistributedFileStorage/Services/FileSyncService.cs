using DistributedFileStorage.Events;
using DistributedFileStorage.Messaging;

namespace DistributedFileStorage.Services;

public class FileSyncService : IMessageBusHandle
{
    private readonly ILogger<FileSyncService> _logger;
    private readonly string _nodeId;
    private readonly string _storagePath;

    public FileSyncService(ILogger<FileSyncService> logger, IConfiguration config)
    {
        _logger = logger;
        _nodeId = config.GetValue<string>("NodeId")!;
        _storagePath = config.GetValue<string>("StoragePath")!;
    }

    public void RegisterHandlersAsync(IMessageBus messageBus)
    {
        messageBus.SubscribeAsync<FileUploadedEvent>(
            HandleUploaded,
            exchangeName: "file-uploaded-events",
            exchangeType: "fanout",
            queueName: $"file-uploaded-events.{_nodeId}",
            prefetchCount: 5
        );

        messageBus.SubscribeAsync<FileDeletedEvent>(
            HandleDeleted,
            exchangeName: "file-deleted-events",
            exchangeType: "fanout",
            queueName: $"file-deleted-events.{_nodeId}",
            prefetchCount: 5
        );
    }

    private async Task HandleUploaded(FileUploadedEvent evt)
    {
        var originalFileName = Path.GetFileName(evt.FileName);
        var folderPath = Path.Combine(
            _storagePath,
            Path.GetDirectoryName(evt.FileName) ?? "",
            Path.GetFileNameWithoutExtension(originalFileName),
            evt.Revision.ToString()
        );

        Directory.CreateDirectory(folderPath);
        var fullPath = Path.Combine(folderPath, originalFileName);

        if (System.IO.File.Exists(fullPath))
        {
            _logger.LogInformation("Already exists: {Path}", fullPath);
            return;
        }

        try
        {
            var bytes = Convert.FromBase64String(evt.ContentBase64);
            await System.IO.File.WriteAllBytesAsync(fullPath, bytes);
            _logger.LogInformation("Saved file {Path} from {NodeId}", fullPath, evt.NodeId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save uploaded file from {NodeId}", evt.NodeId);
        }
    }

    private Task HandleDeleted(FileDeletedEvent evt)
    {
        var originalFileName = Path.GetFileName(evt.FileName);
        var folderPath = Path.Combine(
            _storagePath,
            Path.GetDirectoryName(evt.FileName) ?? "",
            Path.GetFileNameWithoutExtension(originalFileName),
            evt.Revision.ToString()
        );

        var fullPath = Path.Combine(folderPath, originalFileName);

        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);
            if (!Directory.EnumerateFileSystemEntries(folderPath).Any())
            {
                Directory.Delete(folderPath);
            }

            _logger.LogInformation("Synced delete: {Path} (r{Revision})", fullPath, evt.Revision);
        }
        else
        {
            _logger.LogWarning("File already missing: {Path}", fullPath);
        }

        return Task.CompletedTask;
    }
}
