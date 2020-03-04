using System.Linq;
using System.Reflection;
using Dgraph4Net.Annotations;

namespace Dgraph4Net.Identity
{
    internal static class EntityExtensions
    {
        internal static string GetDType(this IEntity entity)
        {
            var attr =
            entity.GetType().GetCustomAttributes()
                .FirstOrDefault(dt => dt is DgraphTypeAttribute)
                as DgraphTypeAttribute;

            return attr?.Name;
        }
    }
}
