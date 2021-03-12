using System;
using System.Collections.Concurrent;

using Microsoft.Extensions.DependencyInjection;

using OpenIddict.Abstractions;
using Dgraph4Net.OpenIddict.Models;

using SR = OpenIddict.Abstractions.OpenIddictResources;
using Dgraph4Net.OpenIddict.Stores;

namespace Dgraph4Net.OpenIddict.Resolvers
{
    /// <summary>
    /// Exposes a method allowing to resolve an authorization store.
    /// </summary>
    public class OpenIddictDgraphAuthorizationStoreResolver : IOpenIddictAuthorizationStoreResolver
    {
        private readonly ConcurrentDictionary<Type, Type> _cache = new ConcurrentDictionary<Type, Type>();
        private readonly IServiceProvider _provider;

        public OpenIddictDgraphAuthorizationStoreResolver(IServiceProvider provider)
            => _provider = provider;

        /// <summary>
        /// Returns an authorization store compatible with the specified authorization type or throws an
        /// <see cref="InvalidOperationException"/> if no store can be built using the specified type.
        /// </summary>
        /// <typeparam name="TAuthorization">The type of the Authorization entity.</typeparam>
        /// <returns>An <see cref="IOpenIddictAuthorizationStore{TAuthorization}"/>.</returns>
        public IOpenIddictAuthorizationStore<TAuthorization> Get<TAuthorization>() where TAuthorization : class
        {
            var store = _provider.GetService<IOpenIddictAuthorizationStore<TAuthorization>>();
            if (store is not null)
            {
                return store;
            }

            var type = _cache.GetOrAdd(typeof(TAuthorization), key =>
            {
                if (!typeof(OpenIddictDgraphAuthorization).IsAssignableFrom(key))
                {
                    throw new InvalidOperationException(SR.GetResourceString(SR.ID0258));
                }

                return typeof(OpenIddictDgraphAuthorizationStore<>).MakeGenericType(key);
            });

            return (IOpenIddictAuthorizationStore<TAuthorization>)_provider.GetRequiredService(type);
        }
    }
}
