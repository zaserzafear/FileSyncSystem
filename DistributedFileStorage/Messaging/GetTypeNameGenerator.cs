namespace DistributedFileStorage.Messaging;

public class GetTypeNameGenerator
{
    public static string GetTypeName<T>()
    {
        var type = typeof(T);
        var typeName = type.ToString() ?? typeof(T).Name;

        return typeName;
    }
}
