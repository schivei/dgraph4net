using System.Threading;
using System.Threading.Tasks;

namespace Dgraph4Net.OpenIddict
{
    /// <summary>
    /// Exposes the Dgraph database used by the OpenIddict stores.
    /// </summary>
    public interface IOpenIddictDgraphContext
    {
        /// <summary>
        /// Gets the <see cref="IDgraph4NetClient"/>.
        /// </summary>
        /// <returns>
        /// A <see cref="ValueTask{TResult}"/> that can be used to monitor the
        /// asynchronous operation, whose result returns the Dgraph database.
        /// </returns>
        ValueTask<IDgraph4NetClient> GetDatabaseAsync(CancellationToken cancellationToken);
    }
}
