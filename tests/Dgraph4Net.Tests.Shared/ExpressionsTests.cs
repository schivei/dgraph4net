namespace Dgraph4Net.Tests;

[Collection("Dgraph4Net")]
public class ExpressionsTests : ExamplesTest
{
    [Fact]
    public void FilterTest()
    {
        var expressed = ExpressedFilterFunctions.Parse(func =>
            func.Has<Testing>(x => x.Name) &&
            (func.Has<Person>(p => p.Age) || func.Eq<Person, string>(x => x.Family, "teste")));
        var expectedExpression = $"@filter(has(name) and (has(age) or eq(family, {expressed.Variables["family"].Name})))";
        var vars = expressed.Variables.ToQueryString();
        var expectedVars = $"{expressed.Variables["family"].Name}: string";
        var ex = expressed.ToString();
        Equal(expectedExpression, ex);
        Equal(expectedVars, vars);
    }
}
