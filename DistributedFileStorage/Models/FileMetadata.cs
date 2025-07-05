namespace DistributedFileStorage.Models;

public class FileMetadata
{
    public Guid Id { get; set; } = Ulid.NewUlid().ToGuid();

    public required string FilePath { get; set; }
    public int Revision { get; set; }

    public long Size { get; set; }
    public required string Checksum { get; set; }
    public string? ContentType { get; set; }
    public DateTime CreatedAt { get; set; }
}
