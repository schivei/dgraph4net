using System;
using System.Threading.Tasks;

namespace Dgraph4Net
{
    public interface IDgraph4NetClient : IAsyncDisposable
    {
        Task Alter(string schema, bool dropAll = false);
        Task Login(string userid, string password);
        bool IsDisposed();
    }
}
