using System.Text;
using System.Text.RegularExpressions;

namespace CommandRefGen;

/// <summary>
/// Renders the whole reference file. Every line of the output comes from here: the header, the preamble
/// templates, the contents list, the categorised sections and the RuntimeOnly section. Nothing in the
/// output file is meant to be edited by hand.
/// </summary>
public sealed partial class MarkdownWriter(Annotations annotations, Categories categories, Action<string> warn)
{
    [GeneratedRegex(@"^UNITY_(?<major>\d+)_(?<minor>\d+)(_(?<patch>\d+))?_OR_NEWER$")]
    private static partial Regex UnityVersionSymbol();

    [GeneratedRegex(@"[^a-z0-9 -]")]
    private static partial Regex AnchorNoise();

    // Any spelling, because Substitute matches its placeholders exactly: {{Version}} and {{VERSION_2}}
    // are as unfilled as {{VERSON}} and would reach the file just as literally.
    [GeneratedRegex(@"\{\{[A-Za-z0-9_]+\}\}")]
    private static partial Regex UnresolvedPlaceholder();

    // <project>, <player> and friends are placeholders in the package's own prose, but a markdown
    // renderer reads them as HTML tags and drops them, leaving a sentence with a hole in it. Code spans
    // are matched too, only to be handed back untouched — a placeholder already inside one is safe, and
    // wrapping it again would split the span.
    [GeneratedRegex(@"(?<code>`[^`]*`)|<(?<placeholder>[A-Za-z][A-Za-z0-9 _-]*)>")]
    private static partial Regex AngleBracketPlaceholder();

    /// <summary>Builds the file contents for <paramref name="commands"/> at package <paramref name="version"/>.</summary>
    public string Render(IReadOnlyList<CommandInfo> commands, string version, string packageName, TemplateSet templates)
    {
        var listed = commands.Where(c => !c.RuntimeOnly).ToList();
        var runtimeOnly = commands.Where(c => c.RuntimeOnly).ToList();

        var sections = listed
            .GroupBy(c => categories.SectionFor(c.SourcePath))
            .OrderBy(g => categories.SortKey(g.Key))
            .ThenBy(g => g.Key, StringComparer.Ordinal)
            .ToList();

        foreach (var command in sections.Where(s => s.Key == Categories.Fallback).SelectMany(s => s))
            warn($"no section rule matches {command.SourcePath} (command '{command.Name}') — filed under \"{Categories.Fallback}\"");

        var titles = sections.Select(s => s.Key).ToList();
        if (runtimeOnly.Count > 0)
            titles.Add(Categories.RuntimeOnly);
        else
            warn($"no command sets RuntimeOnly — the preamble's link to \"{Categories.RuntimeOnly}\" has no target in this file");

        var builder = new StringBuilder();
        builder.Append(Substitute(templates.Header, version, packageName, listed.Count, runtimeOnly.Count));
        builder.AppendLine(ReferenceStamp.Format(packageName, version));
        builder.AppendLine();
        builder.Append(Substitute(templates.Preamble, version, packageName, listed.Count, runtimeOnly.Count));

        builder.AppendLine("## Contents");
        builder.AppendLine();
        builder.AppendLine(string.Join(" · ", titles.Select(t => $"[{t}](#{Anchor(t)})")));
        builder.AppendLine();

        foreach (var section in sections)
        {
            builder.AppendLine($"## {section.Key}");
            builder.AppendLine();
            foreach (var command in section.OrderBy(c => c.Name, StringComparer.Ordinal))
                AppendCommand(builder, command);
        }

        if (runtimeOnly.Count > 0)
        {
            builder.AppendLine($"## {Categories.RuntimeOnly}");
            builder.AppendLine();
            builder.Append(Substitute(templates.RuntimeOnlyIntro, version, packageName, listed.Count, runtimeOnly.Count));
            foreach (var command in runtimeOnly.OrderBy(c => c.Name, StringComparer.Ordinal))
                AppendCommand(builder, command);
        }

        // AppendLine follows the host platform; the reference is committed with LF endings everywhere.
        var markdown = builder.ToString().Replace("\r\n", "\n").TrimEnd() + "\n";

        foreach (var unresolved in UnresolvedPlaceholder().Matches(markdown).Select(m => m.Value).Distinct(StringComparer.Ordinal))
            warn($"the templates use {unresolved}, which this generator does not fill in — it reaches the file as written");

        return markdown;
    }

    private void AppendCommand(StringBuilder builder, CommandInfo command)
    {
        builder.AppendLine($"### {command.Name}");

        var description = command.Description;
        ReportIfOverlong(description, command.Name, command);

        if (annotations.Commands.TryGetValue(command.Name, out var note) && note.Length > 0)
            description = Join(description, note);

        var flags = new List<string>();
        if (!command.MainThreadRequired)
            flags.Add("works while main thread is busy");
        flags.AddRange(command.Gates.Select(DescribeGate));

        description = Renderable(description);
        builder.AppendLine(flags.Count > 0 ? $"{description} *({string.Join(", ", flags)})*" : description);

        if (command.Args.Count == 0)
            builder.AppendLine("- *(no arguments)*");
        else
            AppendArgs(builder, command.Args, command, string.Empty, indent: 0);

        builder.AppendLine();
    }

    /// <summary>
    /// Writes one bullet per argument, indenting the fields of a structured argument underneath it so a
    /// reader can see the shape of the JSON object the argument expects.
    /// </summary>
    private void AppendArgs(StringBuilder builder, IReadOnlyList<CommandArg> args, CommandInfo command, string path, int indent)
    {
        var margin = new string(' ', indent * 2);

        foreach (var arg in args)
        {
            ReportIfOverlong(arg.Description, $"{command.Name} {path}{arg.Name}", command);

            var required = arg.Required ? "\\*" : string.Empty;
            var defaultValue = arg.DefaultValue is null ? string.Empty : $" (default {arg.DefaultValue})";
            var text = arg.Description.Length > 0 ? $" — {Renderable(arg.Description)}" : string.Empty;

            // A generic type carries angle brackets, which a renderer reads as an HTML tag and drops —
            // "List" without its element type. Code makes them literal.
            var type = arg.Type.Contains('<') ? $"`{arg.Type}`" : arg.Type;
            builder.AppendLine($"{margin}- `{arg.Name}`{required} {type}{defaultValue}{text}");

            if (arg.Members.Count > 0)
                AppendArgs(builder, arg.Members, command, $"{path}{arg.Name}.", indent + 1);
        }
    }

    /// <summary>
    /// Descriptions are emitted in full whatever their length; past the threshold the generator only says
    /// so, because a wall of text in the sources is a problem to fix upstream, not text to cut here.
    /// </summary>
    private void ReportIfOverlong(string text, string subject, CommandInfo command)
    {
        if (text.Length > Prose.LongDescriptionThreshold)
            warn($"{subject} has a {text.Length}-character description ({command.SourcePath}:{command.SourceLine}) — emitted in full");
    }

    /// <summary>Appends a field note to a source description, supplying the sentence break the source omits.</summary>
    private static string Join(string description, string note)
    {
        if (description.Length == 0)
            return note;

        var separator = description[^1] is '.' or '!' or '?' or ':' ? " " : ". ";
        return description + separator + note;
    }

    /// <summary>Turns a preprocessor condition into the availability note printed next to the command.</summary>
    private string DescribeGate(string condition)
    {
        var symbol = condition.Trim();

        var unity = UnityVersionSymbol().Match(symbol);
        if (unity.Success)
        {
            var version = unity.Groups["patch"].Success
                ? $"{unity.Groups["major"].Value}.{unity.Groups["minor"].Value}.{unity.Groups["patch"].Value}"
                : $"{unity.Groups["major"].Value}.{unity.Groups["minor"].Value}";
            return $"Unity {version}+ only";
        }

        // Every gate the package uses today is a Unity version check. Anything else is printed as it
        // stands, with a warning, so that a new kind of gate is noticed and given a readable form here
        // rather than reaching the reference as jargon.
        warn($"preprocessor condition `{symbol}` has no readable form — printed verbatim");
        return $"compiled under {symbol}";
    }

    /// <summary>Keeps an angle-bracketed placeholder visible in a rendered page by making it code.</summary>
    private static string Renderable(string text) =>
        AngleBracketPlaceholder().Replace(
            text,
            match => match.Groups["code"].Success ? match.Value : $"`<{match.Groups["placeholder"].Value}>`");

    /// <summary>GitHub's heading anchor: lowercase, punctuation dropped, spaces to hyphens.</summary>
    private static string Anchor(string title) =>
        AnchorNoise().Replace(title.ToLowerInvariant(), string.Empty).Replace(' ', '-');

    private static string Substitute(string template, string version, string packageName, int listedCount, int runtimeOnlyCount) =>
        template
            .Replace("{{PACKAGE}}", packageName)
            .Replace("{{VERSION}}", version)
            .Replace("{{LISTED_COUNT}}", listedCount.ToString())
            .Replace("{{RUNTIME_ONLY_COUNT}}", runtimeOnlyCount.ToString())
            .Replace("{{TOTAL_COUNT}}", (listedCount + runtimeOnlyCount).ToString())
            .Replace("{{RUNTIME_ONLY_SECTION}}", Categories.RuntimeOnly)
            .Replace("{{RUNTIME_ONLY_ANCHOR}}", Anchor(Categories.RuntimeOnly));

    /// <summary>The hand-written prose blocks the generator splices into the output.</summary>
    public sealed record TemplateSet(string Header, string Preamble, string RuntimeOnlyIntro)
    {
        /// <summary>Loads the templates from <paramref name="directory"/>.</summary>
        public static TemplateSet Load(string directory) => new(
            Read(directory, "header.md"),
            Read(directory, "preamble.md"),
            Read(directory, "runtime-only-intro.md"));

        private static string Read(string directory, string name)
        {
            var path = Path.Combine(directory, name);
            if (!File.Exists(path))
                throw new FileNotFoundException($"missing template: {path}", path);

            // Normalise so the output has the same line endings on every platform.
            return File.ReadAllText(path).Replace("\r\n", "\n").TrimEnd() + "\n\n";
        }
    }
}
