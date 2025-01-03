using System.Reflection;

using Dgraph4Net.Core;

namespace Dgraph4Net.ActiveRecords;

public readonly record struct DateTimePredicate(IClassMap ClassMap, PropertyInfo Property, string PredicateName, DateTimeToken Token = DateTimeToken.None, bool Upsert = false) : IPredicate
{
    readonly string IPredicate.ToSchemaPredicate() =>
        $"{PredicateName}: dateTime {(Token != DateTimeToken.None ? $"@index({Token.ToString().ToLowerInvariant()})" : Upsert ? $"@index({DateTimeToken.Hour})" : "")} {(Upsert ? "@upsert" : "")} .";

    readonly string IPredicate.ToTypePredicate() =>
        PredicateName;

    public static DateTimePredicate operator |(DateTimePredicate lpa1, DateTimePredicate lpa2) =>
        lpa1.Merge(lpa2);

    public DateTimePredicate Merge(DateTimePredicate lpa) =>
        new(ClassMap, Property, PredicateName, (DateTimeToken)Math.Max((int)Token, (int)lpa.Token), Upsert || lpa.Upsert);

    public IPredicate Merge(IPredicate p2) =>
        p2 switch
        {
            DateTimePredicate p => Merge(p),
            _ => ((IPredicate)this).ToSchemaPredicate().StartsWith(':') ? p2 : this
        };

    public void SetValue<T>(T? target, object? value) where T : IEntity
    {
        if (((IPredicate)this).SetFaceted(target, value))
            return;

        if ((Property.PropertyType == typeof(DateTime) || Property.PropertyType == typeof(DateTime?)) &&
            DateTimeOffset.TryParse(value.ToString(), out var dt))
        {
            Property.SetValue(target, dt.DateTime);
        }
        else if ((Property.PropertyType == typeof(DateTimeOffset) || Property.PropertyType == typeof(DateTimeOffset?)) &&
            DateTimeOffset.TryParse(value.ToString(), out var dto))
        {
            Property.SetValue(target, dto);
        }
        else if ((Property.PropertyType == typeof(DateOnly) || Property.PropertyType == typeof(DateOnly?)) &&
            DateTimeOffset.TryParse(value.ToString(), out var d))
        {
            Property.SetValue(target, DateOnly.FromDateTime(d.Date));
        }
    }
}
