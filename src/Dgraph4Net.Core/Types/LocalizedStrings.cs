using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json.Serialization;

#nullable enable

namespace System;

[JsonConverter(typeof(LocalizedStringsConverter))]
public sealed class LocalizedStrings : List<LocalizedString>, ICollection<LocalizedString>
{
    public LocalizedStrings()
    {
    }

    public LocalizedStrings(IEnumerable<LocalizedString> collection) : base(collection)
    {
    }

    public LocalizedStrings(int capacity) : base(capacity)
    {
    }

    void ICollection<LocalizedString>.Add(LocalizedString item) =>
        Add(item);

    public new void AddRange(IEnumerable<LocalizedString> localizedStrings)
    {
        localizedStrings = localizedStrings.Where(ls => this.All(l => l.LocalizedKey != ls.LocalizedKey));

        if (localizedStrings.Any())
            base.AddRange(localizedStrings);
    }

    public new void Add(LocalizedString localizedString)
    {
        if (this.All(l => l.LocalizedKey != localizedString.LocalizedKey))
            base.Add(localizedString);
    }

    public Dictionary<CultureInfo, string> ToDictionary()
    {
        return this.ToDictionary(k => k.CultureInfo, v => v.Value);
    }

    public void SetPredicate(string name)
    {
        this.AsParallel().ForAll(x => x.Key = name);
    }

    public static implicit operator Dictionary<CultureInfo, string>(LocalizedStrings lss)
    {
        return lss.ToDictionary(k => k.CultureInfo, v => v.Value);
    }

    public static implicit operator LocalizedStrings(Dictionary<CultureInfo, string> dict)
    {
        return new(dict.Select(d => new LocalizedString
        {
            CultureInfo = d.Key,
            Value = d.Value
        }));
    }
}
