using System.Collections.Generic;
using System.Numerics;

using Dgraph4Net.ActiveRecords;

using NetGeo.Json;

namespace Dgraph4Net.Tests;

public sealed class Testing : AEntity<Testing>
{
    public string Name { get; set; }
    public Testing? Test { get; set; }
    public List<FacetedString> Ways { get; set; } = [];
}

public sealed class Testing2 : AEntity<Testing2>
{
    public Vector<float> Vector { get; set; }
    public MultiPoint MultiPoint { get; set; }
}
