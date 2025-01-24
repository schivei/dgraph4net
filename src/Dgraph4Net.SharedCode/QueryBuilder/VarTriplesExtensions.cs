namespace Dgraph4Net;

public static class VarTriplesExtensions
{
    public static string ToQueryString(this VarTriples vars) =>
        string.Join(", ", vars);
}
