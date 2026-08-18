using System.Text;
using System.Text.RegularExpressions;

namespace CommandRefGen;

/// <summary>
/// Compares a previously written reference file against a freshly generated one and reports what moved,
/// so a regeneration is reviewable without reading the whole diff: added and removed commands, changed
/// descriptions, and per-argument changes.
/// </summary>
public static partial class ReferenceDiff
{
    [GeneratedRegex(@"^### (?<name>\S+)\s*$")]
    private static partial Regex CommandHeading();

    [GeneratedRegex(@"^## (?<title>.+?)\s*$")]
    private static partial Regex SectionHeading();

    // The default value may itself contain a closing bracket, so the group only ends where the
    // description separator or the end of the line follows. Leading spaces mark a field of the
    // structured argument above.
    [GeneratedRegex(@"^(?<indent> *)- `(?<name>[^`]+)`(?<required>\\\*)? (?<type>\S+)(?<default> \(default (?<value>.*?)\)(?= — |$))?(?: — (?<description>.*))?$")]
    private static partial Regex ArgumentLine();

    private sealed record ParsedArg(string Name, string Type, bool Required, string? DefaultValue, string Description);

    private sealed record ParsedCommand(string Name, string Section, string Description, List<ParsedArg> Args);

    /// <summary>Renders a human-readable change summary; empty string when nothing changed.</summary>
    public static string Summarise(string? previousMarkdown, string currentMarkdown, Action<string> warn)
    {
        var before = previousMarkdown is null
            ? new Dictionary<string, ParsedCommand>(StringComparer.Ordinal)
            : Parse(previousMarkdown, warn);
        var after = Parse(currentMarkdown, warn);

        var added = after.Keys.Except(before.Keys, StringComparer.Ordinal).OrderBy(n => n, StringComparer.Ordinal).ToList();
        var removed = before.Keys.Except(after.Keys, StringComparer.Ordinal).OrderBy(n => n, StringComparer.Ordinal).ToList();
        var common = after.Keys.Intersect(before.Keys, StringComparer.Ordinal).OrderBy(n => n, StringComparer.Ordinal).ToList();

        var report = new StringBuilder();
        var changedCommands = 0;

        foreach (var name in added)
            report.Append($"+ {name} ({after[name].Args.Count} args)\n");
        foreach (var name in removed)
            report.Append($"- {name}\n");

        foreach (var name in common)
        {
            var lines = CompareCommand(before[name], after[name], warn);
            if (lines.Count == 0)
                continue;

            changedCommands++;
            report.Append($"~ {name}\n");
            foreach (var line in lines)
                report.Append($"    {line}\n");
        }

        if (report.Length == 0)
            return string.Empty;

        report.Append($"\n{added.Count} added, {removed.Count} removed, {changedCommands} changed"
                      + $" (was {before.Count} commands, now {after.Count})\n");
        return report.ToString();
    }

    private static List<string> CompareCommand(ParsedCommand before, ParsedCommand after, Action<string> warn)
    {
        var lines = new List<string>();

        if (!string.Equals(before.Section, after.Section, StringComparison.Ordinal))
            lines.Add($"section: \"{before.Section}\" -> \"{after.Section}\"");

        if (!string.Equals(before.Description, after.Description, StringComparison.Ordinal))
            lines.Add($"description: {Abbreviate(before.Description)} -> {Abbreviate(after.Description)}");

        var beforeArgs = ByName(before, warn);
        var afterArgs = ByName(after, warn);

        foreach (var name in afterArgs.Keys.Except(beforeArgs.Keys, StringComparer.Ordinal).OrderBy(n => n, StringComparer.Ordinal))
            lines.Add($"+ arg {name} ({afterArgs[name].Type})");
        foreach (var name in beforeArgs.Keys.Except(afterArgs.Keys, StringComparer.Ordinal).OrderBy(n => n, StringComparer.Ordinal))
            lines.Add($"- arg {name}");

        foreach (var name in afterArgs.Keys.Intersect(beforeArgs.Keys, StringComparer.Ordinal).OrderBy(n => n, StringComparer.Ordinal))
        {
            var oldArg = beforeArgs[name];
            var newArg = afterArgs[name];
            var changes = new List<string>();

            if (oldArg.Type != newArg.Type)
                changes.Add($"type {oldArg.Type} -> {newArg.Type}");
            if (oldArg.Required != newArg.Required)
                changes.Add($"required {oldArg.Required} -> {newArg.Required}");
            if (oldArg.DefaultValue != newArg.DefaultValue)
                changes.Add($"default {oldArg.DefaultValue ?? "(none)"} -> {newArg.DefaultValue ?? "(none)"}");
            if (!string.Equals(oldArg.Description, newArg.Description, StringComparison.Ordinal))
                changes.Add($"description {Abbreviate(oldArg.Description)} -> {Abbreviate(newArg.Description)}");

            if (changes.Count > 0)
                lines.Add($"~ arg {name}: {string.Join("; ", changes)}");
        }

        var beforeOrder = string.Join(",", before.Args.Select(a => a.Name));
        var afterOrder = string.Join(",", after.Args.Select(a => a.Name));
        if (beforeOrder != afterOrder && beforeArgs.Keys.ToHashSet(StringComparer.Ordinal).SetEquals(afterArgs.Keys))
            lines.Add($"argument order: {beforeOrder} -> {afterOrder}");

        return lines;
    }

    /// <summary>
    /// Indexes a command's arguments by name. A repeated name means the reference lists the same
    /// argument twice; that is worth reporting, but it must not stop the summary.
    /// </summary>
    private static Dictionary<string, ParsedArg> ByName(ParsedCommand command, Action<string> warn)
    {
        var byName = new Dictionary<string, ParsedArg>(StringComparer.Ordinal);

        foreach (var arg in command.Args.Where(arg => !byName.TryAdd(arg.Name, arg)))
            warn($"'{command.Name}' lists the argument '{arg.Name}' more than once — comparing the first occurrence");

        return byName;
    }

    /// <summary>
    /// Reads back the generator's own output format: <c>## section</c>, <c>### name</c>, a description
    /// line, argument bullets.
    /// </summary>
    private static Dictionary<string, ParsedCommand> Parse(string markdown, Action<string> warn)
    {
        var commands = new Dictionary<string, ParsedCommand>(StringComparer.Ordinal);
        var lines = markdown.Replace("\r\n", "\n").Split('\n');
        var section = string.Empty;

        for (var i = 0; i < lines.Length; i++)
        {
            var sectionHeading = SectionHeading().Match(lines[i]);
            if (sectionHeading.Success)
            {
                section = sectionHeading.Groups["title"].Value;
                continue;
            }

            var heading = CommandHeading().Match(lines[i]);
            if (!heading.Success)
                continue;

            var name = heading.Groups["name"].Value;
            var description = i + 1 < lines.Length ? lines[i + 1].Trim() : string.Empty;
            var args = new List<ParsedArg>();

            // Fields of a structured argument are indented under it; they are compared as dotted paths
            // (settings.addTags) so a change inside a nested object is reported like any other.
            var path = new List<string>();
            for (var j = i + 2; j < lines.Length && lines[j].TrimStart().StartsWith("- ", StringComparison.Ordinal); j++)
            {
                if (lines[j] == "- *(no arguments)*")
                    break;

                var argument = ArgumentLine().Match(lines[j]);
                if (!argument.Success)
                {
                    args.Add(new ParsedArg(lines[j].Trim(), "?", false, null, string.Empty));
                    continue;
                }

                var depth = argument.Groups["indent"].Value.Length / 2;
                path.RemoveRange(Math.Min(depth, path.Count), path.Count - Math.Min(depth, path.Count));
                path.Add(argument.Groups["name"].Value);

                args.Add(new ParsedArg(
                    string.Join('.', path),
                    argument.Groups["type"].Value,
                    argument.Groups["required"].Success,
                    argument.Groups["default"].Success ? argument.Groups["value"].Value : null,
                    argument.Groups["description"].Value));
            }

            if (!commands.TryAdd(name, new ParsedCommand(name, section, description, args)))
                warn($"the reference lists '{name}' more than once — comparing the first entry");
        }

        return commands;
    }

    private static string Abbreviate(string value) =>
        value.Length <= 60 ? $"\"{value}\"" : $"\"{value[..57]}...\"";
}
