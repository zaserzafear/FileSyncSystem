using DistributedFileStorage.Caching;
using DistributedFileStorage.Data;
using DistributedFileStorage.Events;
using DistributedFileStorage.Messaging;
using DistributedFileStorage.Models;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;

namespace DistributedFileStorage.Services;

public class FileService : IFileService
{
    private readonly AppDbContext _db;
    private readonly IMessageBus _messageBus;
    private readonly IFileCache _fileCache;

    public FileService(AppDbContext db, IMessageBus messageBus, IFileCache fileCache)
    {
        _db = db;
        _messageBus = messageBus;
        _fileCache = fileCache;
    }

    public async Task<FileMetadata> UploadFileAsync(IFormFile file, string? path, string nodeId)
    {
        var relativePath = string.IsNullOrWhiteSpace(path)
            ? file.FileName
            : Path.Combine(path, file.FileName).Replace("\\", "/");

        var latestMetadata = await _fileCache.GetMetadataAsync(relativePath, null);

        if (latestMetadata == null)
        {
            latestMetadata = await _db.Files
                .Where(f => f.FilePath == relativePath)
                .OrderByDescending(f => f.Revision)
                .FirstOrDefaultAsync();

            if (latestMetadata != null)
                await _fileCache.SetMetadataAsync(latestMetadata);
        }

        var lastRevision = latestMetadata?.Revision ?? 0;

        var nextRevision = lastRevision + 1;
        var fileId = Ulid.NewUlid().ToGuid();

        string checksum;
        string contentBase64;

        await using (var ms = new MemoryStream())
        {
            await file.CopyToAsync(ms);
            ms.Position = 0;

            using var md5 = MD5.Create();
            checksum = BitConverter.ToString(md5.ComputeHash(ms)).Replace("-", "").ToLowerInvariant();

            ms.Position = 0;
            contentBase64 = Convert.ToBase64String(ms.ToArray());
        }

        var evt = new FileUploadedEvent(
            FileName: relativePath,
            Revision: nextRevision,
            NodeId: nodeId,
            ContentBase64: contentBase64
        );

        await _messageBus.PublishAsync(evt, "file-uploaded-events", "fanout", "");

        var fileMeta = new FileMetadata
        {
            Id = fileId,
            FilePath = relativePath,
            Revision = nextRevision,
            Size = file.Length,
            Checksum = checksum,
            ContentType = file.ContentType,
            CreatedAt = DateTime.UtcNow,
        };

        _db.Files.Add(fileMeta);
        await _db.SaveChangesAsync();

        await _fileCache.SetMetadataAsync(fileMeta);

        return fileMeta;
    }

    public async Task<FileMetadata?> GetFileMetadataAsync(string filePath, int? revision)
    {
        var cache = await _fileCache.GetMetadataAsync(filePath, revision);
        if (cache != null) return cache;

        var query = _db.Files.Where(f => f.FilePath == filePath);
        query = revision.HasValue
            ? query.Where(f => f.Revision == revision.Value)
            : query.OrderByDescending(f => f.Revision);

        var fileMeta = await query.FirstOrDefaultAsync();

        if (fileMeta != null)
            await _fileCache.SetMetadataAsync(fileMeta);

        return fileMeta;
    }

    public Stream? GetFileStream(FileMetadata fileMeta, string storagePath)
    {
        var originalFileName = Path.GetFileName(fileMeta.FilePath);
        var folderPath = Path.Combine(
            storagePath,
            Path.GetDirectoryName(fileMeta.FilePath) ?? "",
            Path.GetFileNameWithoutExtension(originalFileName),
            fileMeta.Revision.ToString()
        );

        var fullPath = Path.Combine(folderPath, originalFileName);
        if (!File.Exists(fullPath))
            return null;

        return File.OpenRead(fullPath);
    }

    public async Task<List<FileDeletedEvent>> DeleteFileAsync(string filePath, int? revision, string nodeId)
    {
        var filesQuery = _db.Files.Where(f => f.FilePath == filePath);

        if (revision.HasValue)
            filesQuery = filesQuery.Where(f => f.Revision == revision.Value);

        var files = await filesQuery.ToListAsync();
        if (!files.Any()) return new List<FileDeletedEvent>();

        var deletedEvents = files.Select(f => new FileDeletedEvent(
            FileName: f.FilePath,
            Revision: f.Revision
        )).ToList();

        _db.Files.RemoveRange(files);
        await _db.SaveChangesAsync();

        foreach (var file in files)
        {
            await _fileCache.RemoveMetadataAsync(file.FilePath, file.Revision);
            await _fileCache.RemoveMetadataAsync(file.FilePath, null);
        }

        foreach (var evt in deletedEvents)
        {
            await _messageBus.PublishAsync(evt, "file-deleted-events", "fanout", "");
        }

        return deletedEvents;
    }
}
