using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace CommandRefGen;

/// <summary>
/// Reads `[CliCommand]` / `[CliArg]` declarations out of the package sources through the
/// Roslyn syntax tree. Attributes span multiple lines and defaults come from two places
/// (the attribute's DefaultValue, else the C# parameter default), which is why this is a
/// parse and not a set of regexes.
/// </summary>
internal sealed class SourceParser(int maxDescription)
{
    private static readonly Regex WhitespaceRun = new(@"\s+", RegexOptions.Compiled);
    private static readonly Regex UnityVersionSymbol =
        new(@"^UNITY_(\d+)(?:_(\d+))?(?:_(\d+))?_OR_NEWER$", RegexOptions.Compiled);
    private static readonly Regex Identifier =
        new(@"[A-Za-z_][A-Za-z0-9_]*", RegexOptions.Compiled);

    /// <summary>Parses every command in a package root, keyed by command name.</summary>
    public List<CommandInfo> Parse(string packageRoot)
    {
        var byName = new Dictionary<string, CommandInfo>(StringComparer.Ordinal);
        var files = 0;

        foreach (var file in EnumerateSources(packageRoot))
        {
            files++;
            var rel = Relative(packageRoot, file);
            var text = File.ReadAllText(file);

            var symbols = CollectPreprocessorSymbols(text);
            var regions = ConditionRegions(ParseTree(text, []));

            // Pass A defines every symbol seen in an #if, so version-gated commands
            // (`#if UNITY_6000_7_OR_NEWER`) land in the tree instead of in disabled text.
            foreach (var c in ReadCommands(ParseTree(text, symbols), rel, regions))
                Merge(byName, c);

            // Pass B defines nothing, which is the only way to reach `#else` and `#if !SYMBOL`
            // branches. Commands present in both passes are unconditional.
            if (symbols.Count > 0)
                foreach (var c in ReadCommands(ParseTree(text, []), rel, regions))
                    Merge(byName, c);
        }

        foreach (var fixture in FixtureCommands.Where(byName.ContainsKey))
            Log.Warn($"'{fixture}' is a test-assembly registration fixture but was found in " +
                     $"{byName[fixture].SourcePath}; check the Tests/ exclusion");

        Log.Info($"parsed {files} source file(s), found {byName.Count} [CliCommand] method(s)");
        return byName.Values.OrderBy(c => c.Name, StringComparer.Ordinal).ToList();
    }

    // ---- source discovery -------------------------------------------------

    private static IEnumerable<string> EnumerateSources(string root) =>
        Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
            .Where(f => !IsExcluded(root, f))
            .OrderBy(f => f, StringComparer.Ordinal);

    private static bool IsExcluded(string root, string file)
    {
        var dirs = Segments(Relative(root, file))[..^1];

        for (var i = 0; i < dirs.Length; i++)
        {
            // Trailing-~ directories are invisible to Unity's importer.
            if (dirs[i].EndsWith('~')) return true;

            // The package's Tests/ assembly holds registration fixtures (log_editor,
            // test_types, test_structured), not real commands. A `Commands/Tests/` category
            // directory is a different thing — that is where run_tests and friends live.
            if (!dirs[i].Equals("Tests", StringComparison.OrdinalIgnoreCase)) continue;
            if (i > 0 && dirs[i - 1].Equals("Commands", StringComparison.Ordinal)) continue;
            return true;
        }

        return false;
    }

    /// <summary>Registration fixtures from the package's test assembly; never real commands.</summary>
    private static readonly string[] FixtureCommands = ["log_editor", "test_types", "test_structured"];

    private static string Relative(string root, string file) =>
        Path.GetRelativePath(root, file).Replace('\\', '/');

    private static string[] Segments(string relPath) => relPath.Split('/');

    /// <summary>
    /// The category directory a source file sits in: the segment right after the last
    /// `Commands/` directory, e.g. `Editor/Commands/Scenes/SceneCommands.cs` -> `Scenes`.
    /// Null when the file sits directly in `Commands/` or outside one entirely.
    /// </summary>
    public static string? CategoryDirOf(string relPath)
    {
        var segs = Segments(relPath);
        for (var i = segs.Length - 2; i >= 0; i--)
        {
            if (!segs[i].Equals("Commands", StringComparison.Ordinal)) continue;
            return i + 1 <= segs.Length - 2 ? segs[i + 1] : null;
        }
        return null;
    }

    // ---- preprocessor -----------------------------------------------------

    private static SyntaxTree ParseTree(string text, IEnumerable<string> symbols) =>
        CSharpSyntaxTree.ParseText(text, new CSharpParseOptions(
            LanguageVersion.Latest, DocumentationMode.None, SourceCodeKind.Regular, symbols));

    private static IEnumerable<DirectiveTriviaSyntax> Directives(SyntaxTree tree)
    {
        var d = ((CSharpSyntaxNode)tree.GetRoot()).GetFirstDirective();
        while (d is not null)
        {
            yield return d;
            d = d.GetNextDirective();
        }
    }

    private static List<string> CollectPreprocessorSymbols(string text)
    {
        var names = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var d in Directives(ParseTree(text, [])))
        {
            var cond = d switch
            {
                IfDirectiveTriviaSyntax f => f.Condition,
                ElifDirectiveTriviaSyntax e => e.Condition,
                _ => null,
            };
            if (cond is null) continue;
            foreach (var id in cond.DescendantNodesAndSelf().OfType<IdentifierNameSyntax>())
                names.Add(id.Identifier.ValueText);
        }
        return names.ToList();
    }

    /// <summary>Text spans covered by each `#if`/`#elif`/`#else` branch, with its condition.</summary>
    private static List<(TextSpan Span, string Condition)> ConditionRegions(SyntaxTree tree)
    {
        var regions = new List<(TextSpan, string)>();
        var stack = new Stack<(int Start, string Condition)>();

        foreach (var d in Directives(tree))
        {
            switch (d)
            {
                case IfDirectiveTriviaSyntax f:
                    stack.Push((f.FullSpan.End, f.Condition.ToString().Trim()));
                    break;
                case ElifDirectiveTriviaSyntax e:
                    Close(e.FullSpan.Start);
                    stack.Push((e.FullSpan.End, e.Condition.ToString().Trim()));
                    break;
                case ElseDirectiveTriviaSyntax el:
                    var previous = Close(el.FullSpan.Start);
                    stack.Push((el.FullSpan.End, previous is null ? "else" : $"!({previous})"));
                    break;
                case EndIfDirectiveTriviaSyntax end:
                    Close(end.FullSpan.Start);
                    break;
            }
        }

        return regions;

        string? Close(int end)
        {
            if (stack.Count == 0) return null;
            var top = stack.Pop();
            regions.Add((TextSpan.FromBounds(top.Start, Math.Max(top.Start, end)), top.Condition));
            return top.Condition;
        }
    }

    /// <summary>
    /// Splits enclosing conditions into the highest Unity version gate and the rest.
    /// </summary>
    private static (string? MinUnityVersion, List<string> Other) ClassifyConditions(IEnumerable<string> conditions)
    {
        string? version = null;
        var other = new List<string>();

        foreach (var condition in conditions)
        {
            // `A && B` gates independently; anything with ! or || is reported verbatim.
            string[] parts = condition.Contains('!') || condition.Contains("||")
                ? [condition]
                : condition.Split("&&", StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

            foreach (var part in parts)
            {
                var m = UnityVersionSymbol.Match(part.Trim('(', ')', ' '));
                if (!m.Success)
                {
                    if (!other.Contains(part)) other.Add(part);
                    continue;
                }

                var v = string.Join('.', m.Groups.Cast<Group>().Skip(1).Where(g => g.Success).Select(g => g.Value));
                if (version is null || SemVer.Parse(v).CompareTo(SemVer.Parse(version)) > 0) version = v;
            }
        }

        return (version, other);
    }

    /// <summary>The Unity symbol a version string came from, for the generated note.</summary>
    public static string UnitySymbolFor(string version) =>
        "UNITY_" + version.Replace('.', '_') + "_OR_NEWER";

    // ---- command extraction ----------------------------------------------

    private IEnumerable<CommandInfo> ReadCommands(
        SyntaxTree tree, string relPath, List<(TextSpan Span, string Condition)> regions)
    {
        var category = CategoryDirOf(relPath);

        foreach (var method in tree.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>())
        {
            var attribute = Attribute(method.AttributeLists, "CliCommand");
            if (attribute is null) continue;

            var line = tree.GetLineSpan(method.Span).StartLinePosition.Line + 1;
            var where = $"{relPath}:{line}";
            var args = new AttributeArgs(attribute);

            if (!Literals.TryEvaluate(args.Get(0, "name"), out var nameValue)
                || nameValue is not string name || name.Length == 0)
            {
                Log.Warn($"{where}: [CliCommand] name is not a literal string; skipping this method");
                continue;
            }

            var (minUnity, conditions) = ClassifyConditions(
                regions.Where(r => r.Span.Contains(method.Span.Start)).Select(r => r.Condition));

            yield return new CommandInfo
            {
                Name = name,
                Description = Describe(args.Get(1, "description"), $"{where}: command '{name}'"),
                MainThreadRequired = Flag(args.Named("MainThreadRequired"), true, where, name),
                RuntimeOnly = Flag(args.Named("RuntimeOnly"), false, where, name),
                CategoryDir = category,
                MinUnityVersion = minUnity,
                Conditions = conditions,
                SourcePath = relPath,
                SourceLine = line,
                Args = ReadArgs(method, where, name),
            };
        }
    }

    private List<CliArgInfo> ReadArgs(MethodDeclarationSyntax method, string where, string command)
    {
        var result = new List<CliArgInfo>();
        var unannotated = new List<string>();

        foreach (var parameter in method.ParameterList.Parameters)
        {
            var attribute = Attribute(parameter.AttributeLists, "CliArg");
            if (attribute is null)
            {
                unannotated.Add(parameter.Identifier.ValueText);
                continue;
            }

            var args = new AttributeArgs(attribute);
            var name = Literals.TryEvaluate(args.Get(0, "name"), out var n) && n is string s && s.Length > 0
                ? s
                : parameter.Identifier.ValueText;

            // The attribute's DefaultValue wins; otherwise the C# parameter default applies.
            var defaultExpr = args.Named("DefaultValue") ?? parameter.Default?.Value;
            object? defaultValue = null;
            if (defaultExpr is not null)
            {
                if (defaultExpr.IsKind(SyntaxKind.DefaultLiteralExpression)
                    || defaultExpr is DefaultExpressionSyntax)
                {
                    defaultValue = parameter.Type is null ? null : Literals.DefaultOf(parameter.Type);
                }
                else if (!Literals.TryEvaluate(defaultExpr, out defaultValue))
                {
                    var text = WhitespaceRun.Replace(defaultExpr.ToString(), " ");
                    Log.Warn($"{where}: '{command}' argument '{name}' has a non-constant default `{text}`; " +
                             "emitting it verbatim");
                    defaultValue = new RawDefault(text);
                }
            }

            var required = Literals.TryEvaluate(args.Named("Required") ?? args.Named("IsRequired"), out var r)
                           && r is bool explicitly
                ? explicitly
                : defaultExpr is null;

            result.Add(new CliArgInfo
            {
                Name = name,
                Type = parameter.Type is null ? "Object" : Literals.FormatType(parameter.Type),
                Default = Literals.FormatDefault(defaultValue),
                Required = required,
                Description = Describe(args.Get(1, "description"), $"{where}: '{command}' argument '{name}'"),
            });
        }

        if (unannotated.Count > 0)
            Log.Warn($"{where}: '{command}' has parameter(s) without [CliArg]: " +
                     $"{string.Join(", ", unannotated)}; they are left out of the reference");

        return result;
    }

    /// <summary>A default the parser could not fold; rendered verbatim.</summary>
    private sealed record RawDefault(string Text)
    {
        public override string ToString() => Text;
    }

    private static AttributeSyntax? Attribute(SyntaxList<AttributeListSyntax> lists, string name) =>
        lists.SelectMany(l => l.Attributes).FirstOrDefault(a =>
        {
            var simple = Literals.SimpleName(a.Name) ?? "";
            if (simple.EndsWith("Attribute", StringComparison.Ordinal))
                simple = simple[..^"Attribute".Length];
            return simple.Equals(name, StringComparison.Ordinal);
        });

    private static bool Flag(ExpressionSyntax? expr, bool fallback, string where, string command)
    {
        if (expr is null) return fallback;
        if (Literals.TryEvaluate(expr, out var v) && v is bool b) return b;
        Log.Warn($"{where}: '{command}' has a non-constant flag `{expr}`; assuming {fallback}");
        return fallback;
    }

    private string Describe(ExpressionSyntax? expr, string what)
    {
        if (expr is null) return "";

        if (!Literals.TryEvaluate(expr, out var value) || value is not string text)
        {
            Log.Warn($"{what}: description is not a constant string; using its source text");
            text = expr.ToString();
        }

        // Collapse newlines and whitespace runs: a multi-line description must not break the
        // markdown list layout. Never shortened below the emergency ceiling.
        var description = WhitespaceRun.Replace(text, " ").Trim();

        if (description.Length > maxDescription)
        {
            Log.Warn($"{what}: description is {description.Length} chars, over the " +
                     $"{maxDescription}-char ceiling; truncated (raise --max-description to keep it whole)");
            description = description[..maxDescription].TrimEnd() + "…";
        }

        return description;
    }

    // ---- duplicate resolution --------------------------------------------

    private static void Merge(Dictionary<string, CommandInfo> map, CommandInfo command)
    {
        if (!map.TryGetValue(command.Name, out var existing))
        {
            map[command.Name] = command;
            return;
        }

        if (existing.SourcePath != command.SourcePath)
        {
            Log.Warn($"command '{command.Name}' is declared twice: " +
                     $"{existing.SourcePath}:{existing.SourceLine} and " +
                     $"{command.SourcePath}:{command.SourceLine}; keeping the first");
            return;
        }

        // Same declaration seen again by the second preprocessor pass.
        if (existing.SourceLine == command.SourceLine) return;

        // Two declarations in the same file means two preprocessor branches, so the command
        // exists whichever branch compiles: drop the gate rather than claim a version floor.
        if (Gates(command) == 0) map[command.Name] = command;
        else if (Gates(existing) > 0) map[command.Name] = existing with { MinUnityVersion = null, Conditions = [] };

        static int Gates(CommandInfo c) => c.Conditions.Count + (c.MinUnityVersion is null ? 0 : 1);
    }

    /// <summary>Positional and named attribute arguments, addressable either way.</summary>
    private sealed class AttributeArgs
    {
        private readonly List<ExpressionSyntax> _positional = [];
        private readonly Dictionary<string, ExpressionSyntax> _named = new(StringComparer.OrdinalIgnoreCase);

        public AttributeArgs(AttributeSyntax attribute)
        {
            if (attribute.ArgumentList is null) return;

            foreach (var argument in attribute.ArgumentList.Arguments)
            {
                var name = argument.NameEquals?.Name.Identifier.ValueText
                           ?? argument.NameColon?.Name.Identifier.ValueText;
                if (name is null) _positional.Add(argument.Expression);
                else _named[name] = argument.Expression;
            }
        }

        public ExpressionSyntax? Named(string name) => _named.GetValueOrDefault(name);

        public ExpressionSyntax? Get(int index, string name) =>
            Named(name) ?? (index < _positional.Count ? _positional[index] : null);
    }
}
