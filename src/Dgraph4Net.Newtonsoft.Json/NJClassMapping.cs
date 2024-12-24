using System.Globalization;
using System.Reflection;
using Google.Protobuf;
using NetGeo.Json;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Dgraph4Net.ActiveRecords;

namespace Dgraph4Net;

internal class NjClassMapping : ClassMappingImpl, IClassMapping
{
    Func<object?, string> IClassMapping.JsonSerializer =>
        JsonConvert.SerializeObject;

    Func<string, Type, object?> IClassMapping.JsonDeserializer =>
        JsonConvert.DeserializeObject;

    public override void SetDefaults()
    {
        IIEntityConverter.Instance = typeof(EntityConverter);

        GeoExtensions.SetDefaults();

        var settings = JsonConvert.DefaultSettings?.Invoke() ?? new JsonSerializerSettings();

        if (settings.Converters.Any(x => x is UidConverter or EntityConverter))
            return;

        settings.Converters = new List<JsonConverter>(settings.Converters)
        {
            new UidConverter(),
            new EntityConverter()
        };

        settings.Culture = CultureInfo.InvariantCulture;
        settings.PreserveReferencesHandling = PreserveReferencesHandling.Objects;
        settings.ReferenceLoopHandling = ReferenceLoopHandling.Serialize;
        settings.TypeNameHandling = TypeNameHandling.Auto;
        settings.DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate;
        settings.NullValueHandling = NullValueHandling.Ignore;

        JsonConvert.DefaultSettings = () => settings;
    }

    private static JObject? GetData(ByteString bytes)
    {
        var element = Impl.Deserialize(bytes.ToStringUtf8(), typeof(JObject)) as JObject;

        if (element is null || element.TryGetValue("data", out var data) || data is null || data.Type != JTokenType.Object)
            return element;

        return data as JObject;
    }

    private class JsonClassMap<T> : ClassMap<T> where T : AEntity<T>
    {
        protected override void Map()
        {
            SetType(typeof(T).Name);

            var properties = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(x => x.GetCustomAttribute<JsonPropertyAttribute>() is not null &&
                    x.Name != "Id" && x.Name != "DgraphType");

            foreach (var prop in properties)
            {
                var attr = prop.GetCustomAttribute<JsonPropertyAttribute>();

                if (prop.PropertyType.IsEnum)
                {
                    if (prop.PropertyType.GetCustomAttribute<FlagsAttribute>(true) is not null)
                    {
                        ListString(prop, attr.PropertyName);
                    }
                    else
                    {
                        String(prop, attr.PropertyName, false, false, false, StringToken.Exact, false);
                    }
                }
                else
                {
                    if (TryGetType(prop.PropertyType, out var dataType))
                    {
                        switch (dataType)
                        {
                            case "uid":
                                HasOne(prop, attr.PropertyName.Replace("~", ""), attr.PropertyName is ['~', ..], false);
                                break;

                            case "string":
                                String(prop, attr.PropertyName, false, false, false, StringToken.Term, false);
                                break;

                            case "int":
                                Integer(prop, attr.PropertyName, true);
                                break;

                            case "float":
                                Float(prop, attr.PropertyName, true);
                                break;

                            case "datetime":
                                DateTime(prop, attr.PropertyName, Core.DateTimeToken.Hour, false);
                                break;

                            case "geo":
                                Geo(prop, attr.PropertyName, true, false);
                                break;
                        }
                    }
                    else if (dataType is ['[', ..])
                    {
                        switch (dataType)
                        {
                            case "[uid]":
                                HasMany(prop, attr.PropertyName);
                                break;

                            case "[string]":
                            case "[int]":
                            case "[float]":
                            case "[datetime]":
                            case "[geo]":
                                var dt = dataType[1..^1];
                                List(prop, dt, attr.PropertyName);
                                break;
                        }
                    }
                }
            }
        }
    }

    public override object? FromJson(ByteString bytes, Type type, string param)
    {
        if (bytes.IsEmpty)
            return default;

        var element = GetData(bytes);

        if (element is null)
            return default;

        if (!element.HasValues || !element.TryGetValue(param ?? "_", out var children) || children is null)
            return Deserialize(element.ToString(), type);

        return Deserialize(children.ToString(), type);
    }

    public override bool TryMapJson(Type type, out IClassMap? classMap)
    {
        if (ClassMappings.TryGetValue(type, out classMap))
            return true;

        var mapType = typeof(JsonClassMap<>).MakeGenericType(type);

        if (Activator.CreateInstance(mapType) is IClassMap map)
        {
            classMap = map;
            ClassMappings.TryAdd(type, map);

            map.Start();
            map.Finish();

            return true;
        }

        return false;
    }

    protected override IIEntityConverter GetConverter(bool ignoreNulls, bool getOnlyNulls, bool convertDefaultToNull) =>
        new EntityConverter(ignoreNulls, getOnlyNulls, convertDefaultToNull);
}
