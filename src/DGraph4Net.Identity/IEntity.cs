using System;
using System.Collections.Generic;

namespace Dgraph4Net.Identity
{
    public interface IEntity
    {
        Uid Id { get; }

        ICollection<string> DType { get; }
    }
}
