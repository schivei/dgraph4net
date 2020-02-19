using System;
using System.Collections.Generic;

namespace DGraph4Net.Identity
{
    public interface IEntity
    {
        Uid Id { get; }

        ICollection<string> DType { get; }
    }
}
