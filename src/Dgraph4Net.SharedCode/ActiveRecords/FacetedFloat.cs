namespace Dgraph4Net.ActiveRecords;

public class FacetedFloat : FacetedValue<float>
{
    public static implicit operator FacetedFloat(float value) =>
        new() { Value = value };

    public static implicit operator float(FacetedFloat value) =>
        value.Value;
}
