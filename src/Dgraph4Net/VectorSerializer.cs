using System.Globalization;
using System.Numerics;
using System.Text;

namespace Dgraph4Net;

public static class VectorSerializer
{
    public static float[] ToArray(this Vector<float> vector)
    {
        Span<float> items = new float[Vector<float>.Count];

        vector.CopyTo(items);

        return items.ToArray();
    }

    public static Vector<float> ToVector(this float[] array) => new(array);

    public static string Serialize(this Vector<float> vector)
    {
        var sb = new StringBuilder();

        sb
            .Append('[')
            .Append(string.Join(' ', vector.ToArray().Select(i => i.ToString(CultureInfo.InvariantCulture))))
            .Append(']');

        return sb.ToString();
    }

    public static Vector<float> DeserializeVector(this string value)
    {
        var items = value
            .TrimStart('[')
            .TrimEnd(']')
            .Split(' ')
            .Select(float.Parse)
            .ToArray();

        return items.ToVector();
    }
}
