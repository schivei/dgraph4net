using System.Numerics;
using System.Reflection;

namespace Dgraph4Net.ActiveRecords;

public readonly record struct Float32VectorPredicate(IClassMap ClassMap, PropertyInfo Property, string PredicateName, Float32VetorMetrics Index = Float32VetorMetrics.euclidean, bool Upsert = false) : IPredicate
{
    readonly string IPredicate.ToSchemaPredicate() =>
        $"{PredicateName}: float32vector @index(hnsw(metric:\"{Enum.GetName(Index)}\")){(Upsert ? "@upsert" : "")} .";
    readonly string IPredicate.ToTypePredicate() =>
        PredicateName;
    public static Float32VectorPredicate operator |(Float32VectorPredicate lpa1, Float32VectorPredicate lpa2) =>
        lpa1.Merge(lpa2);
    public Float32VectorPredicate Merge(Float32VectorPredicate lpa) =>
        new(ClassMap, Property, PredicateName, lpa.Index, Upsert || lpa.Upsert);

    public IPredicate Merge(IPredicate p2) =>
        p2 switch
        {
            Float32VectorPredicate p => Merge(p),
            _ => ((IPredicate)this).ToSchemaPredicate().StartsWith(':') ? p2 : this
        };

    public void SetValue<T>(T? target, object? value) where T : IEntity
    {
        if (((IPredicate)this).SetFaceted(target, value))
            return;

        switch (value)
        {
            case Vector<float> vector:
                Property.SetValue(target, vector);
                break;
            case float[] float32Array:
                Property.SetValue(target, new Vector<float>(float32Array));
                break;
            case int[] int32Array:
                Property.SetValue(target, new Vector<float>(int32Array.Select(Convert.ToSingle).ToArray()));
                break;
            default:
                throw new InvalidCastException($"The value {value} is not a valid float32 vector.");
        }
    }
}
