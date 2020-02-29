using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Runtime;
using System.Text;
using DGraph4Net.Annotations;
using DGraph4Net.Services;
using Newtonsoft.Json;

// ReSharper disable once CheckNamespace
namespace DGraph4Net
{
    [SuppressMessage("ReSharper", "UnusedMember.Global")]
    public static class DGraphExtensions
    {
        /// <summary>
        /// Generates mapping
        /// </summary>
        public static StringBuilder Map(this DGraph dGraph)
        {
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();

            var types = assemblies
                .SelectMany(assembly => assembly.GetTypes()
                    .Where(t => t.GetCustomAttributes()
                        .Any(att => att is DGraphTypeAttribute)))
                .OrderBy(t => t.GetProperties()
                    .Any(pi => pi.GetCustomAttributes()
                        .Any(a => a is PredicateReferencesToAttribute)));

            var properties =
            types.SelectMany(type => type.GetProperties()
                .Where(prop => prop.GetCustomAttributes()
                    .Any(attr => attr is StringPredicateAttribute ||
                                 attr is CommonPredicateAttribute ||
                                 attr is DateTimePredicateAttribute ||
                                 attr is PasswordPredicateAttribute ||
                                 attr is ReversePredicateAttribute)).Select(prop => (type, prop)));

            var triples =
            properties.Where(pp => pp.prop.DeclaringType != null &&
                                     !typeof(IDictionary).IsAssignableFrom(pp.prop.PropertyType) &&
                                     !typeof(KeyValuePair).IsAssignableFrom(pp.prop.PropertyType) &&
                                     pp.prop.GetCustomAttributes()
                                         .Where(attr => attr is JsonPropertyAttribute)
                                         .OfType<JsonPropertyAttribute>()
                                         .All(jattr =>
                                             jattr.PropertyName?.Contains("|") != true))
                .Select(pp =>
                {
                    var (type, prop) = pp;
                    var jattr =
                        prop.GetCustomAttributes()
                            .Where(attr => attr is JsonPropertyAttribute)
                            .OfType<JsonPropertyAttribute>().FirstOrDefault() ??
                        new JsonPropertyAttribute(NormalizeName(prop.Name));

                    if (jattr.PropertyName == "dgraph.type" || jattr.PropertyName == "uid")
                        return (null, null, null, null, null);

                    var pAttr = prop
                                    .GetCustomAttributes()
                                    .FirstOrDefault(attr => attr is StringPredicateAttribute ||
                                                            attr is CommonPredicateAttribute ||
                                                            attr is DateTimePredicateAttribute ||
                                                            attr is PasswordPredicateAttribute ||
                                                            attr is ReversePredicateAttribute) ??
                                new CommonPredicateAttribute();

                    var princType = prop.PropertyType.IsGenericType
                        ? prop.PropertyType.GetGenericArguments().First()
                        : prop.PropertyType;

                    var isList = prop.PropertyType != typeof(string) &&
                                 typeof(IEnumerable).IsAssignableFrom(prop.PropertyType);

                    if (!isList && prop.PropertyType.IsGenericType)
                        princType = prop.PropertyType;

                    if (isList && typeof(char[]) == prop.PropertyType)
                    {
                        isList = false;
                        princType = prop.PropertyType;
                    }

                    var propType =
                        princType == typeof(Uid) ||
                        princType == typeof(Uid?) ? "uid" :
                        princType.IsEnum ||
                        princType == typeof(string) ||
                        princType == typeof(char) ||
                        princType == typeof(char[]) ||
                        princType == typeof(char?) ||
                        princType == typeof(TimeSpan) ||
                        princType == typeof(TimeSpan?) ? "string" :
                        princType == typeof(float) ||
                        princType == typeof(double) ||
                        princType == typeof(decimal) ||
                        princType == typeof(float?) ||
                        princType == typeof(double?) ||
                        princType == typeof(decimal?) ? "float" :
                        princType == typeof(int) ||
                        princType == typeof(uint) ||
                        princType == typeof(long) ||
                        princType == typeof(ulong) ||
                        princType == typeof(short) ||
                        princType == typeof(ushort) ||
                        princType == typeof(byte) ||
                        princType == typeof(sbyte) ||
                        princType == typeof(int?) ||
                        princType == typeof(uint?) ||
                        princType == typeof(long?) ||
                        princType == typeof(ulong?) ||
                        princType == typeof(short?) ||
                        princType == typeof(ushort?) ||
                        princType == typeof(byte?) ||
                        princType == typeof(sbyte?) ? "int" :
                        princType == typeof(bool) ||
                        princType == typeof(bool?) ? "bool" :
                        princType == typeof(DateTime) ||
                        princType == typeof(DateTimeOffset) ||
                        princType == typeof(DateTime?) ||
                        princType == typeof(DateTimeOffset?) ? "datetime" :
                        new[]
                        {
                            "Point", "LineString", "Polygon", "MultiPoint", "MultiLineString", "MultiPolygon",
                            "GeometryCollection"
                        }.Contains(princType.Name) ? "geo" :
                        pAttr is ReversePredicateAttribute ? "uid" : "default";

                    var predicate = $"<{jattr.PropertyName}>: ";
                    predicate += isList ? "[{0}]" : "{0}";

                    switch (pAttr)
                    {
                        case ReversePredicateAttribute _ when propType == "uid":
                            propType = "uid";
                            predicate = string.Format(predicate, "uid");

                            predicate += isList ? " @reverse @count ." : " @reverse .";
                            break;
                        case StringPredicateAttribute sa when propType == "string":
                            propType = "string";
                            predicate = string.Format(predicate, "string");
                            if (sa.Fulltext || sa.Trigram || sa.Upsert || sa.Token != StringToken.None)
                            {
                                predicate += " @index(";
                                predicate += sa.Fulltext ? "fulltext" : "";
                                predicate += sa.Trigram
                                    ? predicate.Contains("fulltext") ? ",trigram" : "trigram"
                                    : "";

                                var tk = sa.Token switch
                                {
                                    StringToken.Exact => "exact",
                                    StringToken.Hash => "hash",
                                    StringToken.Term => "term",
                                    _ => ""
                                };

                                predicate += !string.IsNullOrEmpty(tk)
                                    ? predicate.Contains("fulltext") || predicate.Contains("trigram") ? $",{tk}" : tk
                                    : "";

                                predicate += sa.Lang ? ") @lang" : ")";
                                predicate += sa.Upsert ? " @upsert" : "";
                            }
                            else
                            {
                                predicate += sa.Lang ? " @lang" : "";
                            }

                            predicate += isList ? " @count ." : " .";
                            break;

                        case CommonPredicateAttribute pa:
                            predicate = string.Format(predicate, propType);
                            if (pa.Upsert || pa.Index)
                            {
                                predicate += $" @index({propType})";
                                predicate += pa.Upsert ? " @upsert" : "";
                            }

                            predicate += isList ? " @count ." : " .";
                            break;

                        case DateTimePredicateAttribute da:
                            predicate = string.Format(predicate, propType);
                            if (da.Upsert || da.Token != DateTimeToken.None)
                            {
                                if (da.Token == DateTimeToken.None)
                                    da.Token = DateTimeToken.Year;

                                predicate += $" @index({da.Token.ToString().ToLowerInvariant()})";
                                predicate += da.Upsert ? " @upsert" : "";
                            }

                            predicate += isList ? " @count ." : " .";
                            break;

                        case PasswordPredicateAttribute _ when propType == "string":
                            propType = "password";
                            predicate = string.Format(predicate, "password");

                            predicate += " .";
                            break;

                        default:
                            predicate = string.Format(predicate, propType);

                            predicate += isList ? " @count ." : " .";
                            break;
                    }

                    return (jattr.PropertyName, predicate, type.GetCustomAttribute<DGraphTypeAttribute>().Name, propType, prop);
                }).Where(x => x.PropertyName != null);

            var ambiguous = triples.GroupBy(x => x.PropertyName)
                .Where(x => x.Select(y => y.predicate).Distinct().Count() > 1);

            var imps = ambiguous.Select(s => s.Key).ToArray();

            if (imps.Length > 1)
                throw new AmbiguousImplementationException($"There are two or more different implementations of: {string.Join(',', imps).Trim(',').Replace(",", ", ")}.");

            var sb = new StringBuilder();

            foreach (var (_, predicate, _, _, _) in triples)
                sb.AppendLine(predicate);

            sb.AppendLine();

            var ts =
            triples.GroupBy(triple => triple.Name)
                .Select(tp =>
                {
                    var typename = tp.Key;
                    var typeProperties = tp.Select(pred =>
                    {
                        var (propertyName, predicate, _, _, prop) = pred;
                        var nm = predicate.Contains("@reverse") ?
                            $"<~{propertyName}>" :
                            $"<{propertyName}>";

                        var r =
                        prop.GetCustomAttributes()
                            .Where(a => a is PredicateReferencesToAttribute)
                            .OfType<PredicateReferencesToAttribute>()
                            .FirstOrDefault();

                        if (r == null) return $"{nm}: {pred.propType}";

                        var tn = r.RefType.GetCustomAttribute<DGraphTypeAttribute>().Name;

                        if (predicate.Contains("["))
                            tn = $"[{tn}]";

                        return $"{nm}: {tn}";
                    });

                    return $@"type {typename} {{
{string.Join("\n", typeProperties.Select(s => $"  {s}"))}
}}";
                });

            foreach (var type in ts)
                sb.Append(type + "\n");

            var schema = sb.ToString().Replace("\r\n", "\n").Trim('\n') + '\n';

            var op = new Operation
            {
                DropAll = false,
                Schema = schema
            };

            dGraph.Alter(op).GetAwaiter().GetResult();

            return sb;
        }

        private static string NormalizeName(string propName)
            => string.Join("", propName
                    .Select(c => char.IsUpper(c) ? $"_{char.ToLowerInvariant(c)}" : c.ToString()))
                .Trim('_');
    }
}
