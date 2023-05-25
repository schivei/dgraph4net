using System.CommandLine;

namespace Dgraph4Net.Tools.Commands;

internal sealed class UserIdOption : Option<string?>
{
    public UserIdOption() : base(new[] { "--user", "-u" }, "The user id")
    {
    }
}
