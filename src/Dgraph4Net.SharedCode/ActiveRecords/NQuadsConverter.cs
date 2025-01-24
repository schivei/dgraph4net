using System.Collections;
using System.Data;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using Google.Protobuf;
using NetGeo.Json;

namespace Dgraph4Net.ActiveRecords;

internal class NQuadsConverter
{
    public static (ByteString, ByteString) ToNQuads([NotNull] object obj, bool dropNulls = false)
    {
        ArgumentNullException.ThrowIfNull(obj, nameof(obj));

        var set = new NQuadWriter();
        var del = new NQuadWriter(dropNulls);

        if (obj is IEntity entity)
        {
            ToNQuads(entity, set, del, dropNulls);

            return (ByteString.CopyFromUtf8(set ?? string.Empty), ByteString.CopyFromUtf8(del ?? string.Empty));
        }

        if (obj is IEnumerable<IEntity> entities)
        {
            foreach (var data in entities)
                ToNQuads(data, set, del, dropNulls);

            return (ByteString.CopyFromUtf8(set ?? string.Empty), ByteString.CopyFromUtf8(del ?? string.Empty));
        }

        throw new InvalidConstraintException("The object must be an IEntity or IEnumerable<IEntity>.");
    }

    private static void ToNQuads([NotNull] object obj, NQuadWriter set, NQuadWriter del, bool dropNulls)
    {
        if (obj is null)
            return;

        if (obj is not IEntity entity)
            return;

        if (dropNulls)
            del.WriteNQuads(entity);

        set.WriteNQuads(entity);
    }

    private class NQuadWriter(bool dropNulls = false) : StringWriter(new StringBuilder())
    {
        private readonly bool _dropNulls = dropNulls;

        public void WriteNQuads(IEntity entity)
        {
            if (!ClassMapping.ClassMappings.TryGetValue(entity.GetType(), out var map))
                return;

            var predicates = ClassMapping.GetPredicates(map.Type);

            var facets = entity.Facets;

            if (entity.Uid.IsEmpty)
                entity.Uid.Replace(Uid.NewUid());

            string eUid;
            if (entity.Uid.IsConcrete)
                eUid = $"<{entity.Uid}>";
            else
                eUid = entity.Uid.ToString();

            foreach (var predicate in predicates)
            {
                if (predicate is UidPredicate)
                    continue;

                var value = predicate.Property.GetValue(entity);

                if (_dropNulls)
                {
                    if (value is null)
                        WriteLine($"{eUid} <{predicate.PredicateName}> * .");

                    continue;
                }

                if (value is null)
                    continue;

                if (value is IEntity e)
                {
                    WriteReference(eUid, predicate.PredicateName, e);
                    continue;
                }

                if (value is IEnumerable<IEntity> entities)
                {
                    foreach (var ent in entities)
                    {
                        if (ent is null)
                            continue;

                        WriteReference(eUid, predicate.PredicateName, ent);
                    }

                    continue;
                }

                if (value is IEnumerable items and not string)
                {
                    if (predicate is TypePredicate)
                    {
                        var values = new HashSet<string>(items.Cast<string>())
                        {
                            map.DgraphType
                        };

                        foreach (var item in values)
                            WriteLine($"{eUid} <{predicate.PredicateName}> \"{item}\" .");
                    }
                    else if (value is IEnumerable<IFacetedValue> fv && fv.Any())
                        WriteFacetedValues(entity.Uid, predicate.PredicateName, fv);
                    else if (value is IEnumerable<byte> bytes && ClassMapping.ImplClassMapping.Serialize(Convert.ToBase64String(bytes.ToArray())) is string b64)
                        WriteLine($"{eUid} <{predicate.PredicateName}> {b64} .");
                    else
                    {
                        var values = items.Cast<object?>();

                        foreach (var item in values)
                        {
                            var pitem = ClassMapping.ImplClassMapping.Serialize(item);

                            WriteLine($"{eUid} <{predicate.PredicateName}> {pitem} .");
                        }
                    }

                    continue;
                }

                if (value is GeoObject geo)
                {
                    var geoj = ClassMapping.ImplClassMapping.Serialize(geo.ToGeoJson());

                    WriteLine($"{eUid} <{predicate.PredicateName}> {geoj} .");
                    continue;
                }

                var pfacets = facets.Where(x => x.Key.PredicateName == predicate.PredicateName);

                var faceted = new StringBuilder();

                if (pfacets.Any(x => !x.Key.IsI18N))
                {
                    faceted.Append(" (");

                    foreach (var facet in pfacets)
                    {
                        var fvalue = ClassMapping.ImplClassMapping.Serialize(facet.Value);

                        faceted.Append($"{facet.Key.FacetName}={fvalue}, ");
                    }

                    faceted.Length -= 2;
                    faceted.Append(')');
                }

                var asJson = ClassMapping.ImplClassMapping.Serialize(value);

                WriteLine($"{eUid} <{predicate.PredicateName}> {asJson}{faceted} .");

                if (pfacets.Any(x => x.Key.IsI18N))
                {
                    foreach (var facet in pfacets)
                    {
                        var fvalue = ClassMapping.ImplClassMapping.Serialize(facet.Value);

                        WriteLine($"{eUid} <{predicate.PredicateName}> {fvalue}@{facet.Key.FacetName} .");
                    }
                }
            }
        }

        private void WriteFacetedValues(Uid origin, string predicateName, IEnumerable<IFacetedValue> facetedValues)
        {
            string origUid;
            if (origin.IsConcrete)
                origUid = $"<{origin}>";
            else
                origUid = origin.ToString();

            foreach (var fv in facetedValues)
            {
                var value = fv.Value;

                if (value is null)
                    continue;

                var pvalue = ClassMapping.ImplClassMapping.Serialize(value);

                if (_dropNulls)
                {
                    if (fv.Drop)
                        WriteLine($"{origUid} <{predicateName}> {pvalue} .");

                    continue;
                }

                if (fv.Drop)
                    continue;

                var pfacets = fv.Facets;

                var faceted = new StringBuilder();

                if (pfacets.Any())
                {
                    faceted.Append(" (");

                    foreach (var facet in pfacets)
                    {
                        var fvalue = ClassMapping.ImplClassMapping.Serialize(facet.Value);

                        faceted.Append($"{facet.Key}={fvalue}, ");
                    }

                    faceted.Length -= 2;
                    faceted.Append(')');
                }

                WriteLine($"{origUid} <{predicateName}> {pvalue}{faceted} .");
            }
        }

        private void WriteReference(string origUid, string predicateName, IEntity reference)
        {
            WriteNQuads(reference);

            string refUid;
            if (reference.Uid.IsConcrete)
                refUid = $"<{reference.Uid}>";
            else
                refUid = reference.Uid.ToString();

            var facets = reference.Facets.Where(x => x.Key.PredicateName == predicateName && !x.Key.IsI18N);

            var faceted = new StringBuilder();

            if (facets.Any())
            {
                faceted.Append(" (");

                foreach (var facet in facets)
                {
                    var fvalue = ClassMapping.ImplClassMapping.Serialize(facet.Value);

                    faceted.Append($"{facet.Key.FacetName}={fvalue}, ");
                }

                faceted.Length -= 2;
                faceted.Append(')');
            }

            WriteLine($"{origUid} <{predicateName}> {refUid}{faceted} .");
        }

        public static implicit operator string(NQuadWriter writer) =>
            writer.ToString();
    }
}
