using System.CommandLine;

namespace Dgraph4Net.Tools.Commands.Migration;

internal sealed class UpdateOption : Option<bool>
{
    public UpdateOption() : base(["--update", "-u"], "Update the database")
    {
    }
}
