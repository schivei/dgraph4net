using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime;
using System.Text;
using System.Text.RegularExpressions;

using Dgraph4Net.Annotations;

using GeoJSON.Net.Geometry;

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;

namespace Dgraph4Net
{
    public static class DgraphExtensions
    {
        private static void WalkNode(JToken node, Action<JObject> action)
        {
            if (node.Type == JTokenType.Object)
            {
                action((JObject)node);

                foreach (JProperty child in node.Children<JProperty>())
                {
                    WalkNode(child.Value, action);
                }
            }
            else if (node.Type == JTokenType.Array)
            {
                foreach (JToken child in node.Children())
                {
                    WalkNode(child, action);
                }
            }
        }

        private static readonly Dictionary<Type, CustomTypeMetadata> s_customTypes = new();

        public static void AddCustomType(this Type userType, params Attribute[] Attributes)
        {
            var attributes = Attributes.Where(attr => (attr is StringPredicateAttribute ||
                                 attr is CommonPredicateAttribute ||
                                 attr is DateTimePredicateAttribute ||
                                 attr is PasswordPredicateAttribute ||
                                 attr is ReversePredicateAttribute ||
                                 attr is GeoPredicateAttribute ||
                                 attr is DgraphTypeAttribute ||
                                 (attr is StringLanguageAttribute && typeof(IEnumerable<LocalizedString>).IsAssignableFrom(userType))) &&
                                 !(attr is IgnoreMappingAttribute ||
                                 attr is JsonIgnoreAttribute)).ToArray();

            if (attributes.Length == 0)
            {
                throw new ArgumentException("The parameter '{0}' has no valid attribute to map user types.");
            }

            var meta = new CustomTypeMetadata(attributes);

            if (s_customTypes.ContainsKey(userType))
            {
                s_customTypes[userType] = meta;
            }
            else
            {
                s_customTypes.Add(userType, meta);
            }
        }

        public static bool HasCustomType(this Type type) =>
            s_customTypes.ContainsKey(type);

        public static IEnumerable<Attribute> GetTypeAttributes(this Type type)
        {
            var attrs = new HashSet<Attribute>();
            if (HasCustomType(type))
            {
                s_customTypes[type].Attributes.ToList()
                    .ForEach(a => attrs.Add(a));
            }

            if (attrs.Count == 0)
            {
                type.GetCustomAttributes().ToList()
                    .ForEach(a => attrs.Add(a));
            }
            else
            {
                foreach (var attr in type.GetCustomAttributes())
                {
                    var pre = attrs.FirstOrDefault(x => x.GetType() == attr.GetType());
                    if (pre is null)
                    {
                        attrs.Add(attr);
                    }
                    else
                    {
                        switch (pre)
                        {
                            case StringLanguageAttribute sla when typeof(IEnumerable<LocalizedString>).IsAssignableFrom(type):
                                sla |= attr as StringLanguageAttribute;
                                break;
                            case StringPredicateAttribute spa:
                                spa |= attr as StringPredicateAttribute;
                                break;
                            case CommonPredicateAttribute cpa:
                                cpa |= attr as CommonPredicateAttribute;
                                break;
                            case DateTimePredicateAttribute dtpa:
                                dtpa |= attr as DateTimePredicateAttribute;
                                break;
                            case GeoPredicateAttribute spa:
                                spa |= attr as GeoPredicateAttribute;
                                break;
                            case PasswordPredicateAttribute:
                            case ReversePredicateAttribute:
                            case IgnoreMappingAttribute:
                            case JsonIgnoreAttribute:
                                break;
                            default:
                                attrs.Add(attr);
                                break;
                        }
                    }
                }
            }

            if (type.BaseType is not null)
            {
                var baseAttrs = GetTypeAttributes(type.BaseType);

                foreach (var attr in baseAttrs)
                {
                    var pre = attrs.FirstOrDefault(x => x.GetType() == attr.GetType());
                    if (pre is null)
                    {
                        attrs.Add(attr);
                    }
                    else
                    {
                        switch (pre)
                        {
                            case StringLanguageAttribute sla when typeof(IEnumerable<LocalizedString>).IsAssignableFrom(type.BaseType):
                                sla |= attr as StringLanguageAttribute;
                                break;
                            case StringPredicateAttribute spa:
                                spa |= attr as StringPredicateAttribute;
                                break;
                            case CommonPredicateAttribute cpa:
                                cpa |= attr as CommonPredicateAttribute;
                                break;
                            case DateTimePredicateAttribute dtpa:
                                dtpa |= attr as DateTimePredicateAttribute;
                                break;
                            case GeoPredicateAttribute spa:
                                spa |= attr as GeoPredicateAttribute;
                                break;
                            case PasswordPredicateAttribute:
                            case ReversePredicateAttribute:
                            case IgnoreMappingAttribute:
                            case JsonIgnoreAttribute:
                                break;
                            default:
                                attrs.Add(attr);
                                break;
                        }
                    }
                }
            }

            return attrs.Distinct();
        }

        public static IEnumerable<Attribute> GetPropertyAttributes(this PropertyInfo prop)
        {
            var attrs = new HashSet<Attribute>();
            if (HasCustomType(prop.PropertyType))
            {
                GetTypeAttributes(prop.PropertyType).ToList()
                    .ForEach(a => attrs.Add(a));
            }

            if (attrs.Count == 0)
            {
                prop.GetCustomAttributes().ToList()
                    .ForEach(a => attrs.Add(a));
            }
            else
            {
                foreach (var attr in GetPropertyAttributes(prop))
                {
                    var pre = attrs.FirstOrDefault(x => x.GetType() == attr.GetType());
                    if (pre is null)
                    {
                        attrs.Add(attr);
                    }
                    else
                    {
                        switch (pre)
                        {
                            case StringLanguageAttribute sla when typeof(IEnumerable<LocalizedString>).IsAssignableFrom(prop.PropertyType):
                                sla |= attr as StringLanguageAttribute;
                                break;
                            case StringPredicateAttribute spa:
                                spa |= attr as StringPredicateAttribute;
                                break;
                            case CommonPredicateAttribute cpa:
                                cpa |= attr as CommonPredicateAttribute;
                                break;
                            case DateTimePredicateAttribute dtpa:
                                dtpa |= attr as DateTimePredicateAttribute;
                                break;
                            case GeoPredicateAttribute spa:
                                spa |= attr as GeoPredicateAttribute;
                                break;
                            case PasswordPredicateAttribute:
                            case ReversePredicateAttribute:
                            case IgnoreMappingAttribute:
                            case JsonIgnoreAttribute:
                                break;
                            default:
                                attrs.Add(attr);
                                break;
                        }
                    }
                }
            }

            return attrs.Distinct();
        }

        public static T GetPropertyAttribute<T>(this PropertyInfo prop) where T : Attribute
        {
            if (prop is null)
                return null;

            var attrs = GetPropertyAttributes(prop);

            return attrs.FirstOrDefault(x => x is T) as T ?? prop.GetCustomAttribute<T>();
        }

        public static T GetTypeAttribute<T>(this Type type) where T : Attribute
        {
            var attrs = GetTypeAttributes(type);

            return attrs.FirstOrDefault(x => x is T) as T ?? type.GetCustomAttribute<T>();
        }

        private static readonly ConcurrentBag<TypeMetadata> s_mappings = new();

        /// <summary>
        /// Generates mapping
        /// </summary>
        public static StringBuilder Map(this IDgraph4NetClient dgraph, params Assembly[] assemblies)
        {
            var types = assemblies
                .SelectMany(assembly => assembly.GetTypes()
                    .Where(t => GetTypeAttributes(t)
                        .Any(att => att is DgraphTypeAttribute)))
                .OrderBy(t => t.GetProperties()
                    .Any(pi => GetPropertyAttributes(pi)
                        .Any(a => a is PredicateReferencesToAttribute)));

            var properties =
            types.SelectMany(type => type.GetProperties()
                .Where(prop => GetPropertyAttributes(prop)
                    .Any(attr => (attr is StringPredicateAttribute ||
                                 attr is CommonPredicateAttribute ||
                                 attr is DateTimePredicateAttribute ||
                                 attr is PasswordPredicateAttribute ||
                                 attr is ReversePredicateAttribute ||
                                 attr is GeoPredicateAttribute ||
                                 (attr is StringLanguageAttribute && typeof(IEnumerable<LocalizedString>).IsAssignableFrom(prop.PropertyType))) &&
                                 !(attr is IgnoreMappingAttribute ||
                                 attr is JsonIgnoreAttribute))
                    ).Select(prop => (type, prop)));

            var triples =
            properties.Where(pp => pp.prop.DeclaringType is not null &&
                                     !typeof(IDictionary).IsAssignableFrom(pp.prop.PropertyType) &&
                                     !typeof(KeyValuePair).IsAssignableFrom(pp.prop.PropertyType) &&
                                     GetPropertyAttributes(pp.prop)
                                         .Where(attr => attr is JsonPropertyAttribute)
                                         .OfType<JsonPropertyAttribute>()
                                         .All(jattr =>
                                             jattr.PropertyName?.Contains("|") != true))
                .Select(pp =>
                {
                    var (type, prop) = pp;
                    var jattr =
                        GetPropertyAttributes(prop)
                            .Where(attr => attr is JsonPropertyAttribute)
                            .OfType<JsonPropertyAttribute>().FirstOrDefault() ??
                        new JsonPropertyAttribute(prop.Name.NormalizeName());

                    if (jattr.PropertyName == "dgraph.type" || jattr.PropertyName == "uid")
                        return (null, null, null, null, null, null, false);

                    var pAttr = GetPropertyAttributes(prop)
                                    .FirstOrDefault(attr => attr is StringPredicateAttribute ||
                                                            attr is CommonPredicateAttribute ||
                                                            attr is DateTimePredicateAttribute ||
                                                            attr is PasswordPredicateAttribute ||
                                                            attr is ReversePredicateAttribute ||
                                                            attr is GeoPredicateAttribute ||
                                                            (attr is StringLanguageAttribute && typeof(IEnumerable<LocalizedString>).IsAssignableFrom(prop.PropertyType))) ??
                                new CommonPredicateAttribute();

                    var princType = prop.PropertyType.IsGenericType
                        ? prop.PropertyType.GetGenericArguments().First()
                        : prop.PropertyType;

                    var isList = prop.PropertyType != typeof(string) &&
                                 typeof(IEnumerable).IsAssignableFrom(prop.PropertyType) &&
                                 !typeof(IEnumerable<LocalizedString>).IsAssignableFrom(prop.PropertyType);

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
                                   princType == typeof(TimeSpan?) ||
                                   pAttr is StringLanguageAttribute;

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

                    var isGeo = pAttr is GeoPredicateAttribute && new[]
                    {
                        nameof(Point), nameof(LineString), nameof(Polygon), nameof(MultiPoint),
                        nameof(MultiLineString), nameof(MultiPolygon), nameof(GeometryCollection)
                    }.Contains(princType.Name);

                    var isEnum = princType.IsEnum;

                    string propType;
                    if (isEnum)
                    {
                        var converter = GetTypeAttribute<JsonConverterAttribute>(princType);
                        if (converter != null && typeof(StringEnumConverter).IsAssignableFrom(converter.ConverterType))
                            propType = "string";
                        else
                            propType = "int";
                    }
                    else if (isUid)
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

                    propType = pAttr switch
                    {
                        StringLanguageAttribute or StringPredicateAttribute => "string",
                        DateTimePredicateAttribute => "datetime",
                        _ => propType
                    };

                    var predicate = $"<{jattr.PropertyName}>: ";
                    predicate += isList ? "[{0}]" : "{0}";

                    var isReverse = pAttr is ReversePredicateAttribute;

                    var slAttr = pAttr as StringLanguageAttribute;
                    var isLang = slAttr is not null && !isList;

                    if (isLang)
                    {
                        pAttr = new StringPredicateAttribute
                        {
                            Fulltext = slAttr.Fulltext,
                            Token = slAttr.Token,
                            Trigram = slAttr.Trigram,
                            Upsert = slAttr.Upsert
                        };
                    }

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

                                predicate += isLang ? ") @lang" : ")";
                                predicate += sa.Upsert ? " @upsert" : "";
                            }
                            else
                            {
                                predicate += isLang ? " @lang" : "";
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

                        case GeoPredicateAttribute ga:
                            predicate = string.Format(predicate, propType);
                            if (ga.Upsert || ga.Index)
                            {
                                predicate += $" @index({propType})";
                                predicate += ga.Upsert ? " @upsert" : "";
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

                    var isReference = isReverse || isUid || GetPropertyAttribute<PredicateReferencesToAttribute>(prop) is not null;

                    return (jattr.PropertyName, predicate, type, GetTypeAttribute<DgraphTypeAttribute>(type).Name, propType, prop, isReference);
                }).Where(x => !(x.PropertyName is null)).ToArray();

            var ambiguous = triples.GroupBy(x => x.PropertyName)
                .Where(x => x.Select(y => y.predicate).Distinct().Count() > 1);

            var imps = ambiguous.SelectMany(s => s.Select(p => (p.prop.Name, p.prop.DeclaringType?.Name))).ToArray();

            if (imps.Length > 1)
            {
                throw new AmbiguousImplementationException($@"There are two or more different implementations of: {string.Join(',', imps.Select(i => i.Item1).Distinct())
                    .Trim(',').Replace(",", ", ")}.\n\nImplementations: \n{string.Join("\n", imps.Select(i => $"{i.Item2}::{i.Item1}"))}");
            }

            var sb = new StringBuilder();

            foreach (var predicate in triples.OrderBy(p => p.PropertyName).GroupBy(tp => tp.predicate)
                .Select(tp => tp.Key).Distinct())
                sb.AppendLine(predicate);

            sb.AppendLine();

            var ts = triples.OrderBy(p => p.Name).GroupBy(triple => triple.Name)
                .Select(tp =>
                {
                    var typename = tp.Key;
                    var typeProperties = tp.Select(pred =>
                    {
                        var (propertyName, predicate, _, _, _, prop, _) = pred;
                        var nm = predicate.Contains("@reverse") ? $"<~{propertyName}>" : $"<{propertyName}>";

                        var r = GetPropertyAttribute<PredicateReferencesToAttribute>(prop);

                        if (r is null)
                            return $"{nm}: {pred.propType}";

                        var refType = ClassFactory.GetDerivedType(r.RefType) ?? r.RefType;

                        var tn = GetTypeAttribute<DgraphTypeAttribute>(refType).Name;

                        if (predicate.Contains("["))
                            tn = $"[{tn}]";

                        return $"{nm}: {tn}";
                    }).GroupBy(x => x).Select(x => x.Key);

                    var names = string.Join("\n", typeProperties
                        .Select(s => $"  {s}"));

                    return $@"type {typename} {{
{names}
}}";
                });

            foreach (var type in ts)
                sb.Append(type).Append('\n');

            var schema = sb.ToString().Replace("\r\n", "\n").Trim('\n') + '\n';

            Map(triples);

            if (File.Exists("schema.dgraph") && File.ReadAllText("schema.dgraph", Encoding.UTF8) == schema)
                return sb;

            File.WriteAllText("schema.dgraph", schema, Encoding.UTF8);

            try
            {
                dgraph.Alter(schema).ConfigureAwait(false).GetAwaiter().GetResult();
            }
            catch
            {
                throw;
            }

            return sb;
        }

        private static void Map((string PropertyName, string predicate, Type type, string Name, string propType, PropertyInfo prop, bool isReference)[] triples)
        {
            triples.GroupBy(x => (x.type, x.Name)).AsParallel()
                .ForAll(typeMetadata =>
                {
                    var (type, Name) = typeMetadata.Key;
                    var mapping = s_mappings.FirstOrDefault(x => x.Type == type);

                    if (mapping is null)
                    {
                        mapping = new(type, Name, new());
                        s_mappings.Add(mapping);
                    }

                    typeMetadata.AsParallel()
                        .ForAll(info =>
                        {
                            var (PropertyName, predicate, _, _, propType, prop, isReference) = info;

                            var predicateMetadata = mapping.Predicates.FirstOrDefault(x => x.Property == prop);

                            if (predicateMetadata is null)
                            {
                                predicateMetadata = new(prop, PropertyName, propType, predicate, isReference, predicate.Contains("@reverse"));
                                mapping.Predicates.Add(predicateMetadata);
                            }
                        });
                });
        }

        public static string GetDType<T>(this T _) where T : IEntity
        {
            var t = typeof(T);

            var type = s_mappings.FirstOrDefault(x => IsMapped(x.Type, t));

            return type?.TypeName;
        }

        public static string NormalizeName(this string propName)
            => string.Concat(propName
                    .Select(c => char.IsUpper(c) ? $"_{char.ToLowerInvariant(c)}" : c.ToString()))
                .Trim('_');

        public static string[] GetSearchColumns<T>(this T _) where T : class, IEntity, new()
        {
            var t = typeof(T);

            var type = s_mappings.FirstOrDefault(x => IsMapped(x.Type, t));

            if (type is null)
                return Array.Empty<string>();

            return type.Predicates.Where(x => x.Property.GetPropertyAttribute<StringPredicateAttribute>() is StringPredicateAttribute spa
                && spa.Fulltext)
                .Select(x => x.PredicateName).ToArray();
        }

        public static T To<T>(this Type type) where T : class, IEntity, new()
        {
            return (T)Activator.CreateInstance(type);
        }

        public static T As<T>(this object obj) where T : class, IEntity, new()
        {
            if (obj is null)
                return default;

            return (T)obj;
        }

        public static string GetColumns<T>(this T _, Func<Type, Type, string> expand = null) where T : class, IEntity, new()
        {
            var t = typeof(T);

            var type = s_mappings.FirstOrDefault(x => IsMapped(x.Type, t));

            if (type is null)
                return null;

            var sb = new StringBuilder();

            foreach (var predicate in type.Predicates)
            {
                var pt = predicate.Property.PropertyType;
                var rev = predicate.isReversed && typeof(IEnumerable).IsAssignableFrom(pt) ? "~" : "";
                var pred = $"{rev}{predicate.PredicateName}";
                sb.Append(pred);
                if (predicate.isReversed || predicate.IsReference)
                {
                    sb.AppendLine("{");

                    var expanded = false;

                    if (expand is not null && pt != typeof(Uid))
                    {
                        if (typeof(IEnumerable).IsAssignableFrom(pt) && pt.IsGenericType && pt.GenericTypeArguments.Length == 1)
                        {
                            pt = pt.GenericTypeArguments.First();
                        }

                        if (typeof(IEntity).IsAssignableFrom(pt))
                        {
                            var content = expand(t, pt);

                            if (!string.IsNullOrEmpty(content?.Trim().Trim('\n')))
                            {
                                content = Regex.Replace(content, "^", "  ");
                                sb.AppendLine(content);
                                expanded = true;
                            }
                        }
                    }

                    if (!expanded)
                    {
                        sb.AppendLine("\tuid");

                        if (predicate.Property.PropertyType != typeof(Uid))
                            sb.AppendLine("\tdgraph.type");
                    }

                    sb.AppendLine("}");
                }
                else
                {
                    if (pt == typeof(LocalizedStrings))
                    {
                        sb.Append("@*");
                    }

                    if (predicate.Property.GetPropertyAttribute<HasFacetsAttribute>() is not null)
                    {
                        sb.Append(" @facets");
                    }

                    sb.AppendLine();
                }
            }

            return sb.Replace("\t", "  ").ToString().Replace("\r\n", "\n").Replace("\r", "\n");
        }

        public static string GetColumnName<T, TE>(this T entity, Expression<Func<T, TE>> _, Expression<Func<TE, object>> expression) where T : class, IEntity, new()
            where TE : class, IEntity, new()
        {
            MemberExpression memberExpr = null;
            switch (expression.Body.NodeType)
            {
                case ExpressionType.Convert:
                    memberExpr =
                        ((UnaryExpression)expression.Body).Operand as MemberExpression;
                    break;
                case ExpressionType.MemberAccess:
                    memberExpr = expression.Body as MemberExpression;
                    break;
            }

            if (memberExpr is null)
                return null;

            var mType = memberExpr.Member.DeclaringType;
            var mName = memberExpr.Member.Name;

            if (typeof(T) == typeof(TE))
                return entity.GetColumnName(mName);

            return mType.To<TE>().GetColumnName(mName);
        }

        public static string GetColumnName<T>(this T entity, Expression<Func<T, object>> expression) where T : class, IEntity, new() =>
            entity.GetColumnName(_ => entity, expression);

        public static string GetColumnName<T>(this T _, string column) where T : class, IEntity
        {
            if (column == nameof(IEntity.DgraphType) || column == "dgraph.type")
                return "dgrapth.type";

            if (column == nameof(IEntity.Id) || column == "uid")
                return "uid";

            var type = typeof(T);

            return s_mappings.SelectMany(x => x.Predicates.Select(y => (x.Type, y)))
                .Where(x => IsMapped(x.Type, type) && (x.y.PredicateName == column || x.y.Property.Name == column))
                .Select(x => x.y.PredicateName).FirstOrDefault();
        }

        private static bool IsMapped(Type meta, Type search)
        {
            if (meta == search)
                return true;

            return meta.IsAssignableFrom(search) || meta.IsAssignableFrom(search);
        }

        public static string GetColumnType<T>(this T _, string column) where T : class, IEntity
        {
            var type = typeof(T);

            return s_mappings.SelectMany(x => x.Predicates.Select(y => (x.Type, y)))
                .Where(x => IsMapped(x.Type, type) && (x.y.PredicateName == column || x.y.Property.Name == column))
                .Select(x => x.y.PredicateType).FirstOrDefault() ?? "default";
        }

        private record TypeMetadata(Type Type, string TypeName, ConcurrentBag<PredicateMetadata> Predicates);

        private record PredicateMetadata(PropertyInfo Property, string PredicateName, string PredicateType, string Predicate, bool IsReference, bool isReversed);
    }
}
