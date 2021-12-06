using System;

using Dgraph4Net.Annotations;

using Newtonsoft.Json;

namespace Dgraph4Net.OpenIddict.Models
{
    /// <summary>
    /// Represents an OpenIddict token.
    /// </summary>
    public class OpenIddictDgraphToken: AEntity<OpenIddictDgraphToken>
    {
        /// <summary>
        /// Gets or sets the identifier of the application associated with the current token.
        /// </summary>
        [JsonProperty("oidc_app_token"), ReversePredicate, PredicateReferencesTo(typeof(OpenIddictDgraphApplication))]
        public virtual Uid ApplicationId { get; set; } = default!;

        /// <summary>
        /// Gets or sets the identifier of the authorization associated with the current token.
        /// </summary>
        [JsonProperty("oidc_auth_token"), ReversePredicate, PredicateReferencesTo(typeof(OpenIddictDgraphAuthorization))]
        public virtual Uid AuthorizationId { get; set; } = default!;

        /// <summary>
        /// Gets or sets the concurrency token.
        /// </summary>
        [JsonProperty("oidc_concurrency_token"), StringPredicate]
        public virtual Guid ConcurrencyToken { get; set; } = Guid.NewGuid();

        /// <summary>
        /// Gets or sets the UTC creation date of the current token.
        /// </summary>
        [JsonProperty("oidc_creation_date"), DateTimePredicate(Token = DateTimeToken.Hour)]
        public virtual DateTime? CreationDate { get; set; }

        /// <summary>
        /// Gets or sets the UTC expiration date of the current token.
        /// </summary>
        [JsonProperty("oidc_expiration_date"), DateTimePredicate(Token = DateTimeToken.Hour)]
        public virtual DateTime? ExpirationDate { get; set; }

        /// <summary>
        /// Gets or sets the payload of the current token, if applicable.
        /// Note: this property is only used for reference tokens
        /// and may be encrypted for security reasons.
        /// </summary>
        [JsonProperty("oidc_payload"), StringPredicate]
        public virtual string? Payload { get; set; }

        /// <summary>
        /// Gets or sets the additional properties associated with the current token.
        /// </summary>
        [JsonProperty("oidc_properties"), CommonPredicate]
        public virtual byte[] Properties { get; set; } = new byte[0];

        /// <summary>
        /// Gets or sets the UTC redemption date of the current token.
        /// </summary>
        [JsonProperty("oidc_redemption_date"), DateTimePredicate(Token = DateTimeToken.Hour)]
        public virtual DateTime? RedemptionDate { get; set; }

        /// <summary>
        /// Gets or sets the reference identifier associated
        /// with the current token, if applicable.
        /// Note: this property is only used for reference tokens
        /// and may be hashed or encrypted for security reasons.
        /// </summary>
        [JsonProperty("oidc_reference_id"), StringPredicate]
        public virtual string? ReferenceId { get; set; }

        /// <summary>
        /// Gets or sets the status of the current token.
        /// </summary>
        [JsonProperty("oidc_status"), StringPredicate(Token = StringToken.Exact)]
        public virtual string? Status { get; set; }

        /// <summary>
        /// Gets or sets the subject associated with the current token.
        /// </summary>
        [JsonProperty("oidc_subject"), StringPredicate(Token = StringToken.Term, Fulltext = true)]
        public virtual string? Subject { get; set; }

        /// <summary>
        /// Gets or sets the type of the current token.
        /// </summary>
        [JsonProperty("oidc_type"), StringPredicate(Token = StringToken.Exact)]
        public virtual string? Type { get; set; }
    }
}
