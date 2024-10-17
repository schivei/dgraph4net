using System.Text;

namespace Dgraph4Net;

public interface IGraphQueryable
{
    internal Dgraph4NetClient Client { get; }

    internal ICollection<VarBlock> VarBlocks { get; }

    IGraphQueryable WithVar(VarBlock varBlock)
    {
        VarBlocks.Add(varBlock);
        return this;
    }

    async Task<IEnumerable<T>> Do<T>(QueryBlock queryBlock, CancellationToken? cancellationToken = null) where T : new()
    {
        var query = ToQueryString(queryBlock, out var vars);

        await using var txn = Client.NewTransaction(true, true);

        return await txn.QueryWithVars<T>(queryBlock.Name, query, vars);
    }

    string ToQueryString(QueryBlock queryBlock, out Dictionary<string, string> variables)
    {
        var query = new StringBuilder();

        var vars = new VarTriples();

        foreach (var varBlock in VarBlocks)
            vars.AddRange(varBlock.Variables);

        variables = vars.ToDictionary();

        query.AppendLine($"query Q({vars.ToQueryString()}){{");

        foreach (var varBlock in VarBlocks)
            query.Append(varBlock.ToQueryString());

        query.Append(queryBlock.ToQueryString());

        query.AppendLine("}");

        return query.ToString();
    }
}
