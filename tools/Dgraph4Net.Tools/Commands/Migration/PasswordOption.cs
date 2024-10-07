using System.CommandLine;

namespace Dgraph4Net.Tools.Commands.Migration;

internal sealed class PasswordOption : Option<string?>
{
    public PasswordOption() : base(["--password", "-pwd"], "The password")
    {
    }
}
