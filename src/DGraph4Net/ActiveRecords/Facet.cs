namespace Dgraph4Net.ActiveRecords;

public readonly record struct Facet(string PredicateName, string FacetName) : IFacet;
