using System;
using Dgraph4Net.ActiveRecords;

namespace Dgraph4Net.Tests;

public class School : AEntity<School>
{
    public string Name { get; set; }

    [Facet<Person>("since", nameof(Person.Schools))]
    public DateTime Since { get; set; }
}
