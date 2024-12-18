namespace Dgraph4Net;

public interface ISummarizeFunctions : ICountableFunction
{
    void Min(string predicate);

    void Max(string predicate);

    void Avg(string predicate);

    void Sum(string predicate);
}
