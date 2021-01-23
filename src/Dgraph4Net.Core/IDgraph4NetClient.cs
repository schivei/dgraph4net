using System;
using System.Threading.Tasks;

namespace Dgraph4Net
{
    public interface IDgraph4NetClient
    {
        Task Alter(string schema, bool dropAll = false);
        Task Login(string userid, string password);
    }
}
