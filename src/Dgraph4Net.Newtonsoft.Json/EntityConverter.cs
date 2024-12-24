using System.Text;
using Newtonsoft.Json;

namespace Dgraph4Net;

internal partial class EntityConverter(bool ignoreNulls, bool getOnlyNulls = false, bool convertDefaultToNull = false) : JsonConverter
{
    public bool IgnoreNulls { get; set; } = ignoreNulls;
    public bool GetOnlyNulls { get; set; } = getOnlyNulls;
    public bool ConvertDefaultToNull { get; set; } = convertDefaultToNull;

    public EntityConverter() : this(true) { }

    public override bool CanConvert(Type objectType) =>
        objectType.IsAssignableTo(typeof(IEntity));

    private Setter GetSetter(JsonSerializer serializer) =>
        new(serializer, this);

    public override object? ReadJson(JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer)
    {
        var eReader = new EntityConverterReader(GetSetter);

        return eReader.ReadJson(reader, objectType, existingValue, serializer);
    }

    public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
    {
        var eWriter = new EntityConverterWriter(IgnoreNulls, GetOnlyNulls, ConvertDefaultToNull);

        eWriter.WriteJson(writer, value, serializer);
    }

    public partial T? Deserialize<T>(string json) where T : IEntity
    {
        using var reader = new JsonTextReader(new StringReader(json));
        return (T?)ReadJson(reader, typeof(T), null, new());
    }

    public partial string Serialize<T>(T entity, bool ignoreNulls, bool getOnlyNulls, bool convertDefaultToNull) where T : IEntity
    {
        IgnoreNulls = ignoreNulls;
        GetOnlyNulls = getOnlyNulls;
        ConvertDefaultToNull = convertDefaultToNull;

        var sb = new StringBuilder();
        using var sw = new StringWriter(sb);
        using var writer = new JsonTextWriter(sw);
        WriteJson(writer, entity, new()
        {
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
        });
        return sb.ToString();
    }

    public static partial string SerializeEntity<T>(T entity, bool ignoreNulls, bool getOnlyNulls, bool convertDefaultToNull) where T : IEntity =>
        new EntityConverter(ignoreNulls, getOnlyNulls, convertDefaultToNull).Serialize(entity, ignoreNulls, getOnlyNulls, convertDefaultToNull);

    public static partial string SerializeEntities<T>(IEnumerable<T> entities, bool ignoreNulls, bool getOnlyNulls, bool convertDefaultToNull) where T : IEntity
    {
        var sb = new StringBuilder();
        sb.Append('[');

        foreach (var entity in entities)
        {
            sb.Append(SerializeEntity(entity, ignoreNulls, getOnlyNulls, convertDefaultToNull));
            sb.Append(',');
        }

        if (sb.Length > 1)
            sb.Remove(sb.Length - 1, 1);

        sb.Append(']');

        return sb.ToString();
    }
}
