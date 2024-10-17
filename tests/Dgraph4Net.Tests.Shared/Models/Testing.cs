using System.Collections.Generic;
using Dgraph4Net.ActiveRecords;

namespace Dgraph4Net.Tests;

public sealed class Testing : AEntity<Testing>
{
    public string Name { get; set; }
    public Testing? Test { get; set; }
    public List<FacetedString> Ways { get; set; } = [];
}
