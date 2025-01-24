using Dgraph4Net.ActiveRecords;

namespace Dgraph4Net.Tests;

public class PersonMap : ClassMap<Person>
{
    protected override void Map()
    {
        SetType("Person");
        String(x => x.Name, "name");
        Integer(x => x.Age, "age");
        DateTime(x => x.Dob, "dob");
        Boolean(x => x.Married, "married");
        String(x => x.Raw, "raw_bytes");
        HasMany(x => x.Friends, "friend");
        Geo(x => x.Location, "loc");
        HasMany(x => x.Schools, "school");
        DateTime(x => x.Since, "since");
        String(x => x.Family, "family");
        Boolean(x => x.Close, "close");
    }
}
