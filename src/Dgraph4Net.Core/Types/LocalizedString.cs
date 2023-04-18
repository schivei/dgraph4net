using System.Globalization;

#nullable enable

namespace System;

public sealed class LocalizedString
{
    public CultureInfo CultureInfo { get; set; } = CultureInfo.CurrentUICulture;

    public string Value { get; set; } = default!;

    public string Key { get; set; } = default!;

    public bool NoCulture { get; set; }

    public string LocalizedKey
    {
        get => NoCulture ? Key : $"{Key}@{CultureInfo.Name}";
        set
        {
            if (value is null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            if (!value.Contains('@'))
            {
                Value = value;
                NoCulture = false;
            }
            else
            {
                Key = value.Split('@', StringSplitOptions.RemoveEmptyEntries)[0].Trim();
                var ci = value.Split('@', StringSplitOptions.RemoveEmptyEntries)[1].Trim();
                if (!string.IsNullOrEmpty(ci))
                {
                    CultureInfo = CultureInfo.GetCultureInfo(ci);
                }
            }
        }
    }
}
