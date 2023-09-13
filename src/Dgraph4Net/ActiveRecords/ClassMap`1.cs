using System.Collections.Immutable;
using System.Linq.Expressions;
using System.Reflection;
using Dgraph4Net.Core;
using NetGeo.Json;

namespace Dgraph4Net.ActiveRecords;

/// <summary>
/// Create a map to a dgraph type and its predicates, edges and facets
/// </summary>
/// <remarks>
/// It's class is used to map a type to a dgraph type and its predicates, edges and facets
/// </remarks>
/// <typeparam name="T"></typeparam>
public abstract class ClassMap<T> : ClassMap where T : IEntity
{
    protected ClassMap()
    {
        Type = typeof(T);
        if (InternalClassMapping.ClassMappings.ContainsKey(Type))
            throw new InvalidOperationException($"The type {Type.Name} is already mapped.");

        Uid(x => x.Uid);
        Types(x => x.DgraphType);
    }

    public override void Start() => Map();

    protected abstract void Map();

    public override void Finish()
    {
        var pendings = PendingEdges.Where(x => x.Key.DeclaringType == typeof(T)).ToImmutableDictionary();

        var errors = new List<Exception>();

        foreach (var pending in pendings)
        {
            PendingEdges.Remove(pending.Key, out _);

            if (Predicates.TryGetValue(pending.Key, out var reversedPredicate))
            {
                if (reversedPredicate is IEdgePredicate edge)
                {
                    if (!edge.Reverse)
                    {
                        errors.Add(new InvalidOperationException($"{reversedPredicate.PredicateName} is not a reversed edge."));
                    }
                    else
                    {
                        var predicate = new ListPredicate(pending.Value.map, pending.Key, reversedPredicate.PredicateName, "uid", false, true);
                        if (!Predicates.ContainsKey(pending.Value.prop))
                            Predicates.TryAdd(pending.Value.prop, predicate);
                    }
                }
                else
                {
                    throw new InvalidOperationException($"{reversedPredicate.PredicateName} is not an edge.");
                }
            }
            else
            {
                errors.Add(new InvalidOperationException($"The property {pending.Key.Name} is not mapped."));
            }
        }

        if (errors.Any())
            throw new AggregateException($"The type {typeof(T).Name} has following errors.", errors);

        if (string.IsNullOrEmpty(DgraphType))
        {
            lock (_lock)
                DgraphType = Type.Name.Replace("`", "_");
        }
    }

    protected void SetType(string typeName)
    {
        if (string.IsNullOrWhiteSpace(typeName))
            throw new ArgumentNullException(nameof(typeName));

        lock (_lock)
            DgraphType = typeName;
    }

    protected void ListInt<TE>(Expression<Func<T, TE[]>> expression, string? predicateName = null) where TE : struct, IConvertible
    {
        var te = typeof(TE);
        if (!te.IsEnum || te.GetCustomAttribute<FlagsAttribute>(true) is not null)
            throw new ArgumentException($"The type {te.Name} is not a non flagged enum.");

        var property = GetProperty(expression);

        ListInt(property, predicateName);
    }

    protected void ListInt(PropertyInfo property, string? predicateName = null)
    {
        var predicate = new ListPredicate(this, property, predicateName ?? property.Name, "int", false);

        if (!Predicates.ContainsKey(property))
            Predicates.TryAdd(property, predicate);
    }

    protected void ListString<TE>(Expression<Func<T, TE[]>> expression, string? predicateName = null) where TE : struct, IConvertible
    {
        var te = typeof(TE);
        if (!te.IsEnum || te.GetCustomAttribute<FlagsAttribute>(true) is not null)
            throw new ArgumentException($"The type {te.Name} is not a non flagged enum.");

        var property = GetProperty(expression);

        ListString(property, predicateName);
    }

    protected void ListString(PropertyInfo property, string? predicateName = null)
    {
        var predicate = new ListPredicate(this, property, predicateName ?? property.Name, "string", false);

        if (!Predicates.ContainsKey(property))
            Predicates.TryAdd(property, predicate);
    }

    protected void ListInt<TE>(Expression<Func<T, TE>> expression, string? predicateName = null) where TE : struct, IConvertible
    {
        var te = typeof(TE);
        if (!te.IsEnum || te.GetCustomAttribute<FlagsAttribute>(true) is null)
            throw new ArgumentException($"The type {te.Name} is not a flagged enum.");

        var property = GetProperty(expression);

        var predicate = new ListPredicate(this, property, predicateName ?? property.Name, "int", false);

        if (!Predicates.ContainsKey(property))
            Predicates.TryAdd(property, predicate);
    }

    protected void ListString<TE>(Expression<Func<T, TE>> expression, string? predicateName = null) where TE : struct, IConvertible
    {
        var te = typeof(TE);
        if (!te.IsEnum || te.GetCustomAttribute<FlagsAttribute>(true) is null)
            throw new ArgumentException($"The type {te.Name} is not a flagged enum.");

        var property = GetProperty(expression);

        var predicate = new ListPredicate(this, property, predicateName ?? property.Name, "string", false);

        if (!Predicates.ContainsKey(property))
            Predicates.TryAdd(property, predicate);
    }

    protected void List<TE>(Expression<Func<T, TE[]>> expression, string? predicateName = null)
    {
        var te = typeof(TE);

        if (te.IsEnum)
        {
            throw new ArgumentException($"The type {typeof(TE).Name} is not a valid dgraph primitive type. Use ListInt or ListString instead for enums.");
        }

        if (!TryGetType<TE>(out string dataType))
        {
            throw new ArgumentException($"The type {typeof(TE).Name} is not a valid dgraph primitive type.");
        }

        var property = GetProperty(expression);
        List(property, dataType, predicateName);
    }

    protected void List(PropertyInfo property, string dataType, string? predicateName)
    {
        var predicate = new ListPredicate(this, property, predicateName ?? property.Name, dataType, true);
        if (!Predicates.ContainsKey(property))
            Predicates.TryAdd(property, predicate);
    }

    protected void String<TE>(Expression<Func<T, TE?>> expression, string? predicateName = null) where TE : struct, IConvertible
    {
        var te = typeof(TE);
        if (!te.IsEnum)
            throw new ArgumentException($"The type {te.Name} is not a enum.");
        String(GetProperty(expression), predicateName, false, false, false, StringToken.Exact, null);
    }

    protected void String(Expression<Func<T, string?>> expression, string? predicateName = null, bool fulltext = false, bool trigram = false, bool upsert = false, StringToken token = StringToken.None, string? cultures = null) =>
        String(GetProperty(expression), predicateName, fulltext, trigram, upsert, token, cultures);

    protected void String(Expression<Func<T, Guid?>> expression, string? predicateName = null) =>
        String(GetProperty(expression), predicateName, false, false, false, StringToken.Exact, null);

    protected void String(Expression<Func<T, byte[]?>> expression, string? predicateName = null) =>
        String(GetProperty(expression), predicateName, false, false, false, StringToken.Exact, null);

    protected void String(PropertyInfo property, string? predicateName, bool fulltext, bool trigram, bool upsert, StringToken token, string? cultures)
    {
        var predicate = new StringPredicate(this, property, predicateName ?? property.Name, fulltext, trigram, upsert, token, cultures);
        if (!Predicates.ContainsKey(property))
            Predicates.TryAdd(property, predicate);
    }

    protected void Integer<TE>(Expression<Func<T, TE?>> expression, string? predicateName = null) where TE : struct, IConvertible
    {
        var te = typeof(TE);
        if (!te.IsEnum)
            throw new ArgumentException($"The type {te.Name} is not a enum.");
        Integer(GetProperty(expression), predicateName, true);
    }

    protected void Integer(Expression<Func<T, int?>> expression, string? predicateName = null, bool index = false) =>
        Integer(GetProperty(expression), predicateName, index);

    protected void Integer(Expression<Func<T, short?>> expression, string? predicateName = null, bool index = false) =>
        Integer(GetProperty(expression), predicateName, index);

    protected void Integer(Expression<Func<T, long?>> expression, string? predicateName = null, bool index = false) =>
        Integer(GetProperty(expression), predicateName, index);

    protected void Integer(Expression<Func<T, byte?>> expression, string? predicateName = null, bool index = false) =>
        Integer(GetProperty(expression), predicateName, index);

    protected void Integer(Expression<Func<T, TimeOnly?>> expression, string? predicateName = null, bool upsert = false) =>
        Integer(GetProperty(expression), predicateName, upsert);

    protected void Integer(Expression<Func<T, TimeSpan?>> expression, string? predicateName = null, bool index = false) =>
        Integer(GetProperty(expression), predicateName, index);

    protected void Integer(PropertyInfo property, string predicateName, bool index)
    {
        var predicate = new IntegerPredicate(this, property, predicateName ?? property.Name, index);
        if (!Predicates.ContainsKey(property))
            Predicates.TryAdd(property, predicate);
    }

    protected void Float(Expression<Func<T, float?>> expression, string? predicateName = null, bool index = false) =>
        Float(GetProperty(expression), predicateName, index);

    protected void Float(Expression<Func<T, double?>> expression, string? predicateName = null, bool index = false) =>
        Float(GetProperty(expression), predicateName, index);

    protected void Float(Expression<Func<T, decimal?>> expression, string? predicateName = null, bool index = false) =>
        Float(GetProperty(expression), predicateName, index);

    protected void Float(PropertyInfo property, string predicateName, bool index)
    {
        var predicate = new FloatPredicate(this, property, predicateName ?? property.Name, index);
        if (!Predicates.ContainsKey(property))
            Predicates.TryAdd(property, predicate);
    }

    protected void DateTime(Expression<Func<T, DateOnly?>> expression, string? predicateName = null, DateTimeToken token = DateTimeToken.None, bool upsert = false) =>
        DateTime(GetProperty(expression), predicateName, token == DateTimeToken.Hour ? DateTimeToken.Day : token, upsert);

    protected void DateTime(Expression<Func<T, DateTime?>> expression, string? predicateName = null, DateTimeToken token = DateTimeToken.None, bool upsert = false) =>
        DateTime(GetProperty(expression), predicateName, token, upsert);

    protected void DateTime(Expression<Func<T, DateTimeOffset?>> expression, string? predicateName = null, DateTimeToken token = DateTimeToken.None, bool upsert = false) =>
        DateTime(GetProperty(expression), predicateName, token, upsert);

    protected void DateTime(PropertyInfo property, string predicateName, DateTimeToken token, bool upsert)
    {
        var predicate = new DateTimePredicate(this, property, predicateName ?? property.Name, token, upsert);
        if (!Predicates.ContainsKey(property))
            Predicates.TryAdd(property, predicate);
    }

    protected void Boolean(Expression<Func<T, bool?>> expression, string? predicateName = null, bool index = false, bool upsert = false) =>
        Boolean(GetProperty(expression), predicateName, index, upsert);

    protected void Boolean(PropertyInfo property, string predicateName, bool index, bool upsert)
    {
        var predicate = new BooleanPredicate(this, property, predicateName ?? property.Name, index, upsert);
        if (!Predicates.ContainsKey(property))
            Predicates.TryAdd(property, predicate);
    }

    protected void Password(Expression<Func<T, string?>> expression, string? predicateName = null) =>
        Password(GetProperty(expression), predicateName);

    protected void Password(PropertyInfo property, string predicateName)
    {
        var predicate = new PasswordPredicate(this, property, predicateName ?? property.Name);
        if (!Predicates.ContainsKey(property))
            Predicates.TryAdd(property, predicate);
    }

    protected void Geo<TE>(Expression<Func<T, TE?>> expression, string? predicateName = null, bool index = false, bool upsert = false) where TE : GeoObject =>
        Geo(GetProperty(expression), predicateName, index, upsert);

    protected void Geo(PropertyInfo property, string predicateName, bool index, bool upsert)
    {
        var predicate = new GeoPredicate(this, property, predicateName ?? property.Name, index, upsert);
        if (!Predicates.ContainsKey(property))
            Predicates.TryAdd(property, predicate);
    }

    protected void Uid(Expression<Func<T, Uid>> expression) =>
        Uid(GetProperty(expression));

    protected void Uid(PropertyInfo property)
    {
        var predicate = new UidPredicate(this, property);
        if (!Predicates.ContainsKey(property))
            Predicates.TryAdd(property, predicate);
    }

    protected void Types(Expression<Func<T, IEnumerable<string>>> expression) =>
        Types(GetProperty(expression));

    protected void Types(PropertyInfo property)
    {
        var predicate = new TypePredicate(this, property);
        if (!Predicates.ContainsKey(property))
            Predicates.TryAdd(property, predicate);
    }

    protected void HasMany<TE>(Expression<Func<T, ICollection<TE>>> expression, string? predicateName, Expression<Func<TE, ICollection<T>>>? reversedFrom) where TE : IEntity
    {
        var property = GetProperty(expression);
        if (Predicates.ContainsKey(property))
            return;

        predicateName ??= property.Name;

        var reversed = reversedFrom is not null;
        if (reversed)
        {
            PropertyInfo? reversedProperty = GetProperty(reversedFrom);

            HasMany(property, predicateName, reversedProperty);
        }
        else
        {
            HasMany(property, predicateName);
        }
    }

    protected void HasMany<TE>(Expression<Func<T, ICollection<TE>>> expression, string? predicateName, Expression<Func<TE, T>>? reversedFrom) where TE : IEntity
    {
        var property = GetProperty(expression);
        if (Predicates.ContainsKey(property))
            return;

        predicateName ??= property.Name;

        var reversed = reversedFrom is not null;
        if (reversed)
        {
            PropertyInfo? reversedProperty = GetProperty(reversedFrom);

            HasMany(property, predicateName, reversedProperty);
        }
        else
        {
            HasMany(property, predicateName);
        }
    }

    protected void HasMany<TE>(Expression<Func<T, ICollection<TE>>> expression, string? predicateName) where TE : IEntity
    {
        var property = GetProperty(expression);
        if (Predicates.ContainsKey(property))
            return;

        predicateName ??= property.Name;

        HasMany(property, predicateName);
    }

    protected void HasMany<TE>(Expression<Func<T, ICollection<TE>>> expression) where TE : IEntity
    {
        var property = GetProperty(expression);
        if (Predicates.ContainsKey(property))
            return;

        var predicateName = property.Name;

        HasMany(property, predicateName);
    }

    protected void HasMany(PropertyInfo property, string? predicateName = null, PropertyInfo? reversedProperty = null)
    {
        var reversed = reversedProperty is not null;
        if (reversed)
        {
            if (reversedProperty is not null && Predicates.TryGetValue(reversedProperty, out var reversedPredicate))
            {
                if (reversedPredicate is EdgePredicate<T> edge)
                {
                    if (!edge.Reverse)
                        throw new InvalidOperationException($"{reversedPredicate.PredicateName} is not a reversed edge.");

                    var predicate = new ListPredicate(this, property, reversedPredicate.PredicateName, "uid", false, true);
                    if (!Predicates.ContainsKey(property))
                        Predicates.TryAdd(property, predicate);
                }
                else
                {
                    throw new InvalidOperationException($"{reversedPredicate.PredicateName} is not an edge.");
                }
            }
            else
            {
                PendingEdges.TryAdd(reversedProperty, (property, this));
            }
        }
        else
        {
            var predicate = new ListPredicate(this, property, predicateName ?? property.Name, "uid", false);
            if (!Predicates.ContainsKey(property))
                Predicates.TryAdd(property, predicate);
        }
    }

    protected void HasOne<TE>(Expression<Func<T, TE?>> expression, string? predicateName = null, bool reverse = false, bool count = false) where TE : IEntity
    {
        var property = GetProperty(expression);

        HasOne(property, predicateName, reverse, count);
    }

    protected void HasOne(PropertyInfo property, string? predicateName = null, bool reverse = false, bool count = false)
    {
        var predicate = typeof(EdgePredicate<>).MakeGenericType(property.PropertyType)
            .GetConstructors()
            .First(x => x.GetParameters().Length == 5)
            .Invoke(new object[] { this, property, predicateName ?? property.Name, reverse, count }) as IEdgePredicate;

        if (!Predicates.ContainsKey(property))
            Predicates.TryAdd(property, predicate);
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Roslynator", "RCS1158:Static member in generic type should use a type parameter.", Justification = "<Pending>")]
    internal static PropertyInfo GetProperty(Expression expression) =>
        GetProperty<T>(expression);
}
