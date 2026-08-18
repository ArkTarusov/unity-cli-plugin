using System.Text.Json;

namespace CommandRefGen;

/// <summary>
/// Maps a command's source location to the reference section it is documented under. Only commands the
/// editor lists reach this; the rest go to <see cref="RuntimeOnly"/> whatever their source path.
///
/// The rules live in the categories.json sidecar next to the executable: a map of package-relative path
/// prefixes to section titles, longest match wins, so a file rule overrides the directory rule around
/// it. A source path matching no rule falls back to the name of its directory under the commands root
/// (<c>Editor/Commands/VFX/...</c> → "VFX"): a directory a future package version adds gets a usable
/// section without waiting for a rule edit. The derivation is reported through <paramref name="note"/>
/// so the title can be replaced with a deliberate one; it is not a warning, because a run that derives
/// a section is still correct and must keep passing in --strict. A root-level file matching no rule has
/// no directory to take a name from and lands in <see cref="Fallback"/>, which the writer warns about.
/// </summary>
public sealed class Categories(IReadOnlyDictionary<string, string> rules, IReadOnlyList<string> order, Action<string> note)
{
    /// <summary>Title of the section that receives commands with no matching rule and no directory to name one.</summary>
    public const string Fallback = "Other";

    /// <summary>Title of the trailing section for commands the editor hides from its listing.</summary>
    public const string RuntimeOnly = "RuntimeOnly commands (hidden from the listing)";

    private readonly HashSet<string> notedSections = new(StringComparer.Ordinal);

    /// <summary>Reads the sidecar. The file shapes the whole output, so its absence is an error, not a default.</summary>
    public static Categories Load(string path, Action<string> note)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException($"missing categories sidecar: {path}", path);

        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var root = document.RootElement;

        // JsonSerializer would keep the last of two identical keys and silently drop the first; read
        // the document by hand so a duplicated rule is an error instead.
        if (!root.TryGetProperty("rules", out var rulesElement))
            throw new InvalidOperationException($"{path}: no 'rules' object");

        var rules = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var rule in rulesElement.EnumerateObject())
        {
            var section = rule.Value.ValueKind == JsonValueKind.String ? rule.Value.GetString() : null;
            if (string.IsNullOrEmpty(section))
                throw new InvalidOperationException($"{path}: rule \"{rule.Name}\" does not name a section");

            if (!rules.TryAdd(rule.Name, section))
                throw new InvalidOperationException($"{path}: rule \"{rule.Name}\" is declared twice");
        }

        if (!root.TryGetProperty("order", out var orderElement))
            throw new InvalidOperationException($"{path}: no 'order' array");

        var order = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in orderElement.EnumerateArray())
        {
            var title = entry.ValueKind == JsonValueKind.String ? entry.GetString() : null;
            if (string.IsNullOrEmpty(title))
                throw new InvalidOperationException($"{path}: 'order' contains an entry that is not a section title");

            if (!seen.Add(title))
                throw new InvalidOperationException($"{path}: 'order' lists \"{title}\" twice");

            order.Add(title);
        }

        return new Categories(rules, order, note);
    }

    /// <summary>
    /// Section for a package-relative source path: the longest matching rule, else the directory name
    /// under the commands root, else <see cref="Fallback"/>.
    /// </summary>
    public string SectionFor(string sourcePath)
    {
        string? best = null;
        var bestLength = -1;
        foreach (var (prefix, section) in rules)
        {
            if (sourcePath.StartsWith(prefix, StringComparison.Ordinal) && prefix.Length > bestLength)
            {
                best = section;
                bestLength = prefix.Length;
            }
        }

        if (best is not null)
            return best;

        // Command sources always sit under <assembly>/Commands/ (the parser reads nowhere else), so a
        // fourth path segment means the file has a directory of its own to take a section name from.
        var segments = sourcePath.Split('/');
        if (segments.Length >= 4 && segments[1] == "Commands")
        {
            var derived = segments[2];
            if (notedSections.Add(derived))
                note($"no rule covers {segments[0]}/Commands/{derived}/ — section \"{derived}\" is named after the directory; add a rule to rename or regroup it");

            return derived;
        }

        return Fallback;
    }

    /// <summary>Sort key for a section title; titles absent from the configured order come after every listed one.</summary>
    public int SortKey(string section)
    {
        for (var i = 0; i < order.Count; i++)
        {
            if (string.Equals(order[i], section, StringComparison.Ordinal))
                return i;
        }

        return order.Count;
    }

    /// <summary>
    /// Reports rules whose prefix matches none of <paramref name="sourcePaths"/> — after a package
    /// update renames or removes a file, this is what says the sidecar needs the matching edit.
    /// </summary>
    public void ReportUnusedRules(IEnumerable<string> sourcePaths, Action<string> warn)
    {
        var paths = sourcePaths.ToList();
        foreach (var (prefix, section) in rules.OrderBy(r => r.Key, StringComparer.Ordinal))
        {
            if (!paths.Any(path => path.StartsWith(prefix, StringComparison.Ordinal)))
                warn($"categories.json rule \"{prefix}\" → \"{section}\" matches no command source in this package version");
        }
    }
}
