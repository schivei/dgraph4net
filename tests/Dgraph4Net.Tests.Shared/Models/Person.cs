using System;
using Dgraph4Net.ActiveRecords;
using NetGeo.Json;

namespace Dgraph4Net.Tests;

public class Person : AEntity<Person>
{
    public NameFacet Name { get; set; }

    public int Age { get; set; }

    public DateTimeOffset Dob { get; set; }

    public bool Married { get; set; }

    public byte[] Raw { get; set; }

    public Person[] Friends { get; set; }

    public Point Location { get; set; }

    public School[] Schools { get; set; }

    public DateTimeOffset Since { get; set; }

    public string Family { get; set; }

    public bool Close { get; set; }

    public Person()
    {
        Name = new(this, GetType().GetProperty(nameof(Name)));
    }
}
