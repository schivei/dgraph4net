using System;

using Dgraph4Net.OpenIddict;
using Dgraph4Net.OpenIddict.Models;
using Dgraph4Net.OpenIddict.Resolvers;
using Dgraph4Net.OpenIddict.Stores;

using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Exposes extensions allowing to register the OpenIddict Dgraph services.
/// </summary>
public static class OpenIddictDgraphExtensions
{
    /// <summary>
    /// Registers the Dgraph stores services in the DI container and
    /// configures OpenIddict to use the Dgraph entities by default.
    /// </summary>
    /// <param name="builder">The services builder used by OpenIddict to register new services.</param>
    /// <remarks>This extension can be safely called multiple times.</remarks>
    /// <returns>The <see cref="OpenIddictDgraphBuilder"/>.</returns>
    public static OpenIddictDgraphBuilder UseDgraph(this OpenIddictCoreBuilder builder)
    {
        if (builder is null)
        {
            throw new ArgumentNullException(nameof(builder));
        }

        // Note: Mongo uses simple binary comparison checks by default so the additional
        // query filtering applied by the default OpenIddict managers can be safely disabled.
        builder.DisableAdditionalFiltering();

        builder.SetDefaultApplicationEntity<OpenIddictDgraphApplication>()
               .SetDefaultAuthorizationEntity<OpenIddictDgraphAuthorization>()
               .SetDefaultScopeEntity<OpenIddictDgraphScope>()
               .SetDefaultTokenEntity<OpenIddictDgraphToken>();

        // Note: the Mongo stores/resolvers don't depend on scoped/transient services and thus
        // can be safely registered as singleton services and shared/reused across requests.
        builder.ReplaceApplicationStoreResolver<OpenIddictDgraphApplicationStoreResolver>(ServiceLifetime.Singleton)
               .ReplaceAuthorizationStoreResolver<OpenIddictDgraphAuthorizationStoreResolver>(ServiceLifetime.Singleton)
               .ReplaceScopeStoreResolver<OpenIddictDgraphScopeStoreResolver>(ServiceLifetime.Singleton)
               .ReplaceTokenStoreResolver<OpenIddictDgraphTokenStoreResolver>(ServiceLifetime.Singleton);

        builder.Services.TryAddSingleton(typeof(OpenIddictDgraphApplicationStore<>));
        builder.Services.TryAddSingleton(typeof(OpenIddictDgraphAuthorizationStore<>));
        builder.Services.TryAddSingleton(typeof(OpenIddictDgraphScopeStore<>));
        builder.Services.TryAddSingleton(typeof(OpenIddictDgraphTokenStore<>));

        builder.Services.TryAddSingleton<IOpenIddictDgraphContext, OpenIddictDgraphContext>();

        return new OpenIddictDgraphBuilder(builder.Services);
    }

    /// <summary>
    /// Registers the Dgraph stores services in the DI container and
    /// configures OpenIddict to use the Dgraph entities by default.
    /// </summary>
    /// <param name="builder">The services builder used by OpenIddict to register new services.</param>
    /// <param name="configuration">The configuration delegate used to configure the Dgraph services.</param>
    /// <remarks>This extension can be safely called multiple times.</remarks>
    /// <returns>The <see cref="OpenIddictCoreBuilder"/>.</returns>
    public static OpenIddictCoreBuilder UseDgraph(
        this OpenIddictCoreBuilder builder, Action<OpenIddictDgraphBuilder> configuration)
    {
        if (builder is null)
        {
            throw new ArgumentNullException(nameof(builder));
        }

        if (configuration is null)
        {
            throw new ArgumentNullException(nameof(configuration));
        }

        configuration(builder.UseDgraph());

        return builder;
    }
}
