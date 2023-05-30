#nullable enable

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.Json;

namespace Dgraph4Net.ActiveRecords;

public readonly record struct FloatPredicate(IClassMap ClassMap, PropertyInfo Property, string PredicateName, bool Index = false, bool Upsert = false) : IPredicate
{
    public ISet<IFacet> Facets { get; } = new HashSet<IFacet>();

    readonly string IPredicate.ToSchemaPredicate() =>
        $"{PredicateName}: float {(Index || Upsert ? "@index(float)" : "")} {(Upsert ? "@upsert" : "")} .";
    readonly string IPredicate.ToTypePredicate() =>
        PredicateName;
    public static FloatPredicate operator |(FloatPredicate lpa1, FloatPredicate lpa2) =>
        lpa1.Merge(lpa2);
    public FloatPredicate Merge(FloatPredicate lpa) =>
        new(ClassMap, Property, PredicateName, Index || lpa.Index, Upsert || lpa.Upsert);

    public IPredicate Merge(IPredicate p2) =>
        p2 switch
        {
            FloatPredicate p => Merge(p),
            _ => ((IPredicate)this).ToSchemaPredicate().StartsWith(':') ? p2 : this
        };

    public void SetValue(object? value, object? target)
    {
        if (value is null)
            return;

        if (value is JsonElement element)
        {
            if (element.ValueKind == JsonValueKind.Number)
            {
                if (Property.PropertyType == typeof(float) || Property.PropertyType == typeof(float?))
                    Property.SetValue(target, element.GetSingle());
                else if (Property.PropertyType == typeof(double) || Property.PropertyType == typeof(double?))
                    Property.SetValue(target, element.GetDouble());
                else if (Property.PropertyType == typeof(decimal) || Property.PropertyType == typeof(decimal?))
                    Property.SetValue(target, element.GetDecimal());
            }
            else if (element.ValueKind == JsonValueKind.String)
            {
                if (Property.PropertyType == typeof(float) || Property.PropertyType == typeof(float?))
                    Property.SetValue(target, float.Parse(element.GetString()));
                else if (Property.PropertyType == typeof(double) || Property.PropertyType == typeof(double?))
                    Property.SetValue(target, double.Parse(element.GetString()));
                else if (Property.PropertyType == typeof(decimal) || Property.PropertyType == typeof(decimal?))
                    Property.SetValue(target, decimal.Parse(element.GetString()));
            }
        }
        else
        {
            if ((Property.PropertyType == typeof(float) || Property.PropertyType == typeof(float?)) &&
            float.TryParse(value.ToString(), out var fl))
            {
                Property.SetValue(target, fl);
            }
            else if ((Property.PropertyType == typeof(double) || Property.PropertyType == typeof(double?)) &&
                double.TryParse(value.ToString(), out var db))
            {
                Property.SetValue(target, db);
            }
            else if ((Property.PropertyType == typeof(decimal) || Property.PropertyType == typeof(decimal?)) &&
                           decimal.TryParse(value.ToString(), out var dc))
            {
                Property.SetValue(target, dc);
            }
        }
    }
}
