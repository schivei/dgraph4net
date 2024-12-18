using System.CommandLine;
using System.CommandLine.Parsing;

namespace Dgraph4Net.Tools.Commands.Migration;

internal sealed class MigrationNameArgument : Argument<string>
{
    public MigrationNameArgument() : base("name", "The name of the migration")
    {
        AddValidator(Validate);
    }

    /// <summary>
    /// Name must: start with upper case letter, has min of 3 characters, and only contain letters and numbers
    /// </summary>
    private static void Validate(ArgumentResult symbolResult)
    {
        var name = symbolResult.Tokens[0].Value;
        if (name.Length < 3)
            symbolResult.ErrorMessage = "name must have at least 3 characters";
        else if (!char.IsUpper(name[0]))
            symbolResult.ErrorMessage = "name must start with upper case letter";
        else if (!name.All(char.IsLetterOrDigit))
            symbolResult.ErrorMessage = "name must only contain letters and numbers";
    }
}
