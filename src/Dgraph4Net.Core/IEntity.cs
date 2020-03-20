using System;
using System.Collections.Generic;

namespace Dgraph4Net
{
    public interface IEntity
    {
        public Uid Id { get; set; }

        public ICollection<string> DgraphType { get; set; }
    }
}
