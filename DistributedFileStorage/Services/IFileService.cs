using DistributedFileStorage.Events;
using DistributedFileStorage.Models;

namespace DistributedFileStorage.Services;

public interface IFileService
{
    Task<FileMetadata> UploadFileAsync(IFormFile file, string? path, string nodeId);
    Task<FileMetadata?> GetFileMetadataAsync(string filePath, int? revision);
    Stream? GetFileStream(FileMetadata fileMeta, string storagePath);
    Task<List<FileDeletedEvent>> DeleteFileAsync(string filePath, int? revision, string nodeId);
}
