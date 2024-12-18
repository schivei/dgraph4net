namespace Dgraph4Net.ActiveRecords;

public abstract class FacetedValue<T> : IFacetedValue<T>
{
    public Type ValueType { get; } = typeof(T);

    public IDictionary<string, object?> Facets { get; }

    public T? Value { get; set; }

    object? IFacetedValue.Value
    {
        get => Value;
        set => Value = (T)value!;
    }

    public bool Drop { get; set; }

    public FacetedValue() =>
        Facets = new Dictionary<string, object?>();

    public TR? GetFacet<TR>(string name, TR defaultValue = default) where TR : notnull, IComparable, IComparable<TR>, IEquatable<TR> =>
        Facets.TryGetValue(name, out var value) && value is TR val ? val : defaultValue;

    object? IFacetedValue.GetFacet(string name, object? defaultValue) =>
        Facets.TryGetValue(name, out var value) ? value : defaultValue;

    public void SetFacet<TR>(string facet, TR? value) where TR : notnull, IComparable, IComparable<TR>, IEquatable<TR> =>
        Facets[facet] = value;

    void IFacetedValue.SetFacet(string facet, object? value)
    {
        if (value is null)
            return;

        Facets[facet] = value;
    }
}
