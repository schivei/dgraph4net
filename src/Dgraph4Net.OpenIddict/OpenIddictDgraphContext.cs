using System;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using SR = OpenIddict.Abstractions.OpenIddictResources;

namespace Dgraph4Net.OpenIddict;

/// <inheritdoc/>
public class OpenIddictDgraphContext : IOpenIddictDgraphContext
{
    private readonly IOptionsMonitor<OpenIddictDgraphOptions> _options;
    private readonly IServiceProvider _provider;

    public OpenIddictDgraphContext(
        IOptionsMonitor<OpenIddictDgraphOptions> options,
        IServiceProvider provider)
    {
        _options = options;
        _provider = provider;
    }

    /// <inheritdoc/>
    public ValueTask<IDgraph4NetClient> GetDatabaseAsync(CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return new ValueTask<IDgraph4NetClient>(Task.FromCanceled<IDgraph4NetClient>(cancellationToken));
        }

        var database = _options.CurrentValue.Database;
        if (database is null)
        {
            database = _provider.GetService<IDgraph4NetClient>();
        }

        if (database is null)
        {
            return new ValueTask<IDgraph4NetClient>(Task.FromException<IDgraph4NetClient>(
                new InvalidOperationException(SR.GetResourceString(SR.ID0262))));
        }

        return new ValueTask<IDgraph4NetClient>(database);
    }
}
