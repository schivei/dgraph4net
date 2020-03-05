using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace Dgraph4Net
{
    public interface IEntity
    {
        Uid Id { get; set; }
        ICollection<string> DgraphType { get; set; }
    }
}
