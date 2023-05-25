using System;
using System.Collections.Generic;
using Dgraph4Net.Annotations;

namespace Dgraph4Net.ActiveRecords;

public readonly record struct DateTimePredicate(IClassMap ClassMap, string PredicateName, DateTimeToken Token = DateTimeToken.None, bool Upsert = false) : IPredicate
{
    public ISet<IFacet> Facets { get; } = new HashSet<IFacet>();

    readonly string IPredicate.ToSchemaPredicate() =>
        $"{PredicateName}: dateTime {(Token != DateTimeToken.None ? $"@index({Token.ToString().ToLowerInvariant()})" : "")} {(Upsert ? "@upsert" : "")} .";

    readonly string IPredicate.ToTypePredicate() =>
        PredicateName;

    public static DateTimePredicate operator |(DateTimePredicate lpa1, DateTimePredicate lpa2) =>
        lpa1.Merge(lpa2);

    public DateTimePredicate Merge(DateTimePredicate lpa) =>
        new(ClassMap, PredicateName, (DateTimeToken)Math.Max((int)Token, (int)lpa.Token), Upsert || lpa.Upsert);

    public IPredicate Merge(IPredicate p2) =>
        p2 switch
        {
            DateTimePredicate p => Merge(p),
            _ => ((IPredicate)this).ToSchemaPredicate().StartsWith(':') ? p2 : this
        };
}
