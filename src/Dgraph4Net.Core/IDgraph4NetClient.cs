using System.Threading;
using System.Threading.Tasks;

namespace Dgraph4Net;

public interface IDgraph4NetClient
{
    Task<object> Alter(object operation);
    Task Alter(string schema, bool dropAll = false);
    Task Login(string userid, string password);
    object NewTransaction(bool readOnly = false, bool bestEffort = false, CancellationToken? cancellationToken = null);
}
