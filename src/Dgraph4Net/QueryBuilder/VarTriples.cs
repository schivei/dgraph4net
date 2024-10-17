namespace Dgraph4Net;

public class VarTriples : List<VarTriple>
{
    public VarTriple this[string originName]
    {
        get => this.FirstOrDefault(x => x.OriginName == originName);
        set => this[FindIndex(x => x.OriginName == originName)] = value;
    }

    public Dictionary<string, string> ToDictionary() =>
        new(this.Select(x => x.ToKeyValuePair()));
}
