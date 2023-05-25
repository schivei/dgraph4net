using System.CommandLine;

namespace Dgraph4Net.Tools.Commands;

internal sealed class PasswordOption : Option<string?>
{
    public PasswordOption() : base(new[] { "--password", "-p" }, "The password")
    {
    }
}
