namespace Dgraph4Net;

public interface IIEntityConverter
{
    public static Type? Instance { get; set; }

    T? Deserialize<T>(string json) where T : IEntity;
    string Serialize<T>(T entity, bool ignoreNulls = true, bool getOnlyNulls = false, bool convertDefaultToNull = false) where T : IEntity;
}
