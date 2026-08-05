using System.Globalization;
using System.Text.Json;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace CommandRefGen;

/// <summary>
/// Reads the <c>[CliCommand]</c> surface out of the package's C# sources with the Roslyn syntax API.
///
/// The metadata this produces mirrors what a running Pipeline server reports for `unity --json command`:
/// argument name falls back to the C# parameter name, an argument is required when the attribute says so
/// (or, with no attribute, when the parameter has no C# default), and the C# parameter default wins over
/// the attribute's <c>DefaultValue</c>.
/// </summary>
public sealed class CommandParser(Action<string> warn)
{
    /// <summary>Package-relative directories whose commands are documented. Everything else is ignored.</summary>
    private static readonly string[] SourceRoots = { "Editor/Commands", "Runtime/Commands" };

    // Both are built in Parse, once every source file has been read.
    private ConstantEvaluator constants = null!;
    private StructuredInputReader structuredInputs = null!;

    /// <summary>Parses every command under <paramref name="packageRoot"/> (the tarball's <c>package/</c> directory).</summary>
    public List<CommandInfo> Parse(string packageRoot)
    {
        // A command's default value may reference a const declared anywhere in the package (say a level
        // name in Runtime/Console), so the whole package is parsed before any command is read — in a
        // fixed order, because the indexes below resolve a repeated name to whichever declaration they
        // meet first, and an arbitrary order would make the output depend on the machine it ran on.
        // Ordered by the package-relative path rather than the native one: the two disagree because the
        // directory separator sorts differently against letters ('/' below them, '\' above), which would
        // put "Commands/Capture/..." before or after "Commands/CaptureEditorElementCommand.cs" depending
        // on the operating system.
        var sources = EnumerateSources(packageRoot)
            .Select(file => (Path: Path.GetRelativePath(packageRoot, file).Replace('\\', '/'), File: file))
            .OrderBy(source => source.Path, StringComparer.Ordinal);

        var trees = new List<(string Path, SyntaxTree Tree)>();
        foreach (var (relativePath, file) in sources)
            trees.Add((relativePath, ParseTree(file, relativePath)));

        var roots = trees.Select(entry => entry.Tree.GetRoot()).ToList();
        constants = new ConstantEvaluator(ConstantEvaluator.IndexConstants(roots, warn));
        var (types, ambiguousTypes) = IndexTypes(roots);
        structuredInputs = new StructuredInputReader(types, ambiguousTypes, constants, TypeName, warn);

        var commands = new List<CommandInfo>();
        foreach (var (relativePath, tree) in trees)
        {
            if (IsCommandSource(relativePath))
                commands.AddRange(ParseCommands(tree, relativePath));
            else
                ReportStrayCommands(tree, relativePath);
        }

        foreach (var group in commands.GroupBy(c => c.Name).Where(g => g.Count() > 1))
            warn($"command '{group.Key}' is declared {group.Count()} times: {string.Join(", ", group.Select(c => $"{c.SourcePath}:{c.SourceLine}"))}");

        return commands;
    }

    /// <summary>
    /// Reports a <c>[CliCommand]</c> found outside the directories this tool reads commands from. The
    /// package keeps them all under <see cref="SourceRoots"/> today; if a future version moves one, it
    /// would otherwise vanish from the reference while <c>--check</c> still reported success.
    /// </summary>
    private void ReportStrayCommands(SyntaxTree tree, string relativePath)
    {
        foreach (var method in CommandMethods(tree.GetRoot()))
        {
            var line = tree.GetLineSpan(method.Identifier.Span).StartLinePosition.Line + 1;
            warn($"{relativePath}:{line}: [CliCommand] on method '{method.Identifier.ValueText}' lives outside {string.Join(" and ", SourceRoots)} and is missing from the reference");
        }
    }

    /// <summary>
    /// Indexes the package's type declarations by simple name, which is how a parameter refers to them.
    /// A type split with <c>partial</c> keeps all its parts, since its members are spread across them.
    /// Two unrelated types sharing a name in different namespaces are indistinguishable here; the package
    /// has several such pairs that no command uses, so that is reported only if one is actually expanded.
    /// </summary>
    private static (Dictionary<string, List<TypeDeclarationSyntax>> Index, HashSet<string> Ambiguous) IndexTypes(IEnumerable<SyntaxNode> roots)
    {
        var index = new Dictionary<string, List<TypeDeclarationSyntax>>(StringComparer.Ordinal);

        foreach (var declaration in roots.SelectMany(root => root.DescendantNodes().OfType<TypeDeclarationSyntax>()))
        {
            if (!index.TryGetValue(declaration.Identifier.ValueText, out var parts))
                index[declaration.Identifier.ValueText] = parts = new List<TypeDeclarationSyntax>();

            parts.Add(declaration);
        }

        // Several parts of one partial type share a container; declarations in different containers are
        // different types that this index cannot tell apart, whether or not they are partial.
        var ambiguous = index
            .Where(entry => entry.Value.Select(Container).Distinct(StringComparer.Ordinal).Count() > 1)
            .Select(entry => entry.Key)
            .ToHashSet(StringComparer.Ordinal);

        return (index, ambiguous);
    }

    /// <summary>The namespaces and outer types a declaration sits in, which is what its simple name omits.</summary>
    private static string Container(TypeDeclarationSyntax declaration) =>
        string.Join(
            ".",
            declaration.Ancestors()
                .Reverse()
                .Select(ancestor => ancestor switch
                {
                    BaseNamespaceDeclarationSyntax ns => ns.Name.ToString(),
                    TypeDeclarationSyntax outer => outer.Identifier.ValueText,
                    _ => null,
                })
                .OfType<string>());

    private static bool IsCommandSource(string relativePath) =>
        SourceRoots.Any(root => relativePath.StartsWith(root + "/", StringComparison.Ordinal));

    private static IEnumerable<string> EnumerateSources(string packageRoot)
    {
        foreach (var file in Directory.EnumerateFiles(packageRoot, "*.cs", SearchOption.AllDirectories))
        {
            var segments = Path.GetRelativePath(packageRoot, file).Replace('\\', '/').Split('/');

            // The package's own Tests assembly, at the root, declares registration fixtures (log_editor,
            // test_types, ...) that are not part of the shipped command surface — but a directory merely
            // named Tests inside a compiled assembly is compiled like any other, so only the root one is
            // skipped. Unity compiles nothing inside a directory whose name ends with '~' (Samples~,
            // Documentation~) at any depth: a type declared there is not a type of this package.
            if (segments[0] == "Tests" || segments.Any(segment => segment.EndsWith('~')))
                continue;

            yield return file;
        }
    }

    private SyntaxTree ParseTree(string path, string relativePath)
    {
        var text = File.ReadAllText(path);

        // Parse once with nothing defined only to learn which preprocessor symbols the file talks about
        // and with which polarity, then re-parse with the right ones defined so that guarded commands are
        // present in the tree. A symbol the file only tests positively (#if UNITY_6000_7_OR_NEWER) is
        // defined; one it only negates (#if !UNITY_SERVER) is left undefined, so that branch is active
        // too. A symbol tested both ways cannot be satisfied by one parse — it is defined, and the losing
        // branch is caught by ReportHiddenCommands below.
        var probe = CSharpSyntaxTree.ParseText(text, new CSharpParseOptions(LanguageVersion.Latest), path: relativePath);
        var conditionals = Directives(probe.GetRoot()).OfType<ConditionalDirectiveTriviaSyntax>().ToList();

        var positive = new HashSet<string>(StringComparer.Ordinal);
        var negative = new HashSet<string>(StringComparer.Ordinal);
        foreach (var conditional in conditionals)
            CollectPolarities(conditional.Condition, negated: false, positive, negative);

        var symbols = positive.ToList();

        var tree = CSharpSyntaxTree.ParseText(
            text,
            new CSharpParseOptions(LanguageVersion.Latest, preprocessorSymbols: symbols),
            path: relativePath);

        ReportHiddenCommands(tree, relativePath);
        return tree;
    }

    /// <summary>
    /// The symbol choice in <see cref="ParseTree"/> satisfies each condition where it can, but one parse
    /// cannot activate both arms of an <c>#if</c>/<c>#else</c>, nor both branches of a symbol the file
    /// tests positively in one place and negated in another. The losing branch never reaches the syntax
    /// tree; it survives as disabled text — report any that declares a command rather than dropping it
    /// without a word.
    /// </summary>
    private void ReportHiddenCommands(SyntaxTree tree, string relativePath)
    {
        foreach (var trivia in tree.GetRoot().DescendantTrivia(descendIntoTrivia: true)
                     .Where(t => t.IsKind(SyntaxKind.DisabledTextTrivia) && t.ToFullString().Contains("[CliCommand", StringComparison.Ordinal)))
        {
            var line = tree.GetLineSpan(trivia.Span).StartLinePosition.Line + 1;
            warn($"{relativePath}:{line}: a [CliCommand] sits in an inactive conditional branch and is missing from the reference");
        }
    }

    /// <summary>Methods carrying a <c>[CliCommand]</c>, read from the syntax tree so that a mention in a comment is not one.</summary>
    private static IEnumerable<MethodDeclarationSyntax> CommandMethods(SyntaxNode root) =>
        root.DescendantNodes()
            .OfType<MethodDeclarationSyntax>()
            .Where(method => method.AttributeLists.SelectMany(list => list.Attributes).Any(a => IsAttribute(a, "CliCommand")));

    private IEnumerable<CommandInfo> ParseCommands(SyntaxTree tree, string relativePath)
    {
        var root = tree.GetCompilationUnitRoot();
        var scopes = ConditionalScopes(root);

        foreach (var method in CommandMethods(root))
        {
            var attribute = method.AttributeLists
                .SelectMany(list => list.Attributes)
                .First(a => IsAttribute(a, "CliCommand"));

            // CommandRegistry registers static methods only, so a non-static one never becomes a
            // command however it is attributed.
            if (!method.Modifiers.Any(SyntaxKind.StaticKeyword))
            {
                var declaration = tree.GetLineSpan(method.Identifier.Span).StartLinePosition.Line + 1;
                warn($"{relativePath}:{declaration}: [CliCommand] on non-static method '{method.Identifier.ValueText}' — the package registers only static methods, skipping");
                continue;
            }

            yield return BuildCommand(method, attribute, scopes, relativePath, tree);
        }
    }

    private CommandInfo BuildCommand(
        MethodDeclarationSyntax method,
        AttributeSyntax attribute,
        List<(TextSpan Span, string Condition)> scopes,
        string relativePath,
        SyntaxTree tree)
    {
        var nameArgument = ConstructorArgument(attribute, 0, "name");
        var descriptionArgument = ConstructorArgument(attribute, 1, "description");
        if (nameArgument is null || descriptionArgument is null)
            throw new InvalidOperationException($"{relativePath}: [CliCommand] on {method.Identifier.ValueText} does not supply both a name and a description");

        var name = AsString(nameArgument, relativePath);
        var description = Prose.Collapse(AsString(descriptionArgument, relativePath));
        var mainThreadRequired = NamedBool(attribute, "MainThreadRequired") ?? true;
        var runtimeOnly = NamedBool(attribute, "RuntimeOnly") ?? false;

        var gates = scopes
            .Where(scope => scope.Span.Contains(method.Span))
            .OrderBy(scope => scope.Span.Start)
            .Select(scope => scope.Condition)
            .ToList();

        var line = tree.GetLineSpan(attribute.Span).StartLinePosition.Line + 1;
        var args = method.ParameterList.Parameters.Select(p => BuildArg(p, relativePath)).ToList();

        foreach (var duplicate in args.GroupBy(a => a.Name).Where(g => g.Count() > 1))
            warn($"{relativePath}:{line}: command '{name}' declares the argument '{duplicate.Key}' {duplicate.Count()} times");

        return new CommandInfo(name, description, mainThreadRequired, runtimeOnly, args, gates, relativePath, line);
    }

    private CommandArg BuildArg(ParameterSyntax parameter, string relativePath)
    {
        var attribute = parameter.AttributeLists
            .SelectMany(list => list.Attributes)
            .FirstOrDefault(a => IsAttribute(a, "CliArg"));

        var parameterName = parameter.Identifier.ValueText;
        var hasParameterDefault = parameter.Default is not null;

        string name;
        string description;
        bool required;
        object? attributeDefault = null;

        if (attribute is null)
        {
            name = parameterName;
            description = $"Parameter: {parameterName}";
            required = !hasParameterDefault;
        }
        else
        {
            var nameArgument = ConstructorArgument(attribute, 0, "name");
            var descriptionArgument = ConstructorArgument(attribute, 1, "description");
            if (nameArgument is null || descriptionArgument is null)
                throw new InvalidOperationException($"{relativePath}: [CliArg] on parameter '{parameterName}' does not supply both a name and a description");

            name = AsString(nameArgument, relativePath);
            description = Prose.Collapse(AsString(descriptionArgument, relativePath));
            required = NamedBool(attribute, "Required") ?? false;
            var defaultArgument = NamedArgument(attribute, "DefaultValue");
            if (defaultArgument is not null)
                attributeDefault = constants.Evaluate(defaultArgument.Expression);
        }

        // Matches CommandRegistry.DiscoverParameters: the C# default takes precedence, the attribute's
        // DefaultValue is only consulted for parameters that have none.
        var defaultValue = hasParameterDefault
            ? constants.Evaluate(parameter.Default!.Value)
            : attributeDefault;

        return new CommandArg(
            name,
            description,
            TypeName(parameter.Type!),
            required,
            JsonLiteral(defaultValue),
            structuredInputs.Expand(parameter.Type!, $"{relativePath}: argument '{name}'"));
    }

    /// <summary>
    /// Renders a parameter's declared type the way the server's command listing does — that is,
    /// <c>System.Type.Name</c>: keyword aliases become framework names, generics keep the arity suffix,
    /// and namespaces are dropped. Nullable types are the one deliberate departure, see below.
    /// </summary>
    private static string TypeName(TypeSyntax type)
    {
        switch (type)
        {
            case ArrayTypeSyntax array:
                var ranks = string.Concat(array.RankSpecifiers.Select(r => "[" + new string(',', r.Rank - 1) + "]"));
                return TypeName(array.ElementType) + ranks;

            case NullableTypeSyntax nullable:
                // The live listing reports Type.Name here, which for int? is the unusable "Nullable`1".
                // The underlying type is what a caller needs; optionality is already carried by the
                // absence of the required marker.
                return TypeName(nullable.ElementType);

            case GenericNameSyntax generic:
                // Type.Name would give List`1: a literal backtick, which pairs with the ones the writer
                // puts around argument names and breaks the rest of the line. The written-out form says
                // more anyway.
                return $"{generic.Identifier.ValueText}<{string.Join(", ", generic.TypeArgumentList.Arguments.Select(TypeName))}>";

            case QualifiedNameSyntax qualified:
                return TypeName(qualified.Right);

            case PredefinedTypeSyntax predefined:
                return predefined.Keyword.ValueText switch
                {
                    "bool" => "Boolean",
                    "byte" => "Byte",
                    "sbyte" => "SByte",
                    "char" => "Char",
                    "decimal" => "Decimal",
                    "double" => "Double",
                    "float" => "Single",
                    "int" => "Int32",
                    "uint" => "UInt32",
                    "long" => "Int64",
                    "ulong" => "UInt64",
                    "short" => "Int16",
                    "ushort" => "UInt16",
                    "object" => "Object",
                    "string" => "String",
                    var other => other,
                };

            default:
                return type.ToString();
        }
    }

    /// <summary>Formats a default value as the JSON literal the reference prints, or null when there is none.</summary>
    private static string? JsonLiteral(object? value)
    {
        switch (value)
        {
            case null:
                return null;
            case string s:
                return JsonSerializer.Serialize(s);
            case bool b:
                return b ? "true" : "false";
            case float f:
                // JSON has one number type, so a whole float prints as 1, exactly as the live listing shows it.
                return f.ToString("R", CultureInfo.InvariantCulture).Replace("E", "e");
            case double d:
                return d.ToString("R", CultureInfo.InvariantCulture).Replace("E", "e");
            default:
                return Convert.ToString(value, CultureInfo.InvariantCulture);
        }
    }

    /// <summary>The conditional-compilation regions of a file, as spans paired with their condition text.</summary>
    private static List<(TextSpan Span, string Condition)> ConditionalScopes(CompilationUnitSyntax root)
    {
        var scopes = new List<(TextSpan, string)>();
        var open = new Stack<(string Condition, int Start)>();

        foreach (var directive in Directives(root))
        {
            switch (directive)
            {
                case IfDirectiveTriviaSyntax ifDirective:
                    open.Push((ifDirective.Condition.ToString(), ifDirective.Span.End));
                    break;

                case ElifDirectiveTriviaSyntax elif:
                    Close(elif.SpanStart);
                    open.Push((elif.Condition.ToString(), elif.Span.End));
                    break;

                case ElseDirectiveTriviaSyntax elseDirective:
                    var previous = Close(elseDirective.SpanStart);
                    open.Push(($"!({previous})", elseDirective.Span.End));
                    break;

                case EndIfDirectiveTriviaSyntax endIf:
                    Close(endIf.SpanStart);
                    break;
            }
        }

        return scopes;

        string Close(int end)
        {
            if (open.Count == 0)
                return string.Empty;

            var (condition, start) = open.Pop();
            scopes.Add((TextSpan.FromBounds(start, Math.Max(start, end)), condition));
            return condition;
        }
    }

    /// <summary>
    /// Records with which polarity a condition tests each symbol: <c>UNITY_EDITOR &amp;&amp; !UNITY_SERVER</c>
    /// tests the first positively and the second negatively, and <c>!(A || B)</c> negates both. An
    /// equality comparison (<c>X == false</c>) is rare enough in package guards that its operands are
    /// simply counted as positive; a command lost to that goes through the hidden-command report, not
    /// through silence.
    /// </summary>
    private static void CollectPolarities(ExpressionSyntax condition, bool negated, ISet<string> positive, ISet<string> negative)
    {
        switch (condition)
        {
            case IdentifierNameSyntax identifier:
                (negated ? negative : positive).Add(identifier.Identifier.ValueText);
                break;

            case PrefixUnaryExpressionSyntax unary when unary.IsKind(SyntaxKind.LogicalNotExpression):
                CollectPolarities(unary.Operand, !negated, positive, negative);
                break;

            case ParenthesizedExpressionSyntax parenthesized:
                CollectPolarities(parenthesized.Expression, negated, positive, negative);
                break;

            case BinaryExpressionSyntax binary:
                CollectPolarities(binary.Left, negated, positive, negative);
                CollectPolarities(binary.Right, negated, positive, negative);
                break;
        }
    }

    /// <summary>Every preprocessor directive in a file, in source order. Directives live in trivia, not in the node tree.</summary>
    private static IEnumerable<DirectiveTriviaSyntax> Directives(SyntaxNode root) =>
        root.DescendantTrivia(descendIntoTrivia: true)
            .Where(trivia => trivia.HasStructure)
            .Select(trivia => trivia.GetStructure())
            .OfType<DirectiveTriviaSyntax>()
            .OrderBy(directive => directive.SpanStart);

    private static bool IsAttribute(AttributeSyntax attribute, string name)
    {
        var text = attribute.Name switch
        {
            QualifiedNameSyntax qualified => qualified.Right.ToString(),
            var other => other.ToString(),
        };

        return text == name || text == name + "Attribute";
    }

    /// <summary>
    /// The constructor argument at <paramref name="index"/>, which C# lets the caller write either
    /// positionally or as <c>name:</c>. Reading only the position would swap a command's name and
    /// description the day the package writes them the other way round.
    /// </summary>
    private static AttributeArgumentSyntax? ConstructorArgument(AttributeSyntax attribute, int index, string parameterName)
    {
        var arguments = attribute.ArgumentList?.Arguments;
        if (arguments is null)
            return null;

        var named = arguments.Value.FirstOrDefault(a => a.NameColon?.Name.Identifier.ValueText == parameterName);
        if (named is not null)
            return named;

        var positional = arguments.Value.Where(a => a.NameEquals is null && a.NameColon is null).ToList();
        return index < positional.Count ? positional[index] : null;
    }

    private static AttributeArgumentSyntax? NamedArgument(AttributeSyntax attribute, string name) =>
        attribute.ArgumentList?.Arguments.FirstOrDefault(a => a.NameEquals?.Name.Identifier.ValueText == name);

    private bool? NamedBool(AttributeSyntax attribute, string name)
    {
        var argument = NamedArgument(attribute, name);
        if (argument is null)
            return null;

        return constants.Evaluate(argument.Expression) as bool?
               ?? throw new UnsupportedExpressionException($"{name} is not a boolean constant: `{argument.Expression}`");
    }

    private string AsString(AttributeArgumentSyntax argument, string relativePath)
    {
        var value = constants.Evaluate(argument.Expression);
        return value as string
               ?? throw new UnsupportedExpressionException($"{relativePath}: expected a string constant, got `{argument.Expression}`");
    }
}
