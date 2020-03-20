using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime;
using System.Text;
using Dgraph4Net.Annotations;
using Newtonsoft.Json;

namespace Dgraph4Net
{

    [SuppressMessage("ReSharper", "UnusedMember.Global")]
    public static class DgraphExtensions
    {
        /// <summary>
        /// Generates mapping
        /// </summary>
        public static StringBuilder Map(this IDgraph4NetClient dgraph, params Assembly[] assemblies)
        {
            var types = assemblies
                .SelectMany(assembly => assembly.GetTypes()
                    .Where(t => t.GetCustomAttributes()
                        .Any(att => att is DgraphTypeAttribute)))
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
            properties.Where(pp => !(pp.prop.DeclaringType is null) &&
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

                    var isUid = princType == typeof(Uid) ||
                                princType == typeof(Uid?) ||
                                pAttr is ReversePredicateAttribute;

                    var isString = princType.IsEnum ||
                                   princType == typeof(string) ||
                                   princType == typeof(char) ||
                                   princType == typeof(char[]) ||
                                   princType == typeof(char?) ||
                                   princType == typeof(TimeSpan) ||
                                   princType == typeof(TimeSpan?);

                    var isFloat = princType == typeof(float) ||
                                  princType == typeof(double) ||
                                  princType == typeof(decimal) ||
                                  princType == typeof(float?) ||
                                  princType == typeof(double?) ||
                                  princType == typeof(decimal?);

                    var isInt = princType == typeof(int) ||
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
                                princType == typeof(sbyte?);

                    var isBool = princType == typeof(bool) ||
                                 princType == typeof(bool?);

                    var isDate = princType == typeof(DateTime) ||
                                 princType == typeof(DateTimeOffset) ||
                                 princType == typeof(DateTime?) ||
                                 princType == typeof(DateTimeOffset?);

                    var isGeo = new[]
                    {
                        "Point", "LineString", "Polygon", "MultiPoint", "MultiLineString", "MultiPolygon",
                        "GeometryCollection"
                    }.Contains(princType.Name);

                    string propType;
                    if (isUid)
                    {
                        propType = "uid";
                    }
                    else if (isString)
                    {
                        propType = "string";
                    }
                    else if (isFloat)
                    {
                        propType = "float";
                    }
                    else if (isInt)
                    {
                        propType = "int";
                    }
                    else if (isBool)
                    {
                        propType = "bool";
                    }
                    else if (isDate)
                    {
                        propType = "datetime";
                    }
                    else if (isGeo)
                    {
                        propType = "geo";
                    }
                    else
                    {
                        propType = "default";
                    }

                    var predicate = $"<{jattr.PropertyName}>: ";
                    predicate += isList ? "[{0}]" : "{0}";

                    var isReverse = pAttr is ReversePredicateAttribute;

                    switch (pAttr)
                    {
                        case ReversePredicateAttribute _:
                            propType = "uid";
                            predicate = string.Format(predicate, "uid");
                            break;
                        case StringPredicateAttribute sa when propType == "string":
                            propType = "string";
                            predicate = string.Format(predicate, "string");
                            if (sa.Fulltext || sa.Trigram || sa.Upsert || sa.Token != StringToken.None)
                            {
                                predicate += " @index(";
                                predicate += sa.Fulltext ? "fulltext" : "";
                                var tr = predicate.Contains("fulltext") ? ", trigram" : "trigram";
                                predicate += sa.Trigram ? tr : "";

                                var tk = sa.Token switch
                                {
                                    StringToken.Exact => "exact",
                                    StringToken.Hash => "hash",
                                    StringToken.Term => "term",
                                    _ => ""
                                };

                                var fll = predicate.Contains("fulltext") || predicate.Contains("trigram") ? $", {tk}" : tk;

                                predicate += !string.IsNullOrEmpty(tk) ? fll : "";

                                predicate += sa.Lang ? ") @lang" : ")";
                                predicate += sa.Upsert ? " @upsert" : "";
                            }
                            else
                            {
                                predicate += sa.Lang ? " @lang" : "";
                            }
                            break;

                        case CommonPredicateAttribute pa:
                            predicate = string.Format(predicate, propType);
                            if (pa.Upsert || pa.Index)
                            {
                                predicate += $" @index({propType})";
                                predicate += pa.Upsert ? " @upsert" : "";
                            }
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
                            break;

                        case PasswordPredicateAttribute _ when propType == "string":
                            propType = "password";
                            predicate = string.Format(predicate, "password");
                            break;

                        default:
                            predicate = string.Format(predicate, propType);
                            break;
                    }

                    var rev = isReverse && isList ? " @count @reverse ." : " @reverse .";

                    var lt = isList ? " @count ." : " .";

                    predicate += isReverse ? rev : lt;

                    return (jattr.PropertyName, predicate, type.GetCustomAttribute<DgraphTypeAttribute>().Name, propType, prop);
                }).Where(x => !(x.PropertyName is null)).ToArray();

            var ambiguous = triples.GroupBy(x => x.PropertyName)
                .Where(x => x.Select(y => y.predicate).Distinct().Count() > 1);

            var imps = ambiguous.SelectMany(s => s.Select(p => (p.prop.Name, p.prop.DeclaringType.Name))).ToArray();

            if (imps.Length > 1)
            {
                throw new AmbiguousImplementationException($@"There are two or more different implementations of: {(string.Join(',', imps.Select(i => i.Item1).Distinct())
                    .Trim(',').Replace(",", ", "))}.\n\nImplementations: \n{string.Join("\n", imps.Select(i => $"{i.Item2}::{i.Item1}"))}");
            }

            var sb = new StringBuilder();

            foreach (var predicate in triples.OrderBy(p => p.PropertyName).GroupBy(tp => tp.predicate)
                .Select(tp => tp.Key))
                sb.AppendLine(predicate);

            sb.AppendLine();

            var ts = triples.OrderBy(p => p.Name).GroupBy(triple => triple.Name)
                .Select(tp =>
                {
                    var typename = tp.Key;
                    var typeProperties = tp.Select(pred =>
                    {
                        var (propertyName, predicate, _, _, prop) = pred;
                        var nm = predicate.Contains("@reverse") ? $"<~{propertyName}>" : $"<{propertyName}>";

                        var r =
                            prop.GetCustomAttributes()
                                .Where(a => a is PredicateReferencesToAttribute)
                                .OfType<PredicateReferencesToAttribute>()
                                .FirstOrDefault();

                        if (r is null)
                            return $"{nm}: {pred.propType}";

                        var refType = ClassFactory.GetDerivedType(r.RefType) ?? r.RefType;

                        var tn = refType.GetCustomAttribute<DgraphTypeAttribute>().Name;

                        if (predicate.Contains("["))
                            tn = $"[{tn}]";

                        return $"{nm}: {tn}";
                    });

                    var names = string.Join("\n", typeProperties
                        .Select(s => $"  {s}"));

                    return $@"type {typename} {{
{names}
}}";
                });

            foreach (var type in ts)
                sb.Append(type).Append('\n');

            var schema = sb.ToString().Replace("\r\n", "\n").Trim('\n') + '\n';

            if (File.Exists("schema.dgraph") && File.ReadAllText("schema.dgraph", Encoding.UTF8) == schema)
                return sb;

            dgraph.Alter(schema).ConfigureAwait(false).GetAwaiter().GetResult();

            File.WriteAllText("schema.dgraph", schema, Encoding.UTF8);

            return sb;
        }

        public static string GetDType(this object entity)
        {
            var attr =
                entity.GetType().GetCustomAttributes()
                        .FirstOrDefault(dt => dt is DgraphTypeAttribute)
                    as DgraphTypeAttribute;

            return attr?.Name;
        }

        public static string NormalizeName(this string propName)
            => string.Concat(propName
                    .Select(c => char.IsUpper(c) ? $"_{char.ToLowerInvariant(c)}" : c.ToString()))
                .Trim('_');

        public static string[] GetSearchColumns(this object entity)
        {
            var types = new[] { entity.GetType() };

            var properties =
            types.SelectMany(type => type.GetProperties()
                .Where(prop => prop.GetCustomAttributes()
                    .Any(attr => attr is StringPredicateAttribute spa && spa.Fulltext)).Select(prop => (type, prop)));

            var triples =
            properties.Where(pp => !(pp.prop.DeclaringType is null) &&
                                     !typeof(IDictionary).IsAssignableFrom(pp.prop.PropertyType) &&
                                     !typeof(KeyValuePair).IsAssignableFrom(pp.prop.PropertyType) &&
                                     pp.prop.GetCustomAttributes()
                                         .Where(attr => attr is JsonPropertyAttribute)
                                         .OfType<JsonPropertyAttribute>()
                                         .All(jattr =>
                                             jattr.PropertyName?.Contains("|") != true))
                .Select(pp =>
                {
                    var (_, prop) = pp;
                    var jattr =
                        prop.GetCustomAttributes()
                            .Where(attr => attr is JsonPropertyAttribute)
                            .OfType<JsonPropertyAttribute>().FirstOrDefault() ??
                        new JsonPropertyAttribute(NormalizeName(prop.Name));

                    if (jattr.PropertyName == "dgraph.type" || jattr.PropertyName == "uid")
                        return null;

                    return jattr.PropertyName;
                }).Where(x => !(x is null));

            return triples.ToArray();
        }

        public static string GetColumns(this object entity)
        {
            var types = new[] { entity.GetType() };

            var properties =
            types.SelectMany(type => type.GetProperties()
                .Where(prop => prop.GetCustomAttributes()
                    .Any(attr => attr is StringPredicateAttribute ||
                                 attr is CommonPredicateAttribute ||
                                 attr is DateTimePredicateAttribute ||
                                 attr is PasswordPredicateAttribute ||
                                 attr is ReversePredicateAttribute)).Select(prop => (type, prop)));

            var triples =
            properties.Where(pp => !(pp.prop.DeclaringType is null) &&
                                     !typeof(IDictionary).IsAssignableFrom(pp.prop.PropertyType) &&
                                     !typeof(KeyValuePair).IsAssignableFrom(pp.prop.PropertyType) &&
                                     pp.prop.GetCustomAttributes()
                                         .Where(attr => attr is JsonPropertyAttribute)
                                         .OfType<JsonPropertyAttribute>()
                                         .All(jattr =>
                                             jattr.PropertyName?.Contains("|") != true))
                .Select(pp =>
                {
                    var (_, prop) = pp;
                    var jattr =
                        prop.GetCustomAttributes()
                            .Where(attr => attr is JsonPropertyAttribute)
                            .OfType<JsonPropertyAttribute>().FirstOrDefault() ??
                        new JsonPropertyAttribute(NormalizeName(prop.Name));

                    if (jattr.PropertyName == "dgraph.type" || jattr.PropertyName == "uid")
                        return null;

                    return jattr.PropertyName;
                }).Where(x => !(x is null));

            return string.Join('\n', triples);
        }

        public static string NormalizeColumnName<T>(this string column) where T : class, IEntity, new() =>
            new T().GetColumnName(column);

        public static string GetColumnName<T>(this T entity, string column) where T : class, IEntity
        {
            var types = new[] { entity.GetType() };

            var properties =
            types.SelectMany(type => type.GetProperties()
                .Where(prop => prop.GetCustomAttributes()
                    .Any(attr => attr is StringPredicateAttribute ||
                                 attr is CommonPredicateAttribute ||
                                 attr is DateTimePredicateAttribute ||
                                 attr is PasswordPredicateAttribute ||
                                 attr is ReversePredicateAttribute ||
                                 attr is JsonPropertyAttribute)).Select(prop => (type, prop)));

            var triples =
            properties.Where(pp => !(pp.prop.DeclaringType is null) &&
                                     !typeof(IDictionary).IsAssignableFrom(pp.prop.PropertyType) &&
                                     !typeof(KeyValuePair).IsAssignableFrom(pp.prop.PropertyType) &&
                                     pp.prop.GetCustomAttributes()
                                         .Where(attr => attr is JsonPropertyAttribute)
                                         .OfType<JsonPropertyAttribute>()
                                         .All(jattr =>
                                             jattr.PropertyName?.Contains("|") != true))
                .Select(pp =>
                {
                    var (_, prop) = pp;
                    var jattr =
                        prop.GetCustomAttributes()
                            .Where(attr => attr is JsonPropertyAttribute)
                            .OfType<JsonPropertyAttribute>().FirstOrDefault() ??
                        new JsonPropertyAttribute(NormalizeName(prop.Name));

                    if (jattr.PropertyName == "dgraph.type" || jattr.PropertyName == "uid")
                        return (null, null);

                    return (jattr.PropertyName, pp.prop.Name);
                }).Where(p => !(p.PropertyName is null) && !(p.Name is null) && (p.Name == column || p.PropertyName == column))
                .Select(p => p.PropertyName);

            var def = entity.GetType().GetProperty(column)?.GetCustomAttribute<JsonPropertyAttribute>();
            column = def?.PropertyName ?? column;

            return triples.FirstOrDefault() ?? column;
        }

        public static string GetColumnType<T>(this T entity, string column) where T : class, IEntity
        {
            var types = new[] { entity.GetType() };

            var properties =
            types.SelectMany(type => type.GetProperties()
                .Where(prop => prop.GetCustomAttributes()
                    .Any(attr => attr is StringPredicateAttribute ||
                                 attr is CommonPredicateAttribute ||
                                 attr is DateTimePredicateAttribute ||
                                 attr is PasswordPredicateAttribute ||
                                 attr is ReversePredicateAttribute)).Select(prop => (type, prop)));

            var triples =
            properties.Where(pp => !(pp.prop.DeclaringType is null) &&
                                     !typeof(IDictionary).IsAssignableFrom(pp.prop.PropertyType) &&
                                     !typeof(KeyValuePair).IsAssignableFrom(pp.prop.PropertyType) &&
                                     pp.prop.GetCustomAttributes()
                                         .Where(attr => attr is JsonPropertyAttribute)
                                         .OfType<JsonPropertyAttribute>()
                                         .All(jattr =>
                                             jattr.PropertyName?.Contains("|") != true))
                .Select(pp =>
                {
                    var (_, prop) = pp;
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
                        princType = prop.PropertyType;
                    }

                    var isUid = princType == typeof(Uid) ||
                                princType == typeof(Uid?) ||
                                pAttr is ReversePredicateAttribute;

                    var isString = princType.IsEnum ||
                                   princType == typeof(string) ||
                                   princType == typeof(char) ||
                                   princType == typeof(char[]) ||
                                   princType == typeof(char?) ||
                                   princType == typeof(TimeSpan) ||
                                   princType == typeof(TimeSpan?);

                    var isFloat = princType == typeof(float) ||
                                  princType == typeof(double) ||
                                  princType == typeof(decimal) ||
                                  princType == typeof(float?) ||
                                  princType == typeof(double?) ||
                                  princType == typeof(decimal?);

                    var isInt = princType == typeof(int) ||
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
                                princType == typeof(sbyte?);

                    var isBool = princType == typeof(bool) ||
                                 princType == typeof(bool?);

                    var isDate = princType == typeof(DateTime) ||
                                 princType == typeof(DateTimeOffset) ||
                                 princType == typeof(DateTime?) ||
                                 princType == typeof(DateTimeOffset?);

                    var isGeo = new[]
                    {
                        "Point", "LineString", "Polygon", "MultiPoint", "MultiLineString", "MultiPolygon",
                        "GeometryCollection"
                    }.Contains(princType.Name);

                    string propType;
                    if (isUid)
                    {
                        propType = "uid";
                    }
                    else if (isString)
                    {
                        propType = "string";
                    }
                    else if (isFloat)
                    {
                        propType = "float";
                    }
                    else if (isInt)
                    {
                        propType = "int";
                    }
                    else if (isBool)
                    {
                        propType = "bool";
                    }
                    else if (isDate)
                    {
                        propType = "datetime";
                    }
                    else if (isGeo)
                    {
                        propType = "geo";
                    }
                    else
                    {
                        propType = "default";
                    }

                    var isPasswd = pAttr is PasswordPredicateAttribute &&
                                   (propType == "string" || propType == "default");

                    if (isPasswd)
                        propType = "password";

                    return (propType, pp.prop);
                }).Where(p => !(p.propType is null) && !(p.prop?.Name is null) && p.prop.Name == column)
                .Select(p => p.propType);

            return triples.FirstOrDefault() ?? "default";
        }
    }
}
