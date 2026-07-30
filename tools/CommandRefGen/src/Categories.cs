using System.Text;

namespace CommandRefGen;

/// <summary>
/// Turns a source directory under `Commands/` into a section title, and fixes the order
/// sections appear in. Only presentation lives here — which commands exist, and what they
/// do, always comes from the package source.
/// </summary>
internal static class Categories
{
    public const string RuntimeOnlyTitle = "RuntimeOnly commands (hidden from the listing)";
    private const string Fallback = "Other";

    /// <summary>Section order. Titles not listed here follow, sorted alphabetically.</summary>
    private static readonly string[] Order =
    [
        "Editor & play mode",
        "Capture",
        "Console & logs",
        "Scenes",
        "GameObjects & components",
        "Prefabs",
        "Assets & files",
        "Scripts & compilation",
        "Tests",
        "Build",
        "Packages (UPM)",
        "Materials & shaders",
        "Animation & Timeline",
        "Lighting bake",
        "NavMesh bake",
        "Occlusion bake",
        "Project settings",
    ];

    /// <summary>
    /// Directory name (normalized: lowercase, alphanumerics only) to section title.
    /// A directory that is not here still gets a section — its name is humanized and a
    /// warning names it, so the mapping can be extended deliberately.
    /// </summary>
    private static readonly Dictionary<string, string> Titles = new(StringComparer.Ordinal)
    {
        ["editor"] = "Editor & play mode",
        ["playmode"] = "Editor & play mode",
        ["editorplaymode"] = "Editor & play mode",
        ["capture"] = "Capture",
        ["captures"] = "Capture",
        ["screenshots"] = "Capture",
        ["console"] = "Console & logs",
        ["logs"] = "Console & logs",
        ["consolelogs"] = "Console & logs",
        ["scene"] = "Scenes",
        ["scenes"] = "Scenes",
        ["gameobject"] = "GameObjects & components",
        ["gameobjects"] = "GameObjects & components",
        ["components"] = "GameObjects & components",
        ["gameobjectscomponents"] = "GameObjects & components",
        ["hierarchy"] = "GameObjects & components",
        ["prefab"] = "Prefabs",
        ["prefabs"] = "Prefabs",
        ["asset"] = "Assets & files",
        ["assets"] = "Assets & files",
        ["files"] = "Assets & files",
        ["assetsfiles"] = "Assets & files",
        ["script"] = "Scripts & compilation",
        ["scripts"] = "Scripts & compilation",
        ["scripting"] = "Scripts & compilation",
        ["compilation"] = "Scripts & compilation",
        ["scriptscompilation"] = "Scripts & compilation",
        ["test"] = "Tests",
        ["tests"] = "Tests",
        ["testing"] = "Tests",
        ["build"] = "Build",
        ["builds"] = "Build",
        ["package"] = "Packages (UPM)",
        ["packages"] = "Packages (UPM)",
        ["upm"] = "Packages (UPM)",
        ["material"] = "Materials & shaders",
        ["materials"] = "Materials & shaders",
        ["shaders"] = "Materials & shaders",
        ["materialsshaders"] = "Materials & shaders",
        ["animation"] = "Animation & Timeline",
        ["animations"] = "Animation & Timeline",
        ["timeline"] = "Animation & Timeline",
        ["animationtimeline"] = "Animation & Timeline",
        ["lighting"] = "Lighting bake",
        ["lightingbake"] = "Lighting bake",
        ["navmesh"] = "NavMesh bake",
        ["navmeshbake"] = "NavMesh bake",
        ["navigation"] = "NavMesh bake",
        ["occlusion"] = "Occlusion bake",
        ["occlusionbake"] = "Occlusion bake",
        ["occlusionculling"] = "Occlusion bake",
        ["settings"] = "Project settings",
        ["projectsettings"] = "Project settings",
    };

    /// <summary>Section title for a category directory, warning once per unmapped directory.</summary>
    public static string TitleFor(string? directory)
    {
        if (string.IsNullOrEmpty(directory)) return Fallback;

        var key = Normalize(directory);
        if (Titles.TryGetValue(key, out var title)) return title;

        var humanized = Humanize(directory);
        if (Unmapped.Add(directory))
            Log.Warn($"category directory '{directory}' has no mapped section title; using " +
                     $"'{humanized}' and placing it after the known sections " +
                     "(add it to Categories.Titles to control the name and position)");
        return humanized;
    }

    private static readonly HashSet<string> Unmapped = new(StringComparer.Ordinal);

    /// <summary>Sort key: known sections in <see cref="Order"/>, then the rest alphabetically.</summary>
    public static (int Rank, string Title) SortKey(string title)
    {
        var index = Array.IndexOf(Order, title);
        return (index >= 0 ? index : Order.Length, title);
    }

    /// <summary>A GitHub-flavoured heading anchor, for the Contents list.</summary>
    public static string Anchor(string heading)
    {
        var sb = new StringBuilder(heading.Length);
        foreach (var ch in heading.ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(ch) || ch is '-' or '_') sb.Append(ch);
            else if (ch == ' ') sb.Append('-');
        }
        return sb.ToString();
    }

    private static string Normalize(string directory)
    {
        var sb = new StringBuilder(directory.Length);
        foreach (var ch in directory.ToLowerInvariant())
            if (char.IsLetterOrDigit(ch)) sb.Append(ch);
        return sb.ToString();
    }

    /// <summary>`AnimationTimeline` / `animation_timeline` -> `Animation timeline`.</summary>
    private static string Humanize(string directory)
    {
        var sb = new StringBuilder(directory.Length + 4);
        for (var i = 0; i < directory.Length; i++)
        {
            var ch = directory[i];
            if (ch is '_' or '-' or '.')
            {
                sb.Append(' ');
                continue;
            }

            var boundary = i > 0
                           && char.IsUpper(ch)
                           && (char.IsLower(directory[i - 1])
                               || (i + 1 < directory.Length && char.IsLower(directory[i + 1])));
            if (boundary) sb.Append(' ');
            sb.Append(sb.Length == 0 ? char.ToUpperInvariant(ch) : char.ToLowerInvariant(ch));
        }

        var text = sb.ToString().Trim();
        return text.Length == 0 ? Fallback : text;
    }
}
