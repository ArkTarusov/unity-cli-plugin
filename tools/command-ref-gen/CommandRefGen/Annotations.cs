using System.Text.Json;
using System.Text.Json.Serialization;

namespace CommandRefGen;

/// <summary>
/// Field knowledge that is true of a command but is not stated in the package sources — behaviour
/// observed while driving a real editor. It lives here, next to the generator, so that the reference
/// file itself stays fully generated and nothing has to be re-applied by hand after a regeneration.
/// </summary>
public sealed class Annotations
{
    /// <summary>Command name → extra markdown appended to the generated description.</summary>
    [JsonPropertyName("commands")]
    public Dictionary<string, string> Commands { get; init; } = new(StringComparer.Ordinal);

    /// <summary>Reads the sidecar, or returns an empty set when the file is absent.</summary>
    public static Annotations Load(string path, Action<string> warn)
    {
        if (!File.Exists(path))
        {
            warn($"{path}: annotations file not found — descriptions will carry no field notes");
            return new Annotations();
        }

        return JsonSerializer.Deserialize<Annotations>(File.ReadAllText(path))
               ?? throw new InvalidOperationException($"{path}: annotations file is empty");
    }

    /// <summary>Reports annotations that no longer match a command, so stale notes cannot rot unnoticed.</summary>
    public void ReportUnused(IEnumerable<string> commandNames, Action<string> warn)
    {
        var known = new HashSet<string>(commandNames, StringComparer.Ordinal);
        foreach (var name in Commands.Keys.Where(name => !known.Contains(name)).OrderBy(n => n, StringComparer.Ordinal))
            warn($"annotation for '{name}' matches no command in this package version");
    }
}
