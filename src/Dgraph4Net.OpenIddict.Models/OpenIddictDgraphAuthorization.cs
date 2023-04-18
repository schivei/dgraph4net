using System;
using System.Collections.Generic;

using Dgraph4Net.Annotations;

using System.Text.Json.Serialization;

namespace Dgraph4Net.OpenIddict.Models;

/// <summary>
/// Represents an OpenIddict authorization.
/// </summary>
public class OpenIddictDgraphAuthorization : AEntity<OpenIddictDgraphAuthorization>
{
    [JsonPropertyName("~oidc_auth_token"), ReversePredicate, PredicateReferencesTo(typeof(OpenIddictDgraphToken))]
    public virtual List<OpenIddictDgraphToken> Tokens { get; set; } = new();

    // <summary>
    /// Gets or sets the identifier of the application associated with the current authorization.
    /// </summary>
    [JsonPropertyName("oidc_app_auth"), ReversePredicate, PredicateReferencesTo(typeof(OpenIddictDgraphApplication))]
    public virtual Uid ApplicationId { get; set; } = default!;

    /// <summary>
    /// Gets or sets the concurrency token.
    /// </summary>
    [JsonPropertyName("oidc_concurrency_token"), StringPredicate]
    public virtual Guid ConcurrencyToken { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Gets or sets the UTC creation date of the current authorization.
    /// </summary>
    [JsonPropertyName("oidc_creation_date"), DateTimePredicate(Token = DateTimeToken.Hour)]
    public virtual DateTime? CreationDate { get; set; }

    /// <summary>
    /// Gets or sets the additional properties associated with the current authorization.
    /// </summary>
    [JsonPropertyName("oidc_properties"), CommonPredicate]
    public virtual byte[] Properties { get; set; } = new byte[0];

    /// <summary>
    /// Gets or sets the scopes associated with the current authorization.
    /// </summary>
    [JsonPropertyName("oidc_scopes"), StringPredicate(Token = StringToken.Exact)]
    public virtual List<string> Scopes { get; set; } = new();

    /// <summary>
    /// Gets or sets the status of the current authorization.
    /// </summary>
    [JsonPropertyName("oidc_status"), StringPredicate(Token = StringToken.Exact)]
    public virtual string? Status { get; set; }

    /// <summary>
    /// Gets or sets the subject associated with the current authorization.
    /// </summary>
    [JsonPropertyName("oidc_subject"), StringPredicate(Token = StringToken.Term, Fulltext = true)]
    public virtual string? Subject { get; set; }

    /// <summary>
    /// Gets or sets the type of the current authorization.
    /// </summary>
    [JsonPropertyName("oidc_type"), StringPredicate(Token = StringToken.Exact)]
    public virtual string? Type { get; set; }
}
