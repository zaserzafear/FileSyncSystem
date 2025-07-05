using DistributedFileStorage.Models;

namespace DistributedFileStorage.Caching;

public interface IFileCache
{
    Task<FileMetadata?> GetMetadataAsync(string filePath, int? revision);
    Task SetMetadataAsync(FileMetadata metadata);
    Task RemoveMetadataAsync(string filePath, int? revision);
}
