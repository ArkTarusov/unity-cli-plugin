using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CommandRefGen;

/// <summary>Raised when an attribute argument is not a compile-time constant this tool can read.</summary>
public sealed class UnsupportedExpressionException(string message) : Exception(message);

/// <summary>
/// Folds the constant expressions that appear inside <c>[CliCommand]</c> / <c>[CliArg]</c> arguments and
/// parameter defaults: literals, parenthesised expressions, string concatenation, integer arithmetic,
/// <c>const</c> fields (including ones declared in another file of the package), and the limit constants
/// of the primitive types. Anything else throws instead of being guessed at — a wrong default in the
/// reference is worse than a failed run.
/// </summary>
/// <param name="constants">All <c>Type.Field</c> constants declared by the package.</param>
public sealed class ConstantEvaluator(IReadOnlyDictionary<string, ExpressionSyntax> constants)
{
    private const int MaxDepth = 16;

    /// <summary>Evaluates <paramref name="expression"/> to a boxed constant (string, bool, number, or null).</summary>
    public object? Evaluate(ExpressionSyntax expression) => Evaluate(expression, 0);

    private object? Evaluate(ExpressionSyntax expression, int depth)
    {
        if (depth > MaxDepth)
            throw Unsupported(expression, "constant reference chain is too deep (a cycle?)");

        switch (expression)
        {
            // `default` is a literal whose token value is the word itself; folding it would print
            // (default "default") for what the server reports as 0, null or false.
            case LiteralExpressionSyntax literal when literal.IsKind(SyntaxKind.DefaultLiteralExpression):
                throw Unsupported(literal, "`default` does not name a value this tool can print");

            case DefaultExpressionSyntax:
                throw Unsupported(expression, "`default(T)` does not name a value this tool can print");

            case LiteralExpressionSyntax literal:
                return literal.Token.Value;

            case ParenthesizedExpressionSyntax parenthesized:
                return Evaluate(parenthesized.Expression, depth + 1);

            case CastExpressionSyntax cast:
                // e.g. (float)1 — the cast target does not change the value the reference prints.
                return Evaluate(cast.Expression, depth + 1);

            case PrefixUnaryExpressionSyntax unary when unary.IsKind(SyntaxKind.UnaryMinusExpression):
                return Negate(Evaluate(unary.Operand, depth + 1), unary);

            case PrefixUnaryExpressionSyntax unary when unary.IsKind(SyntaxKind.UnaryPlusExpression):
                return Evaluate(unary.Operand, depth + 1);

            case BinaryExpressionSyntax binary:
                return EvaluateBinary(binary, depth);

            case IdentifierNameSyntax identifier:
                return Evaluate(ResolveUnqualified(identifier), depth + 1);

            case MemberAccessExpressionSyntax member:
                return EvaluateMemberAccess(member, depth);

            default:
                throw Unsupported(expression, $"unsupported expression kind {expression.Kind()}");
        }
    }

    /// <summary>
    /// Indexes every <c>const</c> field of the parsed sources by <c>Type.Field</c>. Namespaces are dropped,
    /// so two same-named types would collide; that is reported rather than resolved silently.
    /// </summary>
    public static Dictionary<string, ExpressionSyntax> IndexConstants(IEnumerable<SyntaxNode> roots, Action<string> warn)
    {
        var index = new Dictionary<string, ExpressionSyntax>(StringComparer.Ordinal);

        foreach (var root in roots)
        foreach (var type in root.DescendantNodes().OfType<TypeDeclarationSyntax>())
        foreach (var field in type.Members.OfType<FieldDeclarationSyntax>().Where(f => f.Modifiers.Any(SyntaxKind.ConstKeyword)))
        foreach (var variable in field.Declaration.Variables.Where(v => v.Initializer is not null))
        {
            var key = $"{type.Identifier.ValueText}.{variable.Identifier.ValueText}";
            if (index.TryGetValue(key, out var existing))
            {
                if (existing.ToString() != variable.Initializer!.Value.ToString())
                    warn($"constant {key} is declared more than once with different values — using `{existing}`");
                continue;
            }

            index[key] = variable.Initializer!.Value;
        }

        return index;
    }

    /// <summary>
    /// Handles the two shapes that occur in command metadata: a description split across source lines with
    /// '+', and a size constant written as arithmetic (1024 * 1024).
    /// </summary>
    private object EvaluateBinary(BinaryExpressionSyntax binary, int depth)
    {
        var left = Evaluate(binary.Left, depth + 1);
        var right = Evaluate(binary.Right, depth + 1);

        if (binary.IsKind(SyntaxKind.AddExpression) && (left is string || right is string))
            return string.Concat(left, right);

        if (left is null || right is null)
            throw Unsupported(binary, "arithmetic on a null constant");

        // Integer arithmetic covers every numeric constant the package writes; a fractional operand would
        // silently lose precision here, so it is rejected instead.
        if (left is not (int or long) || right is not (int or long))
            throw Unsupported(binary, "arithmetic is only supported on integer constants");

        var a = Convert.ToInt64(left);
        var b = Convert.ToInt64(right);
        var result = binary.Kind() switch
        {
            SyntaxKind.AddExpression => a + b,
            SyntaxKind.SubtractExpression => a - b,
            SyntaxKind.MultiplyExpression => a * b,
            SyntaxKind.DivideExpression when b != 0 => a / b,
            SyntaxKind.LeftShiftExpression => a << (int)b,
            SyntaxKind.RightShiftExpression => a >> (int)b,
            _ => throw Unsupported(binary, $"unsupported operator {binary.OperatorToken.Text}"),
        };

        // Keep the narrower type when both operands were ints, so the printed default matches the source.
        // A conditional expression would widen the int branch back to long, hence the explicit return.
        if (left is int && right is int && result is >= int.MinValue and <= int.MaxValue)
            return (int)result;

        return result;
    }

    /// <summary>Resolves <c>float.MinValue</c>-style limits and <c>SomeType.SomeConst</c> references.</summary>
    private object? EvaluateMemberAccess(MemberAccessExpressionSyntax member, int depth)
    {
        var typeName = member.Expression.ToString();
        var memberName = member.Name.Identifier.ValueText;

        var limit = PrimitiveLimit(typeName, memberName);
        if (limit is not null)
            return limit;

        // A qualified name may carry a namespace; the index is keyed by the simple type name.
        var simpleTypeName = typeName[(typeName.LastIndexOf('.') + 1)..];
        if (constants.TryGetValue($"{simpleTypeName}.{memberName}", out var initializer))
            return Evaluate(initializer, depth + 1);

        throw Unsupported(
            member,
            $"'{simpleTypeName}.{memberName}' is not a const field of this package — an enum member or a "
            + "computed value has no text this tool can print");
    }

    private static object? PrimitiveLimit(string typeName, string memberName) => (typeName, memberName) switch
    {
        ("float" or "Single", "MinValue") => float.MinValue,
        ("float" or "Single", "MaxValue") => float.MaxValue,
        ("float" or "Single", "Epsilon") => float.Epsilon,
        ("double" or "Double", "MinValue") => double.MinValue,
        ("double" or "Double", "MaxValue") => double.MaxValue,
        ("double" or "Double", "Epsilon") => double.Epsilon,
        ("int" or "Int32", "MinValue") => int.MinValue,
        ("int" or "Int32", "MaxValue") => int.MaxValue,
        ("long" or "Int64", "MinValue") => long.MinValue,
        ("long" or "Int64", "MaxValue") => long.MaxValue,
        ("string" or "String", "Empty") => string.Empty,
        _ => null,
    };

    /// <summary>Resolves a bare identifier against the <c>const</c> fields of the types enclosing it.</summary>
    private ExpressionSyntax ResolveUnqualified(IdentifierNameSyntax identifier)
    {
        var name = identifier.Identifier.ValueText;

        foreach (var type in identifier.Ancestors().OfType<TypeDeclarationSyntax>())
        {
            if (constants.TryGetValue($"{type.Identifier.ValueText}.{name}", out var initializer))
                return initializer;
        }

        throw Unsupported(identifier, $"no const field '{name}' is in scope");
    }

    private static object Negate(object? value, ExpressionSyntax context) => value switch
    {
        int i => -i,
        long l => -l,
        float f => -f,
        double d => -d,
        decimal m => -m,
        _ => throw Unsupported(context, "unary minus on a non-numeric constant"),
    };

    private static UnsupportedExpressionException Unsupported(ExpressionSyntax expression, string reason)
    {
        var location = expression.GetLocation().GetLineSpan();
        return new UnsupportedExpressionException(
            $"{location.Path}({location.StartLinePosition.Line + 1}): cannot evaluate `{expression}` — {reason}");
    }
}
