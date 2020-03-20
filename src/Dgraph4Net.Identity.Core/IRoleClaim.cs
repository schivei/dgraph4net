namespace Dgraph4Net.Identity
{
    public interface IRoleClaim : IEntity
    {
        string ClaimValue { get; set; }
        string ClaimType { get; set; }
    }
}
