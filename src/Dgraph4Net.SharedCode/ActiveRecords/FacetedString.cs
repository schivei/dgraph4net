namespace Dgraph4Net.ActiveRecords;

public class FacetedString : FacetedValue<string>
{
    public static implicit operator FacetedString(string value) =>
        new() { Value = value };

    public static implicit operator string(FacetedString value) =>
        value.Value ?? string.Empty;
}
