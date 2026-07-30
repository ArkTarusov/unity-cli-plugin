using System.Text.Json;

namespace CommandRefGen;

/// <summary>
/// Field notes that are true but not derivable from the package source — observed behaviour,
/// cross-references to other skill files. They live outside the generated document so the
/// generator can own that file end to end; the generator merges them into a command's entry.
/// </summary>
internal sealed class Annotations
{
    private readonly Dictionary<string, string> _notes;

    private Annotations(Dictionary<string, string> notes) => _notes = notes;

    public static Annotations Empty { get; } = new([]);

    /// <summary>The note appended after a command's description, or "" when there is none.</summary>
    public string NoteFor(string command) => _notes.GetValueOrDefault(command, "");

    /// <summary>Names that no longer match a command — stale notes to clean up.</summary>
    public IEnumerable<string> Orphans(IEnumerable<string> commandNames)
    {
        var known = commandNames.ToHashSet(StringComparer.Ordinal);
        return _notes.Keys.Where(k => !known.Contains(k)).OrderBy(k => k, StringComparer.Ordinal);
    }

    public static Annotations Load(string path)
    {
        if (!File.Exists(path))
        {
            Log.Info($"annotations: {path} not found, continuing without field notes");
            return Empty;
        }

        using var doc = JsonDocument.Parse(File.ReadAllText(path), new JsonDocumentOptions
        {
            CommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
        });

        var notes = new Dictionary<string, string>(StringComparer.Ordinal);
        if (doc.RootElement.TryGetProperty("commands", out var commands)
            && commands.ValueKind == JsonValueKind.Object)
        {
            foreach (var entry in commands.EnumerateObject())
            {
                var note = entry.Value.ValueKind switch
                {
                    JsonValueKind.String => entry.Value.GetString(),
                    JsonValueKind.Object when entry.Value.TryGetProperty("note", out var n) => n.GetString(),
                    _ => null,
                };

                if (string.IsNullOrWhiteSpace(note))
                    Log.Warn($"annotations: '{entry.Name}' has no note text; ignoring it");
                else
                    notes[entry.Name] = note.Trim();
            }
        }

        Log.Info($"annotations: {notes.Count} field note(s) from {path}");
        return new Annotations(notes);
    }
}
