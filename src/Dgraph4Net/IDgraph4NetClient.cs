using Api;

namespace Dgraph4Net;

public interface IDgraph4NetClient
{
    Task<Payload> Alter(Operation operation);
    Task Alter(string schema, bool dropAll = false);
    void Login(string userid, string password);
    Task LoginAsync(string userid, string password);
    Txn NewTransaction(bool readOnly = false, bool bestEffort = false, CancellationToken? cancellationToken = null);
}
