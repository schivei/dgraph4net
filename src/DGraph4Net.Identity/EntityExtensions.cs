using System.Linq;
using System.Reflection;
using DGraph4Net.Annotations;

namespace DGraph4Net.Identity
{
    internal static class EntityExtensions
    {
        internal static string GetDType(this IEntity entity)
        {
            var attr =
            entity.GetType().GetCustomAttributes()
                .FirstOrDefault(dt => dt is DGraphTypeAttribute)
                as DGraphTypeAttribute;

            return attr?.Name;
        }
    }
}
