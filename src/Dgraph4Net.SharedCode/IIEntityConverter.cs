using System.Reflection;
using System.Collections.Concurrent;
using System.Text;
namespace Dgraph4Net;

internal partial class EntityConverter : IIEntityConverter
{
    public partial T? Deserialize<T>(string json) where T : IEntity;

    public partial string Serialize<T>(T entity, bool ignoreNulls = true, bool getOnlyNulls = false, bool convertDefaultToNull = false) where T : IEntity;

    public static partial string SerializeEntity<T>(T entity, bool ignoreNulls = true, bool getOnlyNulls = false, bool convertDefaultToNull = false) where T : IEntity;

    public static partial string SerializeEntities<T>(IEnumerable<T> entities, bool ignoreNulls = true, bool getOnlyNulls = false, bool convertDefaultToNull = false) where T : IEntity;
}

public interface IIEntityConverter
{
    public static Type? Instance { get; set; }

    public static ConcurrentDictionary<PropertyInfo, IPredicate> Predicates { get; } = new();

    public static IPredicate? GetPredicate(Type type, string predicateName) =>
        Predicates.FirstOrDefault(x => x.Value.ClassMap.Type == type && x.Value.PredicateName == predicateName).Value;

    public static IEnumerable<IPredicate> GetPredicates(Type type) =>
        Predicates.Where(x => x.Value.ClassMap.Type == type).Select(x => x.Value);

    public static IPredicate? GetPredicate(PropertyInfo prop) =>
        Predicates.GetValueOrDefault(prop);

    T? Deserialize<T>(string json) where T : IEntity;

    string Serialize<T>(T entity, bool ignoreNulls = true, bool getOnlyNulls = false, bool convertDefaultToNull = false) where T : IEntity;
}
