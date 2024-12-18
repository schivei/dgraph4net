using System.Linq.Expressions;
using System.Text.RegularExpressions;
using Dgraph4Net.ActiveRecords;

namespace Dgraph4Net;

public static class TypeExtensions
{
    public static string DgraphType(this Type type)
    {
        if (ClassMapping.ClassMappings.TryGetValue(type, out var classMap))
            return classMap.DgraphType;

        return type.Name;
    }

    public static string DgraphType<T>(this T _) where T : IEntity =>
        DType<T>.Name;

    public static string Predicate(this Type type, string propertyName)
    {
        if (!ClassMapping.ClassMappings.TryGetValue(type, out var classMap))
            return propertyName;

        var predicate = ClassMap.Predicates.Where(x => x.Value.ClassMap == classMap && x.Key.Name == propertyName).Select(x => x.Value).FirstOrDefault();
        if (predicate is not null)
            return predicate.PredicateName;

        return propertyName;
    }

    public static string Predicate<T>(this T _, Expression<Func<T, object?>> expression) where T : IEntity =>
        Predicate(expression);

    public static string Predicate<T>(Expression<Func<T, object?>> expression) where T : IEntity =>
        DType<T>.Predicate(expression);

    private static readonly char[] s_chars = [
        .. Enumerable.Range('a', 26).Select(Convert.ToChar),
        .. Enumerable.Range('A', 26).Select(Convert.ToChar),
        .. Enumerable.Range('0', 10).Select(Convert.ToChar)
    ];

    public static string AutoName(this object obj)
    {
        var nameSize = obj.GetType().Name.Length + 1;
        if (nameSize > 16)
            nameSize = 16;

        if (nameSize < 8)
            nameSize = 8;

        var chars = s_chars.OrderBy(x => Guid.NewGuid()).Take(nameSize).ToArray();

        return new(chars);
    }

    public static string ConcreteUid<T>(this T value)
    {
        Uid? uid = null;

        if (value is IEntity entity)
            uid = entity.Uid;

        if (value is Uid u)
            uid = u;

        if (!uid.HasValue)
            throw new ArgumentException("The value does not have a Uid.");

        if (!uid.Value.IsConcrete)
            throw new ArgumentException("The value does not have a concrete Uid.");

        return uid.Value;
    }

    /// <summary>
    /// Validate if the string is a valid golang regular expression
    /// </summary>
    /// <remarks>
    /// See https://pkg.go.dev/regexp/syntax for more information.
    /// </remarks>
    /// <param name="regexp"></param>
    /// <returns></returns>
    public static bool IsGoRegexp(this string pattern)
    {
        try
        {
            if (pattern is null or { Length: < 2 } or ['/', ..])
                return false;

            var regexContent = pattern[1..];
            var flags = string.Empty;

            var flagsStart = regexContent.Length;
            for (var i = regexContent.Length - 1; i >= 0; i--)
            {
                var currentChar = regexContent[i];
                if ("imsU".Contains(currentChar))
                {
                    flagsStart = i;
                    flags = currentChar + flags;
                }
                else
                {
                    break;
                }
            }

            var regexWithoutFlags = regexContent[0..flagsStart];

            var options = RegexOptions.None;

            foreach (var flag in flags)
            {
                switch (flag)
                {
                    case 'i':
                        options |= RegexOptions.IgnoreCase;
                        break;
                    case 'm':
                        options |= RegexOptions.Multiline;
                        break;
                    case 's':
                        options |= RegexOptions.Singleline;
                        break;
                    case 'U':
                        // not supported by C# regex
                        break;
                }
            }

            _ = new Regex(regexWithoutFlags, options);
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }
}
