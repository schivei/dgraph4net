using System.Reflection;

namespace Dgraph4Net.ActiveRecords;

public readonly record struct StringPredicate(IClassMap ClassMap, PropertyInfo Property, string PredicateName, bool Fulltext, bool Trigram, bool Upsert, StringToken Token, string? Cultures = null) : IPredicate
{
    public ISet<IFacet> Facets { get; } = new HashSet<IFacet>();

    public readonly StringPredicate Merge(StringPredicate spa) =>
        new(ClassMap, Property, PredicateName, Fulltext || spa.Fulltext, Trigram || spa.Trigram, Upsert || spa.Upsert, (StringToken)Math.Max((int)spa.Token, (int)Token), Concat(GetCultures(), spa.GetCultures()));

    private static string? Concat(string[] strings1, string[] strings2)
    {
        if (strings1.Length == 0 && strings2.Length == 0)
            return null;
        var strings = strings1.Concat(strings2).Distinct().ToArray();
        return string.Join(",", strings);
    }

    public readonly string[] GetCultures() =>
        Cultures?.Split(",", StringSplitOptions.RemoveEmptyEntries) ?? Array.Empty<string>();

    readonly string IPredicate.ToSchemaPredicate() =>
        $"{PredicateName}: string{ToIndex()} .";

    readonly string IPredicate.ToTypePredicate() =>
        PredicateName;

    private readonly string ToIndex()
    {
        if (Fulltext || Trigram || Upsert || Token != StringToken.None)
        {
            var predicate = " @index(";
            predicate += Fulltext ? "fulltext" : "";
            var tr = predicate.Contains("fulltext") ? ", trigram" : "trigram";
            predicate += Trigram ? tr : "";

            var tk = Token switch
            {
                StringToken.Exact => "exact",
                StringToken.Hash => "hash",
                StringToken.Term => "term",
                _ => ""
            };

            var fll = predicate.Contains("fulltext") || predicate.Contains("trigram") ? $", {tk}" : tk;

            predicate += !string.IsNullOrEmpty(tk) ? fll : "";

            predicate += GetCultures().Any() ? ") @lang" : ")";
            predicate += Upsert ? " @upsert" : "";

            return predicate;
        }
        else
        {
            return GetCultures().Any() ? " @lang" : "";
        }
    }

    public static StringPredicate operator |(StringPredicate spa1, StringPredicate spa2) =>
        spa1.PredicateName == spa2.PredicateName ? spa1.Merge(spa2) : throw new ArgumentException("Invalid predicate name.");

    public IPredicate Merge(IPredicate p2) =>
        p2 switch
        {
            StringPredicate spa => this | spa,
            _ => ((IPredicate)this).ToSchemaPredicate().StartsWith(':') ? p2 : this
        };

    public void SetValue(object? value, object? target)
    {
        if (value is null)
            return;

        if (Property.PropertyType == typeof(byte[]))
        {
            Property.SetValue(target, Convert.FromBase64String(value.ToString() ?? ""));
        }
        else if (Property.PropertyType == typeof(string))
        {
            Property.SetValue(target, value);
        }
        else if (Property.PropertyType.IsEnum)
        {
            Property.SetValue(target, Enum.Parse(Property.PropertyType, value.ToString() ?? "", true));
        }
        else if (Property.PropertyType.IsValueType && Property.PropertyType.GetMethod("TryParse", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(string), Property.PropertyType.MakeByRefType() }, null) is MethodInfo tryParse && tryParse.ReturnType == typeof(bool))
        {
            var parameters = new object[] { value, Activator.CreateInstance(Property.PropertyType) };
            if ((bool)tryParse.Invoke(null, parameters))
                Property.SetValue(target, parameters[1]);
        }
    }
}
