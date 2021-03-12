using System;

using Microsoft.Extensions.DependencyInjection;

using Dgraph4Net.OpenIddict.Models;
using Dgraph4Net.Annotations;

namespace Dgraph4Net.OpenIddict
{
    /// <summary>
    /// Provides various settings needed to configure the OpenIddict MongoDB integration.
    /// </summary>
    public class OpenIddictDgraphOptions
    {
        private readonly IServiceProvider _provider;

        public OpenIddictDgraphOptions(IServiceProvider provider)
        {
            _provider = provider;

            DgraphExtensions.AddCustomType(typeof(OpenIddictDgraphApplication), new DgraphTypeAttribute(_applicationTypeName));
            DgraphExtensions.AddCustomType(typeof(OpenIddictDgraphAuthorization), new DgraphTypeAttribute(_authorizationTypeName));
            DgraphExtensions.AddCustomType(typeof(OpenIddictDgraphScope), new DgraphTypeAttribute(_scopeTypeName));
            DgraphExtensions.AddCustomType(typeof(OpenIddictDgraphToken), new DgraphTypeAttribute(_tokenTypeName));
        }

        private string _applicationTypeName = "OpenIddictApplication";
        private string _authorizationTypeName = "OpenIddictAuthorization";
        private string _scopeTypeName = "OpenIddictScope";
        private string _tokenTypeName = "OpenIddictToken";

        /// <summary>
        /// Gets or sets the name of the applications collection (by default, openiddict.applications).
        /// </summary>
        public string ApplicationTypeName
        {
            get => _applicationTypeName;
            set
            {
                _applicationTypeName = value ?? throw new ArgumentNullException(nameof(value));

                DgraphExtensions.AddCustomType(typeof(OpenIddictDgraphApplication), new DgraphTypeAttribute(_applicationTypeName));
            }
        }

        private static string? Expands(Type parent, Type type)
        {
            if (typeof(OpenIddictDgraphToken).IsAssignableFrom(type))
            {
                return type.As<OpenIddictDgraphToken>().GetColumns();
            }

            if (typeof(OpenIddictDgraphAuthorization).IsAssignableFrom(type))
            {
                return type.As<OpenIddictDgraphAuthorization>().GetColumns();
            }

            return null;
        }

        private string? _applicationFullQuery = null;
        public string ApplicationFullQuery
        {
            get => _applicationFullQuery ??= $@"{{
                oidc_application(func: type({ApplicationTypeName})) {{
                    {typeof(OpenIddictDgraphApplication).As<OpenIddictDgraphApplication>().GetColumns(Expands)}
                }}
            }}";
            set => _applicationFullQuery = value ?? _applicationFullQuery;
        }

        /// <summary>
        /// Gets or sets the name of the authorizations collection (by default, openiddict.authorizations).
        /// </summary>
        public string AuthorizationTypeName
        {
            get => _authorizationTypeName;
            set
            {
                _authorizationTypeName = value ?? throw new ArgumentNullException(nameof(value));

                DgraphExtensions.AddCustomType(typeof(OpenIddictDgraphAuthorization), new DgraphTypeAttribute(_authorizationTypeName));
            }
        }

        private string? _authorizationFullQuery = null;
        public string AuthorizationFullQuery
        {
            get => _authorizationFullQuery ??= $@"{{
                oidc_autorization(func: type({AuthorizationTypeName})) {{
                    {typeof(OpenIddictDgraphAuthorization).As<OpenIddictDgraphAuthorization>().GetColumns(Expands)}
                }}
            }}";
            set => _authorizationFullQuery = value ?? _authorizationFullQuery;
        }

        /// <summary>
        /// Gets or sets the name of the scopes collection (by default, openiddict.scopes).
        /// </summary>
        public string ScopeTypeName
        {
            get => _scopeTypeName;
            set
            {
                _scopeTypeName = value ?? throw new ArgumentNullException(nameof(value));

                DgraphExtensions.AddCustomType(typeof(OpenIddictDgraphScope), new DgraphTypeAttribute(_scopeTypeName));
            }
        }

        private string? _scopeFullQuery = null;
        public string ScopeFullQuery
        {
            get => _scopeFullQuery ??= $@"{{
                oidc_scope(func: type({ScopeTypeName})) {{
                    {typeof(OpenIddictDgraphScope).As<OpenIddictDgraphScope>().GetColumns()}
                }}
            }}";
            set => _scopeFullQuery = value ?? _scopeFullQuery;
        }

        /// <summary>
        /// Gets or sets the name of the tokens collection (by default, openiddict.tokens).
        /// </summary>
        public string TokenTypeName
        {
            get => _tokenTypeName;
            set
            {
                _tokenTypeName = value ?? throw new ArgumentNullException(nameof(value));

                DgraphExtensions.AddCustomType(typeof(OpenIddictDgraphToken), new DgraphTypeAttribute(_tokenTypeName));
            }
        }

        private string? _tokenFullQuery = null;
        public string TokenFullQuery
        {
            get => _tokenFullQuery ??= $@"{{
                oidc_token(func: type({TokenTypeName})) {{
                    {typeof(OpenIddictDgraphToken).As<OpenIddictDgraphToken>().GetColumns()}
                }}
            }}";
            set => _tokenFullQuery = value ?? _tokenFullQuery;
        }

        public IDgraph4NetClient Database => _provider.GetRequiredService<IDgraph4NetClient>();
    }
}
