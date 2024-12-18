using Api;

namespace Dgraph4Net;

public partial interface IDgraph4NetClient
{
    Task Alter(string schema, bool dropAll = false);
    Task<Payload> Alter(Operation operation);
    void Login(string userid, string password);
    Task LoginAsync(string userid, string password);
    Txn NewTransaction(bool readOnly = false, bool bestEffort = false, CancellationToken? cancellationToken = null, bool useNQuads = false);
}
