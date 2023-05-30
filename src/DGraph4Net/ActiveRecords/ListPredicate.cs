#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;

namespace Dgraph4Net.ActiveRecords;

public readonly record struct ListPredicate(IClassMap ClassMap, PropertyInfo Property, string PredicateName, string ListType, bool Count = true, bool Reversed = false) : IPredicate
{
    public ISet<IFacet> Facets { get; } = new HashSet<IFacet>();

    public ListPredicate Merge(ListPredicate lpa) =>
        new(ClassMap, Property, PredicateName, ListType, Count || lpa.Count);

    readonly string IPredicate.ToSchemaPredicate() =>
        Reversed ? $"{PredicateName}: uid @reverse {(Count ? "@count" : "")} ." : $"{PredicateName}: [{ListType}] {(Count ? "@count" : "")} .";

    readonly string IPredicate.ToTypePredicate() =>
        Reversed ? $"~{PredicateName}" :
        PredicateName;

    public static ListPredicate operator |(ListPredicate lpa1, ListPredicate lpa2) =>
        lpa1.Merge(lpa2);

    public IPredicate Merge(IPredicate p2) =>
        p2 switch
        {
            ListPredicate p => Merge(p),
            _ => ((IPredicate)this).ToSchemaPredicate().StartsWith(':') ? p2 : this
        };

    public void SetValue(object? value, object? target)
    {
        if (value is null)
            return;

        if (value is JsonElement element)
        {
            if (element.ValueKind == JsonValueKind.Array)
            {
                var values = element.EnumerateArray().Select(x => x.GetString()).ToArray();
                if (Property.PropertyType.IsArray)
                {
                    var array = Array.CreateInstance(Property.PropertyType.GetElementType()!, values.Length);
                    for (var i = 0; i < values.Length; i++)
                        array.SetValue(Convert.ChangeType(values[i], Property.PropertyType.GetElementType()!), i);

                    Property.SetValue(target, array);
                }
                else if (Property.PropertyType.IsGenericType && Property.PropertyType.GetGenericTypeDefinition() == typeof(List<>))
                {
                    var list = (IList<object>)Activator.CreateInstance(Property.PropertyType);
                    for (var i = 0; i < values.Length; i++)
                        list.Add(Convert.ChangeType(values[i], Property.PropertyType.GetGenericArguments()[0]!));

                    Property.SetValue(target, list);
                }
            }
            else if (element.ValueKind == JsonValueKind.String)
            {
                if (Property.PropertyType.IsArray)
                {
                    var array = Array.CreateInstance(Property.PropertyType.GetElementType()!, 1);
                    array.SetValue(Convert.ChangeType(element.GetString(), Property.PropertyType.GetElementType()!), 0);

                    Property.SetValue(target, array);
                }
                else if (Property.PropertyType.IsGenericType && Property.PropertyType.GetGenericTypeDefinition() == typeof(List<>))
                {
                    var list = (IList<object>)Activator.CreateInstance(Property.PropertyType);
                    list.Add(Convert.ChangeType(element.GetString(), Property.PropertyType.GetGenericArguments()[0]!));

                    Property.SetValue(target, list);
                }
            }
        }

        if (value.GetType().IsAssignableTo(Property.PropertyType))
        {
            Property.SetValue(target, value);
        }
        else if (Property.PropertyType.IsEnum && ListType == "string")
        {
            var values = (string[])value;
            var enumValue = (int)Enum.Parse(Property.PropertyType, values[0]);
            for (var i = 1; i < values.Length; i++)
                enumValue += (int)Enum.Parse(Property.PropertyType, values[i]);

            var en = Enum.ToObject(Property.PropertyType, enumValue);

            Property.SetValue(target, en);
        }
        else if (Property.PropertyType.IsEnum && ListType == "int")
        {
            var values = (int[])value;
            var enumValue = values.Sum();

            var en = Enum.ToObject(Property.PropertyType, enumValue);

            Property.SetValue(target, en);
        }
    }
}
