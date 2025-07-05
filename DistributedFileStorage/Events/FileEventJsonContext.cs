using System.Text.Json.Serialization;

namespace DistributedFileStorage.Events;

[JsonSourceGenerationOptions(
    WriteIndented = false,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase
)]
[JsonSerializable(typeof(FileEvent))]
[JsonSerializable(typeof(FileUploadedEvent))]
[JsonSerializable(typeof(FileDeletedEvent))]
public partial class FileEventJsonContext : JsonSerializerContext
{
}
