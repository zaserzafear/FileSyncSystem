using System.Text.Json.Serialization;

namespace DistributedFileStorage.Events;


[JsonPolymorphic(TypeDiscriminatorPropertyName = "event")]
[JsonDerivedType(typeof(FileUploadedEvent), "FileUploaded")]
[JsonDerivedType(typeof(FileDeletedEvent), "FileDeleted")]
public abstract record FileEvent;
