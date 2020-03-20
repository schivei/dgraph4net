namespace Dgraph4Net.Identity
{
    public interface IUserClaim : IEntity
    {
        string ClaimValue { get; set; }
        string ClaimType { get; set; }
    }
}
