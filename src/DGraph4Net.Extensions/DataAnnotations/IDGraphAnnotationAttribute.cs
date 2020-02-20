using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DGraph4Net.Extensions.DataAnnotations
{
    internal interface IDGraphAnnotationAttribute
    {
        DGraphType DGraphType { get; }
    }
}
