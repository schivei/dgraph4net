using System;
using System.Collections.Concurrent;

using Microsoft.Extensions.DependencyInjection;

using OpenIddict.Abstractions;
using Dgraph4Net.OpenIddict.Models;

using SR = OpenIddict.Abstractions.OpenIddictResources;
using Dgraph4Net.OpenIddict.Stores;

namespace Dgraph4Net.OpenIddict.Resolvers;

/// <summary>
/// Exposes a method allowing to resolve a token store.
/// </summary>
public class OpenIddictDgraphTokenStoreResolver : IOpenIddictTokenStoreResolver
{
    private readonly ConcurrentDictionary<Type, Type> _cache = new();
    private readonly IServiceProvider _provider;

    public OpenIddictDgraphTokenStoreResolver(IServiceProvider provider)
        => _provider = provider;

    /// <summary>
    /// Returns a token store compatible with the specified token type or throws an
    /// <see cref="InvalidOperationException"/> if no store can be built using the specified type.
    /// </summary>
    /// <typeparam name="TToken">The type of the Token entity.</typeparam>
    /// <returns>An <see cref="IOpenIddictTokenStore{TToken}"/>.</returns>
    public IOpenIddictTokenStore<TToken> Get<TToken>() where TToken : class
    {
        var store = _provider.GetService<IOpenIddictTokenStore<TToken>>();
        if (store is not null)
        {
            return store;
        }

        var type = _cache.GetOrAdd(typeof(TToken), key =>
        {
            if (!typeof(OpenIddictDgraphToken).IsAssignableFrom(key))
            {
                throw new InvalidOperationException(SR.GetResourceString(SR.ID0260));
            }

            return typeof(OpenIddictDgraphTokenStore<>).MakeGenericType(key);
        });

        return (IOpenIddictTokenStore<TToken>)_provider.GetRequiredService(type);
    }
}
