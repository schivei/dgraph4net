using System.Numerics;
using System.Text;

namespace Dgraph4Net;

internal sealed class Functions : IFunctions
{
    private readonly StringBuilder _stringBuilder = new();

    public VarTriples Variables { get; } = [];

    #region Vector

    public void SimilarTo(string predicate, uint top, Vector<float> vector) =>
        _stringBuilder.Append($"similar({predicate}, {top}, \"{vector.Serialize()}\")");

    #endregion

    #region Conditional
    public void Between(string predicate, object start, object end)
    {
        var startTriple = VarTriple.Parse(start);
        var endTriple = VarTriple.Parse(end);

        Variables.Add(startTriple);

        _stringBuilder.Append($"between({predicate}, {startTriple.Value}, {endTriple.Value})");
    }

    public void Eq(string predicate, params object[] values)
    {
        var triples = values.Select(VarTriple.Parse);

        Variables.AddRange(triples);

        _stringBuilder.Append($"eq({predicate}, [{string.Join(',', triples.Select(x => x.Name))}])");
    }

    public void Ge(string predicate, params object[] values)
    {
        var triples = values.Select(VarTriple.Parse);

        Variables.AddRange(triples);

        _stringBuilder.Append($"ge({predicate}, [{string.Join(',', triples.Select(x => x.Name))}])");
    }

    public void Gt(string predicate, params object[] values)
    {
        var triples = values.Select(VarTriple.Parse);

        Variables.AddRange(triples);

        _stringBuilder.Append($"gt({predicate}, [{string.Join(',', triples.Select(x => x.Name))}])");
    }

    public void Le(string predicate, params object[] values)
    {
        var triples = values.Select(VarTriple.Parse);

        Variables.AddRange(triples);

        _stringBuilder.Append($"le({predicate}, [{string.Join(',', triples.Select(x => x.Name))}])");
    }

    public void Lt(string predicate, params object[] values)
    {
        var triples = values.Select(VarTriple.Parse);

        Variables.AddRange(triples);

        _stringBuilder.Append($"lt({predicate}, [{string.Join(',', triples.Select(x => x.Name))}])");
    }
    #endregion

    #region String
    public void AllOfTerms(string predicate, string value)
    {
        var triple = VarTriple.Parse(value);

        Variables.Add(triple);

        _stringBuilder.Append($"allofterms({predicate}, {triple.Name})");
    }

    public void AllOfText(string predicate, string value)
    {
        var triple = VarTriple.Parse(value);

        Variables.Add(triple);

        _stringBuilder.Append($"alloftext({predicate}, {triple.Name})");
    }

    public void AnyOfTerms(string predicate, string value)
    {
        var triple = VarTriple.Parse(value);

        Variables.Add(triple);

        _stringBuilder.Append($"anyofterms({predicate}, {triple.Name})");
    }

    public void Regexp(string predicate, string value)
    {
        if (!value.IsGoRegexp())
            throw new ArgumentException("Invalid Go regexp", nameof(value));

        _stringBuilder.Append($"regexp({predicate}, {value})");
    }

    public void Match(string predicate, string value, uint distance)
    {
        var triple = VarTriple.Parse(value);

        Variables.Add(triple);

        _stringBuilder.Append($"match({predicate}, {triple.Name}, {distance})");
    }
    #endregion

    #region Node Check
    public void Has(string predicate) =>
        _stringBuilder.Append($"has({predicate})");

    public void Type(string type) =>
        _stringBuilder.Append($"type({type})");

    public void Uid(params string[] uids) =>
        _stringBuilder.Append($"uid({string.Join(',', uids)})");

    public void UidIn(string predicate, params string[] uids) =>
        _stringBuilder.Append($"uid_in({predicate}, {string.Join(',', uids)})");
    #endregion

    #region Geo

    public void Contains(string predicate, double latitude, double longitude) =>
        _stringBuilder.Append($"contains({predicate}, [{VarTriple.Cast(latitude)}, {VarTriple.Cast(longitude)}])");

    public void Contains(string predicate, (double latitude, double longitude)[] points)
    {
        var collection = string.Join(',', points.Select(x => $"[{VarTriple.Cast(x.latitude)}, {VarTriple.Cast(x.longitude)}]"));

        _stringBuilder.Append($"contains({predicate}, [{collection}])");
    }

    public void Intersects(string predicate, (double latitude, double longitude)[] points)
    {
        var intersections = string.Join(',', points.Select(x => $"[{VarTriple.Cast(x.latitude)}, {VarTriple.Cast(x.longitude)}]"));

        _stringBuilder.Append($"intersects({predicate}, [[{intersections}]])");
    }

    public void Near(string predicate, double latitude, double longitude, long distance) =>
        _stringBuilder.Append($"near({predicate}, [{VarTriple.Cast(latitude)}, {VarTriple.Cast(longitude)}], {VarTriple.Cast(distance)})");

    public void Within(string predicate, (double latitude, double longitude)[] points)
    {
        var withins = string.Join(',', points.Select(x => $"[{VarTriple.Cast(x.latitude)}, {VarTriple.Cast(x.longitude)}]"));

        _stringBuilder.Append($"within({predicate}, [[{withins}]])");
    }

    #endregion

    public override string ToString() => _stringBuilder.ToString();
}
