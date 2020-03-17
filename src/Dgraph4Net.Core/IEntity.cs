using System;
using System.Collections.Generic;

namespace Dgraph4Net
{
    public interface IEntity
    {
        Uid Id { get; set; }

        ICollection<string> DgraphType { get; set; }
    }
}
