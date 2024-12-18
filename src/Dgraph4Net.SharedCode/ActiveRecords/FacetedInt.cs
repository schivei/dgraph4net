namespace Dgraph4Net.ActiveRecords;

public class FacetedInt : FacetedValue<int>
{
    public static implicit operator FacetedInt(int value) =>
        new() { Value = value };

    public static implicit operator int(FacetedInt value) =>
        value.Value;
}
