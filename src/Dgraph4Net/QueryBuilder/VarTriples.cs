namespace Dgraph4Net;

public class VarTriples : List<VarTriple>
{
    public Dictionary<string, string> ToDictionary() =>
        new(this.Select(x => x.ToKeyValuePair()));
}
