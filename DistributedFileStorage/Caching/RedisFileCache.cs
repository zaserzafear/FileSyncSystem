using DistributedFileStorage.Models;
using StackExchange.Redis;
using System.Text.Json;

namespace DistributedFileStorage.Caching;

public class RedisFileCache : IFileCache
{
    private readonly IDatabase _redis;
    private readonly TimeSpan _cacheTTL = TimeSpan.FromMinutes(30); // TTL ตามเหมาะสม

    public RedisFileCache(IConnectionMultiplexer redis)
    {
        _redis = redis.GetDatabase();
    }

    private static string GetKey(string filePath, int? revision)
    {
        return revision.HasValue
            ? $"file:{filePath}:{revision}"
            : $"file:{filePath}:latest";
    }

    public async Task<FileMetadata?> GetMetadataAsync(string filePath, int? revision)
    {
        var key = GetKey(filePath, revision);
        var value = await _redis.StringGetAsync(key);
        if (value.IsNullOrEmpty) return null;

        return JsonSerializer.Deserialize<FileMetadata>(value);
    }

    public async Task SetMetadataAsync(FileMetadata metadata)
    {
        var latestKey = GetKey(metadata.FilePath, null);
        var specificKey = GetKey(metadata.FilePath, metadata.Revision);

        var json = JsonSerializer.Serialize(metadata);

        await _redis.StringSetAsync(specificKey, json, _cacheTTL);
        await _redis.StringSetAsync(latestKey, json, _cacheTTL);
    }

    public async Task RemoveMetadataAsync(string filePath, int? revision)
    {
        if (revision.HasValue)
        {
            var key = GetKey(filePath, revision);
            await _redis.KeyDeleteAsync(key);
        }
        else
        {
            var latestKey = GetKey(filePath, null);
            await _redis.KeyDeleteAsync(latestKey);
        }
    }
}
