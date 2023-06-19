using System.Collections.Concurrent;
using System.Reflection;
using Google.Protobuf;

namespace Dgraph4Net.ActiveRecords;

internal interface IClassMapping
{
    ConcurrentDictionary<Type, IClassMap> ClassMappings { get; }

    ConcurrentBag<Migration> Migrations { get; set; }

    string ToJsonString<T>(T entity, bool deep = false, bool doNotPropagateNulls = false) where T : IEntity;

    ByteString ToJson<T>(T entity, bool deep = false, bool doNotPropagateNulls = false) where T : IEntity;

    string ToJson<T>(T entity, HashSet<IEntity> mapped, bool deep, bool doNotPropagateNulls = false) where T : IEntity;

    object? FromJson(ByteString bytes, Type type, string param);

    T? FromJson<T>(string bytes);

    T? FromJson<T>(string bytes, string param);

    T? FromJson<T>(ByteString bytes);

    T? FromJson<T>(ByteString bytes, string param);

    object? FromJsonBS(ByteString bytes, Type type, string param);

    object? FromJson(ByteString bytes, Type type);

    object? FromJson(string str, Type type);

    object? FromJson(string str, Type type, string param);

    object? FromJson(ByteString bytes, Type type, Dictionary<Uid, object> loaded);

    bool TryMapJson(Type type, out IClassMap? classMap);

    void Map();

    void Map(params Assembly[] assemblies);
}
