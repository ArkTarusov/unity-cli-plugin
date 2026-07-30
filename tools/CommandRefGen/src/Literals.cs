using System.Globalization;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CommandRefGen;

/// <summary>
/// Folds the constant expressions that appear in `[CliCommand]`/`[CliArg]` attribute
/// arguments and parameter defaults, and renders types and defaults the way the CLI
/// listing does (CLR type names, JSON-ish literals).
/// </summary>
internal static class Literals
{
    /// <summary>A recognized constant. <c>Value</c> may be null for a `null` literal.</summary>
    public static bool TryEvaluate(ExpressionSyntax? expr, out object? value)
    {
        value = null;
        if (expr is null) return false;

        switch (expr)
        {
            case ParenthesizedExpressionSyntax p:
                return TryEvaluate(p.Expression, out value);

            case CastExpressionSyntax c:
                // `(float)0.5` — the cast target is reflected by the declared parameter type.
                return TryEvaluate(c.Expression, out value);

            case LiteralExpressionSyntax lit:
                if (lit.IsKind(SyntaxKind.NullLiteralExpression)) return true; // null, recognized
                if (lit.IsKind(SyntaxKind.DefaultLiteralExpression)) return false; // needs the type
                value = lit.Token.Value;
                return value is not null;

            case PrefixUnaryExpressionSyntax u when u.IsKind(SyntaxKind.UnaryMinusExpression):
                if (!TryEvaluate(u.Operand, out var neg)) return false;
                value = neg switch
                {
                    int i => -i,
                    long l => -l,
                    float f => -f,
                    double d => -d,
                    decimal m => -m,
                    _ => null,
                };
                return value is not null;

            case PrefixUnaryExpressionSyntax u when u.IsKind(SyntaxKind.UnaryPlusExpression):
                return TryEvaluate(u.Operand, out value);

            case PrefixUnaryExpressionSyntax u when u.IsKind(SyntaxKind.LogicalNotExpression):
                if (!TryEvaluate(u.Operand, out var not) || not is not bool b) return false;
                value = !b;
                return true;

            case BinaryExpressionSyntax bin when bin.IsKind(SyntaxKind.AddExpression):
                // Multi-line descriptions are usually written as concatenated literals.
                if (!TryEvaluate(bin.Left, out var left) || !TryEvaluate(bin.Right, out var right)) return false;
                if (left is string ls && right is string rs) { value = ls + rs; return true; }
                return false;

            case MemberAccessExpressionSyntax ma:
                return TryWellKnownConstant(ma, out value);

            default:
                return false;
        }
    }

    /// <summary>`float.MinValue`, `string.Empty`, `int.MaxValue` and friends.</summary>
    private static bool TryWellKnownConstant(MemberAccessExpressionSyntax ma, out object? value)
    {
        value = null;
        var type = SimpleName(ma.Expression);
        if (type is null) return false;

        value = (Canonical(type), ma.Name.Identifier.ValueText) switch
        {
            ("String", "Empty") => "",
            ("Single", "MinValue") => float.MinValue,
            ("Single", "MaxValue") => float.MaxValue,
            ("Single", "Epsilon") => float.Epsilon,
            ("Single", "NaN") => float.NaN,
            ("Single", "PositiveInfinity") => float.PositiveInfinity,
            ("Single", "NegativeInfinity") => float.NegativeInfinity,
            ("Double", "MinValue") => double.MinValue,
            ("Double", "MaxValue") => double.MaxValue,
            ("Double", "Epsilon") => double.Epsilon,
            ("Int32", "MinValue") => int.MinValue,
            ("Int32", "MaxValue") => int.MaxValue,
            ("Int64", "MinValue") => long.MinValue,
            ("Int64", "MaxValue") => long.MaxValue,
            ("Int16", "MinValue") => short.MinValue,
            ("Int16", "MaxValue") => short.MaxValue,
            ("Byte", "MinValue") => byte.MinValue,
            ("Byte", "MaxValue") => byte.MaxValue,
            _ => null,
        };

        return value is not null;
    }

    /// <summary>The zero value of a type, for a `default` / `default(T)` parameter default.</summary>
    public static object? DefaultOf(TypeSyntax type)
    {
        if (type is NullableTypeSyntax or ArrayTypeSyntax) return null;

        return Canonical(SimpleName(type) ?? "") switch
        {
            "Boolean" => false,
            "Int32" => 0,
            "Int64" => 0L,
            "Int16" => (short)0,
            "Byte" => (byte)0,
            "Single" => 0f,
            "Double" => 0d,
            "Decimal" => 0m,
            "Char" => '\0',
            _ => null,
        };
    }

    /// <summary>Renders a default the way the CLI listing shows it; null means "no default".</summary>
    public static string? FormatDefault(object? value) => value switch
    {
        null => null,
        string s => Quote(s),
        char c => Quote(c.ToString()),
        bool b => b ? "true" : "false",
        // G9 round-trips a float and matches the listing's rendering of the
        // "leave Unity's value alone" sentinel float.MinValue as -3.40282347e+38.
        float f => f.ToString("G9", CultureInfo.InvariantCulture).Replace("E", "e"),
        double d => d.ToString("R", CultureInfo.InvariantCulture).Replace("E", "e"),
        decimal m => m.ToString(CultureInfo.InvariantCulture),
        IFormattable n => n.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString(),
    };

    private static string Quote(string s)
    {
        var sb = new StringBuilder(s.Length + 2).Append('"');
        foreach (var ch in s)
        {
            switch (ch)
            {
                case '"': sb.Append("\\\""); break;
                case '\\': sb.Append("\\\\"); break;
                case '\n': sb.Append("\\n"); break;
                case '\r': sb.Append("\\r"); break;
                case '\t': sb.Append("\\t"); break;
                default:
                    if (char.IsControl(ch)) sb.Append("\\u").Append(((int)ch).ToString("x4"));
                    else sb.Append(ch);
                    break;
            }
        }
        return sb.Append('"').ToString();
    }

    /// <summary>Renders a parameter type as the CLI listing does: `String`, `Int32`, `ObjectId[]`.</summary>
    public static string FormatType(TypeSyntax type) => type switch
    {
        // The listing reports the underlying type; nullability is not part of the CLI surface.
        NullableTypeSyntax n => FormatType(n.ElementType),
        ArrayTypeSyntax a => FormatType(a.ElementType) + string.Concat(a.RankSpecifiers.Select(_ => "[]")),
        GenericNameSyntax g when Canonical(g.Identifier.ValueText) is "List" or "IList" or "IEnumerable" or "IReadOnlyList"
            => FormatType(g.TypeArgumentList.Arguments[0]) + "[]",
        GenericNameSyntax g
            => $"{g.Identifier.ValueText}<{string.Join(", ", g.TypeArgumentList.Arguments.Select(FormatType))}>",
        _ => Canonical(SimpleName(type) ?? type.ToString()),
    };

    /// <summary>The trailing identifier of a possibly qualified or aliased name.</summary>
    public static string? SimpleName(SyntaxNode? node) => node switch
    {
        null => null,
        IdentifierNameSyntax id => id.Identifier.ValueText,
        GenericNameSyntax g => g.Identifier.ValueText,
        QualifiedNameSyntax q => SimpleName(q.Right),
        AliasQualifiedNameSyntax a => SimpleName(a.Name),
        MemberAccessExpressionSyntax m => m.Name.Identifier.ValueText,
        PredefinedTypeSyntax p => p.Keyword.ValueText,
        _ => null,
    };

    /// <summary>C# keyword to CLR type name; anything else is passed through.</summary>
    private static string Canonical(string name) => name switch
    {
        "string" => "String",
        "bool" => "Boolean",
        "int" => "Int32",
        "uint" => "UInt32",
        "long" => "Int64",
        "ulong" => "UInt64",
        "short" => "Int16",
        "ushort" => "UInt16",
        "byte" => "Byte",
        "sbyte" => "SByte",
        "float" => "Single",
        "double" => "Double",
        "decimal" => "Decimal",
        "char" => "Char",
        "object" => "Object",
        "void" => "Void",
        _ => name,
    };
}
