using System.Numerics;
using System.Reflection;

namespace Dgraph4Net;

internal sealed class FilterFunctions(ExpressedFilterFunctions expressed) : IFilterFunctions
{
    public VarTriples Variables => expressed.Variables;

    #region Vector

    public bool SimilarTo(string predicate, uint top, Vector<float> vector) =>
        expressed.Append($"similar({predicate}, {top}, \"{vector.Serialize()}\")");

    #endregion

    #region Conditional
    public bool Between(string predicate, object start, object end)
    {
        var startTriple = VarTriple.Parse(start, predicate);
        var endTriple = VarTriple.Parse(end, predicate);

        Variables.AddRange([startTriple, endTriple]);

        return expressed.Append($"between({predicate}, {startTriple.Name}, {endTriple.Name})");
    }

    public bool Eq(string predicate, params object[] values)
    {
        var triples = VarTriple.Parse(values, predicate);

        Variables.Add(triples);

        return expressed.Append($"eq({predicate}, {triples.Name})");
    }

    public bool Ge(string predicate, params object[] values)
    {
        var triples = VarTriple.Parse(values, predicate);

        Variables.Add(triples);

        return expressed.Append($"ge({predicate}, {triples.Name})");
    }

    public bool Gt(string predicate, params object[] values)
    {
        var triples = VarTriple.Parse(values, predicate);

        Variables.Add(triples);

        return expressed.Append($"gt({predicate}, {triples.Name})");
    }

    public bool Le(string predicate, params object[] values)
    {
        var triples = VarTriple.Parse(values, predicate);

        Variables.Add(triples);

        return expressed.Append($"le({predicate}, {triples.Name})");
    }

    public bool Lt(string predicate, params object[] values)
    {
        var triples = VarTriple.Parse(values, predicate);

        Variables.Add(triples);

        return expressed.Append($"lt({predicate}, {triples.Name})");
    }
    #endregion

    #region String
    public bool AllOfTerms(string predicate, string value)
    {
        var triple = VarTriple.Parse(value, predicate);

        Variables.Add(triple);

        return expressed.Append($"allofterms({predicate}, {triple.Name})");
    }

    public bool AllOfText(string predicate, string value)
    {
        var triple = VarTriple.Parse(value, predicate);

        Variables.Add(triple);

        return expressed.Append($"alloftext({predicate}, {triple.Name})");
    }

    public bool AnyOfTerms(string predicate, string value)
    {
        var triple = VarTriple.Parse(value, predicate);

        Variables.Add(triple);

        return expressed.Append($"anyofterms({predicate}, {triple.Name})");
    }

    public bool Regexp(string predicate, string value)
    {
        if (!value.IsGoRegexp())
            throw new ArgumentException("Invalid Go regexp", nameof(value));

        var triple = VarTriple.Parse(value, predicate);

        Variables.Add(triple);

        return expressed.Append($"regexp({predicate}, {triple.Name})");
    }

    public bool Match(string predicate, string value, uint distance)
    {
        var triple = VarTriple.Parse(value, predicate);

        Variables.Add(triple);

        return expressed.Append($"match({predicate}, {triple.Name}, {distance})");
    }
    #endregion

    #region Node Check
    public bool Has(string predicate) =>
         expressed.Append($"has({predicate})");

    public bool Type(string type) =>
         expressed.Append($"type({type})");

    public bool Uid(params string[] uids) =>
         expressed.Append($"uid({string.Join(',', uids)})");

    public bool UidIn(string predicate, params string[] uids) =>
         expressed.Append($"uid_in({predicate}, {string.Join(',', uids)})");
    #endregion

    #region Geo

    public bool Contains(string predicate, double latitude, double longitude) =>
         expressed.Append($"contains({predicate}, [{VarTriple.Cast(latitude)}, {VarTriple.Cast(longitude)}])");

    public bool Contains(string predicate, (double latitude, double longitude)[] points)
    {
        var collection = string.Join(',', points.Select(x => $"[{VarTriple.Cast(x.latitude)}, {VarTriple.Cast(x.longitude)}]"));

        return expressed.Append($"contains({predicate}, [{collection}])");
    }

    public bool Intersects(string predicate, (double latitude, double longitude)[] points)
    {
        var intersections = string.Join(',', points.Select(x => $"[{VarTriple.Cast(x.latitude)}, {VarTriple.Cast(x.longitude)}]"));

        return expressed.Append($"intersects({predicate}, [[{intersections}]])");
    }

    public bool Near(string predicate, double latitude, double longitude, long distance) =>
         expressed.Append($"near({predicate}, [{VarTriple.Cast(latitude)}, {VarTriple.Cast(longitude)}], {VarTriple.Cast(distance)})");

    public bool Within(string predicate, (double latitude, double longitude)[] points)
    {
        var within = string.Join(',', points.Select(x => $"[{VarTriple.Cast(x.latitude)}, {VarTriple.Cast(x.longitude)}]"));

        return expressed.Append($"within({predicate}, [[{within}]])");
    }

    #endregion

    internal bool Call(MethodInfo method, params object[] parameters) =>
        (bool)method.Invoke(this, parameters);
}
