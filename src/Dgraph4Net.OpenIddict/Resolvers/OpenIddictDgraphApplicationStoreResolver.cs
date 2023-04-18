using System;
using System.Collections.Concurrent;

using Microsoft.Extensions.DependencyInjection;

using OpenIddict.Abstractions;
using Dgraph4Net.OpenIddict.Models;

using SR = OpenIddict.Abstractions.OpenIddictResources;
using Dgraph4Net.OpenIddict.Stores;

namespace Dgraph4Net.OpenIddict.Resolvers;

/// <summary>
/// Exposes a method allowing to resolve an application store.
/// </summary>
public class OpenIddictDgraphApplicationStoreResolver : IOpenIddictApplicationStoreResolver
{
    private readonly ConcurrentDictionary<Type, Type> _cache = new();
    private readonly IServiceProvider _provider;

    public OpenIddictDgraphApplicationStoreResolver(IServiceProvider provider)
        => _provider = provider;

    /// <summary>
    /// Returns an application store compatible with the specified application type or throws an
    /// <see cref="InvalidOperationException"/> if no store can be built using the specified type.
    /// </summary>
    /// <typeparam name="TApplication">The type of the Application entity.</typeparam>
    /// <returns>An <see cref="IOpenIddictApplicationStore{TApplication}"/>.</returns>
    public IOpenIddictApplicationStore<TApplication> Get<TApplication>() where TApplication : class
    {
        var store = _provider.GetService<IOpenIddictApplicationStore<TApplication>>();
        if (store is not null)
        {
            return store;
        }

        var type = _cache.GetOrAdd(typeof(TApplication), key =>
        {
            if (!typeof(OpenIddictDgraphApplication).IsAssignableFrom(key))
            {
                throw new InvalidOperationException(SR.GetResourceString(SR.ID0257));
            }

            return typeof(OpenIddictDgraphApplicationStore<>).MakeGenericType(key);
        });

        return (IOpenIddictApplicationStore<TApplication>)_provider.GetRequiredService(type);
    }
}
