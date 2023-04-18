namespace Dgraph4Net.Identity;

public interface IUserToken : IEntity
{
    string LoginProvider { get; set; }
    string Name { get; set; }
    string Value { get; set; }
}
