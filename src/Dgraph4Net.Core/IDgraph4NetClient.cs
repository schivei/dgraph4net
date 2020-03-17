using System;
using System.Threading;
using System.Threading.Tasks;

namespace Dgraph4Net
{
    public interface IDgraph4NetClient : IAsyncDisposable, IDisposable
    {
        bool Disposed { get; }
        Task Alter(string schema, bool dropAll = false);
        Task Login(string userid, string password);
    }
}
