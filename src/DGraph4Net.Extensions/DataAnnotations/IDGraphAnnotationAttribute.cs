namespace DGraph4Net.Extensions.DataAnnotations
{
    public interface IDGraphAnnotationAttribute
    {
        DGraphType DGraphType { get; }
        string Name { get; }
    }
}
