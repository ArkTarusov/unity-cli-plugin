using System.Text;

namespace CommandRefGen;

/// <summary>
/// Compares the generated document against the one already on disk and prints what moved:
/// added / removed / changed commands and arguments. The generator owns the file format, so
/// re-reading its own output is enough — no dump and no editor are needed for the comparison.
/// </summary>
internal static class DiffReporter
{
    private sealed record ParsedArg(string Name, string Type, string? Default, bool Required, string Description)
    {
        public string Signature => $"{Type}|{Default ?? "-"}|{(Required ? "req" : "opt")}";
    }

    private sealed record ParsedCommand(string Name, string Section, string Description, List<ParsedArg> Args);

    public static string Report(string? previousMarkdown, string generatedMarkdown, string path)
    {
        var generated = Parse(generatedMarkdown);

        if (previousMarkdown is null)
            return $"diff: {path} does not exist yet — all {generated.Count} commands are new.";

        var previous = Parse(previousMarkdown);
        var sb = new StringBuilder();
        sb.Append("diff against ").Append(path).Append(":\n");

        var added = generated.Keys.Except(previous.Keys, StringComparer.Ordinal)
            .OrderBy(k => k, StringComparer.Ordinal).ToList();
        var removed = previous.Keys.Except(generated.Keys, StringComparer.Ordinal)
            .OrderBy(k => k, StringComparer.Ordinal).ToList();
        var common = generated.Keys.Intersect(previous.Keys, StringComparer.Ordinal)
            .OrderBy(k => k, StringComparer.Ordinal).ToList();

        var changed = new List<string>();
        var unchanged = 0;

        foreach (var name in common)
        {
            var lines = CommandChanges(previous[name], generated[name]);
            if (lines.Count == 0)
            {
                unchanged++;
                continue;
            }

            changed.Add(name);
            sb.Append("  ~ ").Append(name).Append('\n');
            foreach (var line in lines) sb.Append("      ").Append(line).Append('\n');
        }

        foreach (var name in added)
            sb.Append("  + ").Append(name).Append("  [").Append(generated[name].Section)
              .Append("], ").Append(generated[name].Args.Count).Append(" argument(s)\n");

        foreach (var name in removed)
            sb.Append("  - ").Append(name).Append("  [was in ").Append(previous[name].Section).Append("]\n");

        sb.Append($"  {added.Count} added, {removed.Count} removed, {changed.Count} changed, {unchanged} unchanged")
          .Append($" ({previous.Count} -> {generated.Count} commands)");

        return sb.ToString();
    }

    private static List<string> CommandChanges(ParsedCommand before, ParsedCommand after)
    {
        var lines = new List<string>();

        if (!string.Equals(before.Description, after.Description, StringComparison.Ordinal))
            lines.Add("description changed");

        if (!string.Equals(before.Section, after.Section, StringComparison.Ordinal))
            lines.Add($"section {before.Section} -> {after.Section}");

        var beforeArgs = before.Args.ToDictionary(a => a.Name, StringComparer.Ordinal);
        var afterArgs = after.Args.ToDictionary(a => a.Name, StringComparer.Ordinal);

        foreach (var name in afterArgs.Keys.Except(beforeArgs.Keys, StringComparer.Ordinal)
                     .OrderBy(k => k, StringComparer.Ordinal))
            lines.Add($"+ argument `{name}` {Describe(afterArgs[name])}");

        foreach (var name in beforeArgs.Keys.Except(afterArgs.Keys, StringComparer.Ordinal)
                     .OrderBy(k => k, StringComparer.Ordinal))
            lines.Add($"- argument `{name}` {Describe(beforeArgs[name])}");

        foreach (var name in afterArgs.Keys.Intersect(beforeArgs.Keys, StringComparer.Ordinal)
                     .OrderBy(k => k, StringComparer.Ordinal))
        {
            var a = beforeArgs[name];
            var b = afterArgs[name];
            var deltas = new List<string>();

            if (a.Type != b.Type) deltas.Add($"type {a.Type} -> {b.Type}");
            if (a.Default != b.Default) deltas.Add($"default {a.Default ?? "(none)"} -> {b.Default ?? "(none)"}");
            if (a.Required != b.Required) deltas.Add(b.Required ? "now required" : "no longer required");
            if (!string.Equals(a.Description, b.Description, StringComparison.Ordinal)) deltas.Add("description changed");

            if (deltas.Count > 0) lines.Add($"~ argument `{name}`: {string.Join(", ", deltas)}");
        }

        return lines;
    }

    private static string Describe(ParsedArg arg)
    {
        var text = arg.Type;
        if (arg.Default is not null) text += $" (default {arg.Default})";
        return arg.Required ? text + ", required" : text;
    }

    /// <summary>Reads a reference document in this generator's own format.</summary>
    private static Dictionary<string, ParsedCommand> Parse(string markdown)
    {
        var commands = new Dictionary<string, ParsedCommand>(StringComparer.Ordinal);
        var section = "";
        ParsedCommand? current = null;
        var wantDescription = false;

        foreach (var raw in markdown.Split('\n'))
        {
            var line = raw.TrimEnd('\r');

            if (line.StartsWith("## ", StringComparison.Ordinal))
            {
                section = line[3..].Trim();
                current = null;
                wantDescription = false;
                continue;
            }

            if (line.StartsWith("### ", StringComparison.Ordinal))
            {
                var name = line[4..].Trim();
                current = new ParsedCommand(name, section, "", []);
                commands[name] = current;
                wantDescription = true;
                continue;
            }

            if (current is null) continue;

            if (wantDescription)
            {
                wantDescription = false;
                if (line.Length > 0 && !line.StartsWith("- ", StringComparison.Ordinal))
                {
                    commands[current.Name] = current = current with { Description = line.Trim() };
                    continue;
                }
            }

            if (line.StartsWith("- ", StringComparison.Ordinal) && ParseArg(line) is { } arg)
                current.Args.Add(arg);
        }

        return commands;
    }

    private static ParsedArg? ParseArg(string line)
    {
        var body = line[2..];
        if (!body.StartsWith('`')) return null; // "*(no arguments)*"

        var close = body.IndexOf('`', 1);
        if (close < 0) return null;

        var name = body[1..close];
        var rest = body[(close + 1)..];

        var required = rest.StartsWith("\\*", StringComparison.Ordinal);
        if (required) rest = rest[2..];
        rest = rest.TrimStart();

        var description = "";
        var dash = rest.IndexOf(" — ", StringComparison.Ordinal);
        if (dash >= 0)
        {
            description = rest[(dash + 3)..].Trim();
            rest = rest[..dash];
        }

        string? @default = null;
        const string marker = " (default ";
        var defaultAt = rest.IndexOf(marker, StringComparison.Ordinal);
        if (defaultAt >= 0 && rest.EndsWith(')'))
        {
            @default = rest[(defaultAt + marker.Length)..^1];
            rest = rest[..defaultAt];
        }

        return new ParsedArg(name, rest.Trim(), @default, required, description);
    }
}
