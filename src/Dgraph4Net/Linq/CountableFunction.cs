using System.Text;

namespace Dgraph4Net;

internal sealed class CountableFunction : ICountableFunction
{
    private readonly StringBuilder _sb = new();

    public void Count(string predicate) => _sb.Append($"count({predicate})");

    public override string ToString() => _sb.ToString();

    public static string Perform(Action<ICountableFunction> action)
    {
        var cf = new CountableFunction();
        action(cf);

        return cf.ToString();
    }
}
