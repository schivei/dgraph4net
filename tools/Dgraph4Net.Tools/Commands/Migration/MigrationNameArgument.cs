using System.CommandLine;
using System.CommandLine.Parsing;

namespace Dgraph4Net.Tools.Commands.Migration;

/// <summary>
/// Represents an argument for the migration name.
/// </summary>
internal sealed class MigrationNameArgument : Argument<string>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MigrationNameArgument"/> class.
    /// </summary>
    public MigrationNameArgument() : base("name", "The name of the migration")
    {
        AddValidator(Validate);
    }

    /// <summary>
    /// Validates the migration name argument.
    /// </summary>
    /// <param name="symbolResult">The result of the argument parsing.</param>
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
