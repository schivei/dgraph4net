using System.Text;

namespace Dgraph4Net;

public static class VarTriplesExtensions
{
    public static string ToQueryString(this VarTriples vars)
    {
        return vars.Aggregate(new StringBuilder(), (sb, tpl) =>
        {
            if (sb.Length > 0)
                sb.Append(',');
            sb.Append('$').Append(tpl.Name).Append(':').Append(' ').Append(tpl.TypeName);
            return sb;
        }, sb => sb.ToString());
    }
}
