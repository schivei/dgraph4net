using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Dgraph4Net.OpenIddict;

/// <summary>
/// Exposes extensions simplifying the integration between OpenIddict and MongoDB.
/// </summary>
internal static class OpenIddictMongoDbHelpers
{
    /// <summary>
    /// Executes the query and returns the results as a streamed async enumeration.
    /// </summary>
    /// <typeparam name="T">The type of the returned entities.</typeparam>
    /// <param name="source">The query source.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that can be used to abort the operation.</param>
    /// <returns>The streamed async enumeration containing the results.</returns>
    internal static async IAsyncEnumerable<T> ToAsyncEnumerable<T>(this IEnumerable<T> source)
    {
        if (source is null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        foreach (var item in source)
        {
            yield return item;
        }

        await Task.CompletedTask;
    }

    /// <summary>
    /// Executes the query and returns the results as a streamed async enumeration.
    /// </summary>
    /// <typeparam name="T">The type of the returned entities.</typeparam>
    /// <param name="source">The query source.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that can be used to abort the operation.</param>
    /// <returns>The streamed async enumeration containing the results.</returns>
    internal static IAsyncEnumerable<T> ToAsyncEnumerable<T>(this IQueryable<T> source)
    {
        if (source is null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        return source.AsEnumerable().ToAsyncEnumerable();
    }
}
