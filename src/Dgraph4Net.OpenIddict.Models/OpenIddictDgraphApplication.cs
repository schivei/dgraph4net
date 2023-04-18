using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using Dgraph4Net.Annotations;

namespace Dgraph4Net.OpenIddict.Models;

/// <summary>
/// Represents an OpenIddict application.
/// </summary>
[DgraphType("OpenIddictApplication")]
public class OpenIddictDgraphApplication : AEntity<OpenIddictDgraphApplication>
{
    [JsonPropertyName("~oidc_app_auth"), ReversePredicate, PredicateReferencesTo(typeof(OpenIddictDgraphAuthorization))]
    public virtual List<OpenIddictDgraphAuthorization> Authorizations { get; set; } = new();

    [JsonPropertyName("~oidc_app_token"), ReversePredicate, PredicateReferencesTo(typeof(OpenIddictDgraphToken))]
    public virtual List<OpenIddictDgraphToken> Tokens { get; set; } = new();

    /// <summary>
    /// Gets or sets the client identifier associated with the current application.
    /// </summary>
    [JsonPropertyName("oidc_client_id"), StringPredicate(Token = StringToken.Exact)]
    public virtual string? ClientId { get; set; }

    /// <summary>
    /// Gets or sets the client secret associated with the current application.
    /// Note: depending on the application manager used to create this instance,
    /// this property may be hashed or encrypted for security reasons.
    /// </summary>
    [JsonPropertyName("oidc_client_secret"), StringPredicate(Token = StringToken.Hash)]
    public virtual string? ClientSecret { get; set; }

    /// <summary>
    /// Gets or sets the concurrency token.
    /// </summary>
    [JsonPropertyName("oidc_concurrency_token"), StringPredicate]
    public virtual Guid ConcurrencyToken { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Gets or sets the consent type associated with the current application.
    /// </summary>
    [JsonPropertyName("oidc_consent_type"), StringPredicate]
    public virtual string? ConsentType { get; set; }

    /// <summary>
    /// Gets or sets the display name associated with the current application.
    /// </summary>
    [JsonPropertyName("oidc_display_name"), StringPredicate]
    public virtual string? DisplayName { get; set; }

    [JsonPropertyName("oidc_supported_languages"), StringPredicate]
    public virtual List<string> SupportedLanguages { get; set; } = new();

    private readonly LocalizedStrings _displayNames = new();

    /// <summary>
    /// Gets or sets the localized display names associated with the current application.
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
    /// Gets or sets the permissions associated with the current application.
    /// </summary>
    [JsonPropertyName("oidc_permissions"), StringPredicate]
    public virtual List<string> Permissions { get; set; } = new();

    /// <summary>
    /// Gets or sets the logout callback URLs associated with the current application.
    /// </summary>
    [JsonPropertyName("oidc_post_logout_redirect_uris"), StringPredicate(Token = StringToken.Exact)]
    public virtual List<string> PostLogoutRedirectUris { get; set; } = new();

    /// <summary>
    /// Gets or sets the additional properties associated with the current application.
    /// </summary>
    [JsonPropertyName("oidc_properties"), CommonPredicate]
    public virtual byte[] Properties { get; set; } = Array.Empty<byte>();

    /// <summary>
    /// Gets or sets the callback URLs associated with the current application.
    /// </summary>
    [JsonPropertyName("oidc_redirect_uris"), StringPredicate]
    public virtual List<string> RedirectUris { get; set; } = new();

    /// <summary>
    /// Gets or sets the requirements associated with the current application.
    /// </summary>
    [JsonPropertyName("oidc_requirements"), StringPredicate]
    public virtual List<string> Requirements { get; set; } = new();

    /// <summary>
    /// Gets or sets the application type
    /// associated with the current application.
    /// </summary>
    [JsonPropertyName("oidc_type"), StringPredicate(Token = StringToken.Exact)]
    public virtual string? Type { get; set; }
}
