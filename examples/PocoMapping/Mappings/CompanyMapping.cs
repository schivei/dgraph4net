using Dgraph4Net.ActiveRecords;
using PocoMapping.Entities;

namespace PocoMapping.Mappings;

internal sealed class CompanyMapping : ClassMap<Company>
{
    protected override void Map()
    {
        SetType("Company");
        String(x => x.Name, "name");
        String<CompanyIndustry>(x => x.Industry, "industry");
        HasMany(x => x.WorksHere, "works_for", x => x.WorksFor);
    }
}
