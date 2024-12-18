
namespace Dgraph4Net;

public static class QueryGraph
{
    public static IGraphQueryable CreateQuery(this Dgraph4NetClient client) =>
        new GraphQueryable(client);
}

public class VarBlock
{
    private readonly string _alias;

    public string Alias { get; }

    public VarTriples Variables => _functions.Variables;

    private readonly Functions _functions;

    //private readonly Filters _filters;

    //private readonly SelectBlock? _selectBlock;

    public VarBlock(string alias, Action<IFunctions> function)
    {
        ArgumentNullException.ThrowIfNull(alias);

        ArgumentNullException.ThrowIfNull(function);

        Alias = alias;
        _alias = $"{alias} as ";

        _functions = new();
        //_filters = new();

        function(_functions);
    }

    public VarBlock(string alias, Action<IFunctions> function, Action<IFilterFunctions> filter) : this(alias, function)
    {
        ArgumentNullException.ThrowIfNull(function);

        //filter(_filters);
    }

    //public VarBlock(Action<IFunctions> function, SelectBlock selectBlock) : this(string.Empty, function)
    //{
    //    ArgumentNullException.ThrowIfNull(selectBlock);

    //    _selectBlock = selectBlock;
    //}

    //public VarBlock(Action<IFunctions> function, Action<IFilterFunctions> filter, SelectBlock selectBlock) : this(string.Empty, function, filter)
    //{
    //    ArgumentNullException.ThrowIfNull(selectBlock);

    //    _selectBlock = selectBlock;
    //}

    public string ToQueryString() => $"{_alias}var(func:{_functions}) {{";

    public static implicit operator string(VarBlock varBlock) => varBlock.ToQueryString();

    public static implicit operator VarBlock((string alias, Action<IFunctions> function) vt) => new(vt.alias, vt.function);

    public static implicit operator VarBlock((string alias, Action<IFunctions> function, Action<IFilterFunctions> filter) vt) => new(vt.alias, vt.function, vt.filter);

    //public static implicit operator VarBlock((Action<IFunctions> function, SelectBlock selectBlock) vt) => new(vt.function, vt.selectBlock);

    //public static implicit operator VarBlock((Action<IFunctions> function, Action<IFilterFunctions> filter, SelectBlock selectBlock) vt) => new(vt.function, vt.filter, vt.selectBlock);
}

public sealed class QueryBlock(string name) : ISortingFunctions, IPaginationFunctions
{
    public string Name { get; } = name;

    public VarTriples Variables => _functions.Variables;

    #region Sorting
    internal ISortingFunctions Sorting => this;

    string ISortingFunctions.SortBy { get; set; }

    SortingType ISortingFunctions.Order { get; set; }
    #endregion

    private readonly Functions _functions = new();

    //private readonly Filters _filters = new();

    //private readonly SelectBlock? _selectBlock;

    private long? _first;
    private long? _offset;
    private Uid? _after;

    //public QueryBlock(string name, Action<IFunctions> function, SelectBlock selectBlock) : this(name)
    //{
    //    ArgumentNullException.ThrowIfNull(function);

    //    ArgumentNullException.ThrowIfNull(selectBlock);

    //    function(_functions);

    //    _selectBlock = selectBlock;
    //}

    //public QueryBlock(string name, Action<IFunctions> function, Action<IFilterFunctions> filter, SelectBlock selectBlock) : this(name, function, selectBlock)
    //{
    //    ArgumentNullException.ThrowIfNull(filter);

    //    filter(_filters);
    //}

    public string ToQueryString() => $"{Name} {ToQueryString()}";

    #region Pagination
    internal IPaginationFunctions Pagination => this;

    public void First(long first) =>
        _first = first;

    public void Offset(long offset) =>
        _offset = offset;

    public void After(Uid after) =>
        _after = after;
    #endregion

    private bool HasSorting() =>
        !string.IsNullOrWhiteSpace(Sorting.SortBy);

    private string ToSortingString() =>
        HasSorting() ? $"order{Enum.GetName(Sorting.Order)}:{Sorting.SortBy}" : string.Empty;

    private bool HasPagination() =>
        _first.HasValue || _offset.HasValue || _after.HasValue;

    private string ToPaginationString()
    {
        var comp = new List<string>();

        if (_first.HasValue)
            comp.Add($"first:{_first}");

        if (_offset.HasValue)
            comp.Add($"offset:{_offset}");

        if (_after.HasValue)
            comp.Add($"after:{_after}");

        return string.Join(',', comp);
    }
}
