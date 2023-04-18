using System;
using System.Collections.Generic;

using Dgraph4Net.Annotations;

using System.Text.Json.Serialization;

namespace Dgraph4Net.OpenIddict.Models;

/// <summary>
/// Represents an OpenIddict scope.
/// </summary>
public class OpenIddictDgraphScope : AEntity<OpenIddictDgraphScope>
{
    /// <summary>
    /// Gets or sets the concurrency token.
    /// </summary>
    [JsonPropertyName("oidc_concurrency_token"), StringPredicate]
    public virtual Guid ConcurrencyToken { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Gets or sets the public description associated with the current scope.
    /// </summary>
    [JsonPropertyName("oidc_description"), StringPredicate]
    public virtual string? Description { get; set; }

    private readonly LocalizedStrings _descriptions = new();

    /// <summary>
    /// Gets or sets the localized public descriptions associated with the current scope.
    /// </summary>
    [JsonPropertyName("oidc_descriptions"), StringLanguage]
    public virtual LocalizedStrings Descriptions
    {
        get
        {
            if (_descriptions.Count == 0)
            {
                PopulateLocalized();
            }

            return _descriptions;
        }
        set
        {
            _descriptions.AddRange(value);
        }
    }

    /// <summary>
    /// Gets or sets the display name associated with the current scope.
    /// </summary>
    [JsonPropertyName("oidc_display_name"), StringPredicate]
    public virtual string? DisplayName { get; set; }

    private readonly LocalizedStrings _displayNames = new();

    /// <summary>
    /// Gets or sets the localized display names associated with the current scope.
    /// </summary>
    [JsonPropertyName("oidc_display_names"), StringLanguage(Token = StringToken.Exact)]
    public virtual LocalizedStrings DisplayNames
    {
        get
        {
            if (_displayNames.Count == 0)
            {
                PopulateLocalized();
            }

            return _displayNames;
        }
        set
        {
            _displayNames.AddRange(value);
        }
    }

    /// <summary>
    /// Gets or sets the unique name associated with the current scope.
    /// </summary>
    [JsonPropertyName("oidc_name"), StringPredicate]
    public virtual string? Name { get; set; }

    /// <summary>
    /// Gets or sets the additional properties associated with the current scope.
    /// </summary>
    [JsonPropertyName("oidc_properties"), CommonPredicate]
    public virtual byte[] Properties { get; set; } = new byte[0];

    /// <summary>
    /// Gets or sets the resources associated with the current scope.
    /// </summary>
    [JsonPropertyName("oidc_resources"), StringPredicate]
    public virtual List<string> Resources { get; set; } = new();
}
