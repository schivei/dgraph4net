using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

using Dgraph4Net.Annotations;

[assembly: InternalsVisibleTo("Dgraph4Net")]
[assembly: InternalsVisibleTo("Dgraph4Net.Annotations")]
[assembly: InternalsVisibleTo("Dgraph4Net.Identity")]
[assembly: InternalsVisibleTo("Dgraph4Net.Identity.Core")]
namespace Dgraph4Net;

public static class EntityExtensions
{
    public static string GetDType(this IEntity entity)
    {
        var attr =
        entity.GetType().GetCustomAttributes()
            .FirstOrDefault(dt => dt is DgraphTypeAttribute)
            as DgraphTypeAttribute;

        return attr?.Name;
    }
}
