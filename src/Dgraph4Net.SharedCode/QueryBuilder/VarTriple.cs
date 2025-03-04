using System.Collections;
using System.Globalization;
using System.Numerics;
using System.Text;

namespace Dgraph4Net;

public readonly struct VarTriple
{
    public string OriginName { get; }
    public string Name { get; }
    public VarType Type { get; }
    public string Value { get; }
    public string TypeName => Type is VarType.String ? "string" : "int";

    public VarTriple(string name, object value)
    {
        ArgumentNullException.ThrowIfNull(value);

        Name = '$' + name;

        Type = GetVarType(value);

        Value = Cast(value);
    }

    public VarTriple(string name, object value, string originName) : this(name, value)
    {
        ArgumentNullException.ThrowIfNull(originName);

        OriginName = originName;
    }

    private static VarType GetVarType(object value) =>
        value switch
        {
            ushort => VarType.Integer,
            byte => VarType.Integer,
            long => VarType.Integer,
            int => VarType.Integer,
            short => VarType.Integer,
            sbyte => VarType.Integer,
            _ => VarType.String
        };

    internal static string Cast(object value)
    {
        ArgumentNullException.ThrowIfNull(value);

        var type = GetVarType(value);

        if (type is VarType.Integer)
            return Convert.ToString(Convert.ToInt64(value), CultureInfo.InvariantCulture);

        if (value is DateTime dt)
            return dt.ToString("O", CultureInfo.InvariantCulture);

        if (value is DateTimeOffset dto)
            return dto.ToString("O", CultureInfo.InvariantCulture);

        if (value is DateOnly d)
            return d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        if (value is Vector<float> v)
            return v.Serialize();

        if (value is IEnumerable enumerable and not string)
        {
            var str = new StringBuilder();
            str.Append('[');

            foreach (var item in enumerable)
            {
                str.Append(Cast(item));
                str.Append(',');
            }

            str[^1] = ']';

            return str.ToString();
        }

        if (value is true)
            return "true";

        if (value is false)
            return "false";

        return Convert.ToString(value, CultureInfo.InvariantCulture).Replace("\"", "\\\"");
    }

    public readonly KeyValuePair<string, string> ToKeyValuePair() => new(Name, Value);

    public override string ToString() => $"{Name}: {TypeName}";

    public static VarTriple Parse(object? obj)
    {
        ArgumentNullException.ThrowIfNull(obj);

        if (obj is VarTriple vt)
            return vt;

        var name = obj.AutoName();

        return (name, obj);
    }

    public static VarTriple Parse(object? obj, string originName)
    {
        ArgumentNullException.ThrowIfNull(obj);

        if (obj is VarTriple vt)
            return vt;

        var name = obj.AutoName();

        return (name, obj, originName);
    }

    public static implicit operator VarTriple((string name, object value) vt) =>
        new(vt.name, vt.value);

    public static implicit operator VarTriple((string name, object value, string originName) vt) =>
        new(vt.name, vt.value, vt.originName);
}
