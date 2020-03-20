using Dgraph4Net.Annotations;

namespace Dgraph4Net.Identity
{
    [DgraphType("AspNetUserToken")]
    public class DUserToken : DUserToken<DUserToken, DUser>
    {
    }
}
