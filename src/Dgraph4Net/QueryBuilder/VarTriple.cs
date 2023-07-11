using System.Globalization;

namespace Dgraph4Net;

public readonly struct VarTriple
{
    public string Name { get; }
    public VarType Type { get; }
    public string Value { get; }
    public string TypeName => Type is VarType.String ? "string" : "int";

    public VarTriple(string name, VarType type, object value)
    {
        Name = name;
        Type = type;
        if (value is not ulong and not uint && type is VarType.Integer)
        {
            Value = Convert.ToString(Convert.ToInt64(value), CultureInfo.InvariantCulture);
        }
        else
        {
            Type = VarType.String;

            if (value is DateTime dt)
                Value = dt.ToString("O", CultureInfo.InvariantCulture);
            else if (value is DateTimeOffset dto)
                Value = dto.ToString("O", CultureInfo.InvariantCulture);
            else if (value is DateOnly d)
                Value = d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            else
                Value = Convert.ToString(value, CultureInfo.InvariantCulture);
        }
    }

    public VarTriple(string name, object value)
    {
        Name = name;

        Type = value switch
        {
            ushort => VarType.Integer,
            byte => VarType.Integer,
            long => VarType.Integer,
            int => VarType.Integer,
            short => VarType.Integer,
            sbyte => VarType.Integer,
            _ => VarType.String
        };

        if (Type is VarType.Integer)
        {
            Value = Convert.ToString(Convert.ToInt64(value), CultureInfo.InvariantCulture);
        }
        else
        {
            Type = VarType.String;

            if (value is DateTime dt)
                Value = dt.ToString("O", CultureInfo.InvariantCulture);
            else if (value is DateTimeOffset dto)
                Value = dto.ToString("O", CultureInfo.InvariantCulture);
            else if (value is DateOnly d)
                Value = d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            else
                Value = Convert.ToString(value, CultureInfo.InvariantCulture);
        }
    }

    public readonly KeyValuePair<string, string> ToKeyValuePair() => new($"${Name}", Value);
}
