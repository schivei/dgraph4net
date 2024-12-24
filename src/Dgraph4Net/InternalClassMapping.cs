using System.Collections.Concurrent;
using System.Reflection;
using Google.Protobuf;

using static Grpc.Core.Metadata;

namespace Dgraph4Net;

internal static class InternalClassMapping
{
    internal static ConcurrentDictionary<Type, IClassMap> ClassMappings { get; }

    internal static object? ToJsonByteStringFuncInstance { get; set; }

    internal static MethodInfo? ToJsonByteStringFunc { get; set; }

    internal static object? FromJsonByteStringFuncInstance { get; set; }

    internal static MethodInfo? FromJsonByteStringFunc { get; set; }

    static InternalClassMapping() =>
        ClassMappings = new();

    public static string GetDgraphType(Type type) =>
        ClassMappings.TryGetValue(type, out var classMap) ? classMap.DgraphType : type.Name;

    public static string GetDgraphType<T>() =>
        GetDgraphType(typeof(T));

    public static List<Enum> GetFlaggedValues(this Enum value)
    {
        var enumType = value.GetType();
        var hasFlags = enumType.GetCustomAttributes(true).Any(attr => attr is FlagsAttribute);

        List<Enum> values = [];
        if (hasFlags)
            values.AddRange(Enum.GetValues(enumType).Cast<Enum>().Where(value.HasFlag));
        else
            values.Add(value);

        return values;
    }

    public static ByteString ToJson<T>(this T entity) where T : IEntity =>
        ToJson([entity]);

    public static ByteString ToJson<T>(this IEnumerable<T> entities) where T : IEntity =>
        ToJsonByteStringFunc.MakeGenericMethod(typeof(T)).Invoke(ToJsonByteStringFuncInstance, [entities]) as ByteString;

    public static object? FromJson(this ByteString bytes, Type type) =>
        FromJsonByteStringFunc.Invoke(FromJsonByteStringFuncInstance, [bytes, type]);
}
