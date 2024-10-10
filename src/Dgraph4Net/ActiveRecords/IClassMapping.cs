using System.Collections.Concurrent;
using System.Reflection;
using Google.Protobuf;

namespace Dgraph4Net.ActiveRecords;

internal interface IClassMapping
{
    /// <summary>
    /// Gets the class mappings.
    /// </summary>
    ConcurrentDictionary<Type, IClassMap> ClassMappings { get; }

    /// <summary>
    /// Gets the migrations.
    /// </summary>
    ConcurrentBag<Migration> Migrations { get; set; }

    /// <summary>
    /// Converts the entity to a JSON string.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="entity"></param>
    /// <param name="deep"></param>
    /// <param name="doNotPropagateNulls"></param>
    /// <returns></returns>
    string ToJsonString<T>(T entity) where T : IEntity;

    /// <summary>
    /// Converts the entity to a JSON byte string.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="entity"></param>
    /// <param name="deep"></param>
    /// <param name="doNotPropagateNulls"></param>
    /// <returns></returns>
    ByteString ToJson<T>(T entity) where T : IEntity;

    /// <summary>
    /// Converts the entity to a JSON byte string.
    /// </summary>
    /// <param name="bytes"></param>
    /// <param name="type"></param>
    /// <param name="param"></param>
    /// <returns></returns>
    object? FromJson(ByteString bytes, Type type, string param);

    /// <summary>
    /// Converts the entity to a JSON byte string.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="bytes"></param>
    /// <returns></returns>
    T? FromJson<T>(string bytes);

    /// <summary>
    /// Converts the entity to a JSON byte string.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="bytes"></param>
    /// <param name="param"></param>
    /// <returns></returns>
    T? FromJson<T>(string bytes, string param);

    /// <summary>
    /// Converts the entity to a JSON byte string.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="bytes"></param>
    /// <returns></returns>
    T? FromJson<T>(ByteString bytes);

    /// <summary>
    /// Converts the entity to a JSON byte string.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="bytes"></param>
    /// <param name="param"></param>
    /// <returns></returns>
    T? FromJson<T>(ByteString bytes, string param);

    /// <summary>
    /// Converts the entity to a JSON byte string.
    /// </summary>
    /// <param name="bytes"></param>
    /// <param name="type"></param>
    /// <returns></returns>
    object? FromJson(ByteString bytes, Type type);

    /// <summary>
    /// Converts the entity to a JSON byte string.
    /// </summary>
    /// <param name="str"></param>
    /// <param name="type"></param>
    /// <returns></returns>
    object? FromJson(string str, Type type);

    /// <summary>
    /// Converts the entity to a JSON byte string.
    /// </summary>
    /// <param name="str"></param>
    /// <param name="type"></param>
    /// <param name="param"></param>
    /// <returns></returns>
    object? FromJson(string str, Type type, string param);

    /// <summary>
    /// Maps the JSON to the specified type.
    /// </summary>
    /// <param name="type"></param>
    /// <param name="classMap"></param>
    /// <returns></returns>
    bool TryMapJson(Type type, out IClassMap? classMap);

    /// <summary>
    /// Map the class.
    /// </summary>
    void Map();

    /// <summary>
    /// Map assemblies.
    /// </summary>
    /// <param name="assemblies"></param>
    void Map(params Assembly[] assemblies);
    void SetDefaults();
}
