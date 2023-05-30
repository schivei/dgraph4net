#nullable enable

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.Json;

namespace Dgraph4Net.ActiveRecords;

public readonly record struct IntegerPredicate(IClassMap ClassMap, PropertyInfo Property, string PredicateName, bool Index = false, bool Upsert = false) : IPredicate
{
    public ISet<IFacet> Facets { get; } = new HashSet<IFacet>();

    readonly string IPredicate.ToSchemaPredicate() =>
        $"{PredicateName}: int {(Index || Upsert ? "@index(int)" : "")} {(Upsert ? "@upsert" : "")} .";
    readonly string IPredicate.ToTypePredicate() =>
        PredicateName;

    public static IntegerPredicate operator |(IntegerPredicate lpa1, IntegerPredicate lpa2) =>
        lpa1.Merge(lpa2);

    public IntegerPredicate Merge(IntegerPredicate lpa) =>
        new(ClassMap, Property, PredicateName, Index || lpa.Index, Upsert || lpa.Upsert);

    public IPredicate Merge(IPredicate p2) =>
        p2 switch
        {
            IntegerPredicate p => Merge(p),
            _ => ((IPredicate)this).ToSchemaPredicate().StartsWith(':') ? p2 : this
        };

    public void SetValue(object? value, object? target)
    {
        if (value is null)
        {
            return;
        }

        if (value is JsonElement element)
        {
            if (element.ValueKind == JsonValueKind.Number)
            {
                if (element.TryGetInt32(out var i))
                {
                    Property.SetValue(target, i);
                }
                else if (element.TryGetInt64(out var l))
                {
                    Property.SetValue(target, l);
                }
                else if (element.TryGetDouble(out var d))
                {
                    Property.SetValue(target, d);
                }
                else if (element.TryGetDecimal(out var de))
                {
                    Property.SetValue(target, de);
                }
            }
            else if (element.ValueKind == JsonValueKind.String)
            {
                if (int.TryParse(element.GetString(), out var i))
                {
                    Property.SetValue(target, i);
                }
                else if (long.TryParse(element.GetString(), out var l))
                {
                    Property.SetValue(target, l);
                }
                else if (decimal.TryParse(element.GetString(), out var de))
                {
                    Property.SetValue(target, de);
                }
                else if (double.TryParse(element.GetString(), out var d))
                {
                    Property.SetValue(target, d);
                }
            }
        }
        else
        {
            if ((Property.PropertyType == typeof(int) || Property.PropertyType == typeof(int?)) && int.TryParse(value.ToString(), out var i))
            {
                Property.SetValue(target, i);
            }
            else if ((Property.PropertyType == typeof(long) || Property.PropertyType == typeof(long?)) && long.TryParse(value.ToString(), out var l))
            {
                Property.SetValue(target, l);
            }
            else if ((Property.PropertyType == typeof(short) || Property.PropertyType == typeof(short?)) && short.TryParse(value.ToString(), out var s))
            {
                Property.SetValue(target, s);
            }
            else if ((Property.PropertyType == typeof(byte) || Property.PropertyType == typeof(byte?)) && byte.TryParse(value.ToString(), out var b))
            {
                Property.SetValue(target, b);
            }
            else if ((Property.PropertyType == typeof(uint) || Property.PropertyType == typeof(uint?)) && uint.TryParse(value.ToString(), out var ui))
            {
                Property.SetValue(target, ui);
            }
            else if ((Property.PropertyType == typeof(ulong) || Property.PropertyType == typeof(ulong?)) && ulong.TryParse(value.ToString(), out var ul))
            {
                Property.SetValue(target, ul);
            }
            else if ((Property.PropertyType == typeof(ushort) || Property.PropertyType == typeof(ushort?)) && ushort.TryParse(value.ToString(), out var us))
            {
                Property.SetValue(target, us);
            }
            else if ((Property.PropertyType == typeof(sbyte) || Property.PropertyType == typeof(sbyte?)) && sbyte.TryParse(value.ToString(), out var sb))
            {
                Property.SetValue(target, sb);
            }
            else if (Property.PropertyType.IsEnum)
            {
                if (sbyte.TryParse(value.ToString(), out var enumValueSByte))
                    Property.SetValue(target, Enum.ToObject(Property.PropertyType, enumValueSByte));
                else if (byte.TryParse(value.ToString(), out var enumValueByte))
                    Property.SetValue(target, Enum.ToObject(Property.PropertyType, enumValueByte));
                else if (short.TryParse(value.ToString(), out var enumValueShort))
                    Property.SetValue(target, Enum.ToObject(Property.PropertyType, enumValueShort));
                else if (int.TryParse(value.ToString(), out var enumValueInt))
                    Property.SetValue(target, Enum.ToObject(Property.PropertyType, enumValueInt));
                else if (long.TryParse(value.ToString(), out var enumValueLong))
                    Property.SetValue(target, Enum.ToObject(Property.PropertyType, enumValueLong));
                else if (ushort.TryParse(value.ToString(), out var enumValueUShort))
                    Property.SetValue(target, Enum.ToObject(Property.PropertyType, enumValueUShort));
                else if (uint.TryParse(value.ToString(), out var enumValueUInt))
                    Property.SetValue(target, Enum.ToObject(Property.PropertyType, enumValueUInt));
                else if (ulong.TryParse(value.ToString(), out var enumValueULong))
                    Property.SetValue(target, Enum.ToObject(Property.PropertyType, enumValueULong));
            }
            else if (long.TryParse(value.ToString(), out var ticks))
            {
                if (Property.PropertyType == typeof(TimeSpan) || Property.PropertyType == typeof(TimeSpan?))
                {
                    Property.SetValue(target, TimeSpan.FromTicks(ticks));
                }
                else if (Property.PropertyType == typeof(TimeOnly) || Property.PropertyType == typeof(TimeOnly?))
                {
                    Property.SetValue(target, new DateTime(ticks));
                }
            }
        }
    }
}
