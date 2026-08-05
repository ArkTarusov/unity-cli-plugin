using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace CommandRefGen.Tests;

/// <summary>
/// Constant folding decides what the reference prints as an argument's default, so a wrong answer here
/// is a wrong default in published documentation.
/// </summary>
public class ConstantEvaluatorTests
{
    [Theory]
    [InlineData("\"idle\"", "idle")]
    [InlineData("\"a\" + \"b\"", "ab")]
    [InlineData("@\"raw\\path\"", @"raw\path")]
    public void Folds_string_expressions(string expression, string expected) =>
        Assert.Equal(expected, Evaluate(expression));

    [Theory]
    [InlineData("42", 42)]
    [InlineData("-7", -7)]
    [InlineData("1024 * 1024", 1024 * 1024)]
    [InlineData("(2 + 3) * 4", 20)]
    [InlineData("1 << 10", 1024)]
    public void Folds_integer_arithmetic(string expression, int expected) =>
        Assert.Equal(expected, Evaluate(expression));

    [Fact]
    public void Folds_boolean_and_null() =>
        Assert.Equal(new object?[] { true, false, null }, new[] { Evaluate("true"), Evaluate("false"), Evaluate("null") });

    [Fact]
    public void Reads_limits_of_the_primitive_types() =>
        Assert.Equal(float.MinValue, Evaluate("float.MinValue"));

    [Fact]
    public void Follows_a_constant_declared_in_another_file()
    {
        var evaluator = EvaluatorFor(
            "class Levels { public const string Default = \"log\"; }",
            "class Other { }");

        Assert.Equal("log", evaluator.Evaluate(SyntaxFactory.ParseExpression("Levels.Default")));
    }

    [Fact]
    public void Follows_a_chain_of_constants()
    {
        var evaluator = EvaluatorFor(
            "class Sizes { public const int Kilobyte = 1024; public const int Megabyte = Kilobyte * 1024; }");

        Assert.Equal(1024 * 1024, evaluator.Evaluate(SyntaxFactory.ParseExpression("Sizes.Megabyte")));
    }

    [Fact]
    public void Resolves_an_unqualified_constant_against_the_enclosing_type()
    {
        // The default is read where it appears in the source, so the enclosing type is what makes the
        // bare name resolvable — evaluating the same text out of context could not work.
        var source = "class Commands { const int Tail = 100; static void Run(int tail = Tail) { } }";
        var evaluator = EvaluatorFor(source);
        var tree = CSharpSyntaxTree.ParseText(source);
        var parameterDefault = tree.GetRoot().DescendantNodes().OfType<ParameterSyntax>().Single().Default!.Value;

        Assert.Equal(100, evaluator.Evaluate(parameterDefault));
    }

    [Theory]
    [InlineData("SomeCall()")]
    [InlineData("Missing.Constant")]
    [InlineData("1.5 * 2")]
    public void Refuses_to_guess_at_what_it_cannot_fold(string expression) =>
        Assert.Throws<UnsupportedExpressionException>(() => Evaluate(expression));

    [Fact]
    public void Reports_a_constant_declared_twice_with_different_values()
    {
        var warnings = new List<string>();
        Index(warnings.Add, "class A { public const int X = 1; }", "class A { public const int X = 2; }");

        Assert.Contains(warnings, w => w.Contains("A.X"));
    }

    private static object? Evaluate(string expression) =>
        EvaluatorFor().Evaluate(SyntaxFactory.ParseExpression(expression));

    private static ConstantEvaluator EvaluatorFor(params string[] sources) =>
        new(Index(_ => { }, sources));

    private static Dictionary<string, ExpressionSyntax> Index(Action<string> warn, params string[] sources) =>
        ConstantEvaluator.IndexConstants(sources.Select(s => CSharpSyntaxTree.ParseText(s).GetRoot()), warn);
}
