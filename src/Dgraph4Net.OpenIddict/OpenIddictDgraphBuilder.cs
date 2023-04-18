using System;
using System.ComponentModel;

using OpenIddict.Core;

using SR = OpenIddict.Abstractions.OpenIddictResources;
using Microsoft.Extensions.DependencyInjection;
using Dgraph4Net.OpenIddict.Models;

namespace Dgraph4Net.OpenIddict;

/// <summary>
/// Exposes the necessary methods required to configure the OpenIddict Dgraph services.
/// </summary
public class OpenIddictDgraphBuilder
{
    /// <summary>
    /// Initializes a new instance of <see cref="OpenIddictDgraphBuilder"/>.
    /// </summary>
    /// <param name="services">The services collection.</param>
    public OpenIddictDgraphBuilder(IServiceCollection services)
        => Services = services ?? throw new ArgumentNullException(nameof(services));

    /// <summary>
    /// Gets the services collection.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public IServiceCollection Services { get; }

    /// <summary>
    /// Amends the default OpenIddict Dgraph configuration.
    /// </summary>
    /// <param name="configuration">The delegate used to configure the OpenIddict options.</param>
    /// <remarks>This extension can be safely called multiple times.</remarks>
    /// <returns>The <see cref="OpenIddictDgraphBuilder"/>.</returns>
    public OpenIddictDgraphBuilder Configure(Action<OpenIddictDgraphOptions> configuration)
    {
        if (configuration is null)
        {
            throw new ArgumentNullException(nameof(configuration));
        }

        Services.Configure(configuration);

        return this;
    }

    /// <summary>
    /// Configures OpenIddict to use the specified entity as the default application entity.
    /// </summary>
    /// <returns>The <see cref="OpenIddictDgraphBuilder"/>.</returns>
    public OpenIddictDgraphBuilder ReplaceDefaultApplicationEntity<TApplication>()
        where TApplication : OpenIddictDgraphApplication
    {
        Services.Configure<OpenIddictCoreOptions>(options => options.DefaultApplicationType = typeof(TApplication));

        return this;
    }

    /// <summary>
    /// Configures OpenIddict to use the specified entity as the default authorization entity.
    /// </summary>
    /// <returns>The <see cref="OpenIddictDgraphBuilder"/>.</returns>
    public OpenIddictDgraphBuilder ReplaceDefaultAuthorizationEntity<TAuthorization>()
        where TAuthorization : OpenIddictDgraphAuthorization
    {
        Services.Configure<OpenIddictCoreOptions>(options => options.DefaultAuthorizationType = typeof(TAuthorization));

        return this;
    }

    /// <summary>
    /// Configures OpenIddict to use the specified entity as the default scope entity.
    /// </summary>
    /// <returns>The <see cref="OpenIddictDgraphBuilder"/>.</returns>
    public OpenIddictDgraphBuilder ReplaceDefaultScopeEntity<TScope>()
        where TScope : OpenIddictDgraphScope
    {
        Services.Configure<OpenIddictCoreOptions>(options => options.DefaultScopeType = typeof(TScope));

        return this;
    }

    /// <summary>
    /// Configures OpenIddict to use the specified entity as the default token entity.
    /// </summary>
    /// <returns>The <see cref="OpenIddictDgraphBuilder"/>.</returns>
    public OpenIddictDgraphBuilder ReplaceDefaultTokenEntity<TToken>()
        where TToken : OpenIddictDgraphToken
    {
        Services.Configure<OpenIddictCoreOptions>(options => options.DefaultTokenType = typeof(TToken));

        return this;
    }

    /// <summary>
    /// Replaces the default application type name (by default, OpenIddictApplication).
    /// </summary>
    /// <param name="name">The collection name</param>
    /// <returns>The <see cref="OpenIddictDgraphBuilder"/>.</returns>
    public OpenIddictDgraphBuilder SetApplicationTypeName(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            throw new ArgumentException(SR.GetResourceString(SR.ID0261), nameof(name));
        }

        return Configure(options => options.ApplicationTypeName = name);
    }

    /// <summary>
    /// Replaces the default authorization type name (by default, OpenIddictAuthorization).
    /// </summary>
    /// <param name="name">The type name</param>
    /// <returns>The <see cref="OpenIddictDgraphBuilder"/>.</returns>
    public OpenIddictDgraphBuilder SetAuthorizationTypeName(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            throw new ArgumentException(SR.GetResourceString(SR.ID0261), nameof(name));
        }

        return Configure(options => options.AuthorizationTypeName = name);
    }

    /// <summary>
    /// Replaces the default scope type name (by default, OpenIddictScope).
    /// </summary>
    /// <param name="name">The type name</param>
    /// <returns>The <see cref="OpenIddictDgraphBuilder"/>.</returns>
    public OpenIddictDgraphBuilder SetScopeTypeName(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            throw new ArgumentException(SR.GetResourceString(SR.ID0261), nameof(name));
        }

        return Configure(options => options.ScopeTypeName = name);
    }

    /// <summary>
    /// Replaces the default token type name (by default, OpenIddictToken).
    /// </summary>
    /// <param name="name">The type name</param>
    /// <returns>The <see cref="OpenIddictDgraphBuilder"/>.</returns>
    public OpenIddictDgraphBuilder SetTokenTypeName(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            throw new ArgumentException(SR.GetResourceString(SR.ID0261), nameof(name));
        }

        return Configure(options => options.TokenTypeName = name);
    }

    /// <inheritdoc/>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public override bool Equals(object? obj) => base.Equals(obj);

    /// <inheritdoc/>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public override int GetHashCode() => base.GetHashCode();

    /// <inheritdoc/>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public override string? ToString() => base.ToString();
}
