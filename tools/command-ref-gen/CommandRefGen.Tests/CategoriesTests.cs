using Xunit;

namespace CommandRefGen.Tests;

public class CategoriesTests : IDisposable
{
    private readonly List<string> notes = new();
    private readonly string directory = Path.Combine(Path.GetTempPath(), "commandrefgen-tests", Guid.NewGuid().ToString("N"));

    private Categories Make(Dictionary<string, string>? rules = null, string[]? order = null) =>
        new(rules ?? new Dictionary<string, string>(), order ?? Array.Empty<string>(), notes.Add);

    [Fact]
    public void Applies_the_longest_matching_prefix()
    {
        var categories = Make(new Dictionary<string, string>
        {
            ["Editor/Commands/Scripts/"] = "Scripts & compilation",
            ["Editor/Commands/Scripts/SerializedFieldCommands.cs"] = "GameObjects & components",
        });

        Assert.Equal("GameObjects & components", categories.SectionFor("Editor/Commands/Scripts/SerializedFieldCommands.cs"));
        Assert.Equal("Scripts & compilation", categories.SectionFor("Editor/Commands/Scripts/CreateScriptCommand.cs"));
    }

    [Fact]
    public void Derives_the_section_from_the_directory_when_no_rule_matches()
    {
        Assert.Equal("VFX", Make().SectionFor("Editor/Commands/VFX/VfxCommands.cs"));
        Assert.Contains(notes, n => n.Contains("VFX"));
    }

    [Fact]
    public void Derives_from_the_directory_directly_under_the_commands_root()
    {
        Assert.Equal("Capture", Make().SectionFor("Editor/Commands/Capture/Overlays/OverlayCommands.cs"));
    }

    [Fact]
    public void Derives_a_section_for_a_runtime_directory_too()
    {
        Assert.Equal("Input", Make().SectionFor("Runtime/Commands/Input/RuntimeInputCommands.cs"));
    }

    [Fact]
    public void Notes_each_derived_section_once()
    {
        var categories = Make();
        categories.SectionFor("Editor/Commands/VFX/VfxCommands.cs");
        categories.SectionFor("Editor/Commands/VFX/VfxGraphCommands.cs");

        Assert.Single(notes);
    }

    [Fact]
    public void A_rule_beats_the_derived_name()
    {
        var categories = Make(new Dictionary<string, string>
        {
            ["Editor/Commands/Observability/"] = "Console & logs",
        });

        Assert.Equal("Console & logs", categories.SectionFor("Editor/Commands/Observability/ConsoleCommands.cs"));
        Assert.Empty(notes);
    }

    [Fact]
    public void Files_a_root_level_file_with_no_rule_under_the_fallback()
    {
        Assert.Equal(Categories.Fallback, Make().SectionFor("Editor/Commands/NewCommand.cs"));
        Assert.Equal(Categories.Fallback, Make().SectionFor("Runtime/Commands/NewCommand.cs"));
    }

    [Fact]
    public void Sorts_listed_titles_first_and_unknown_ones_after_them()
    {
        var categories = Make(order: new[] { "Capture", "Scenes" });

        Assert.True(categories.SortKey("Capture") < categories.SortKey("Scenes"));
        Assert.True(categories.SortKey("Scenes") < categories.SortKey("VFX"));
    }

    [Fact]
    public void Reports_a_rule_whose_prefix_matches_no_source()
    {
        var categories = Make(new Dictionary<string, string>
        {
            ["Editor/Commands/Scenes/"] = "Scenes",
            ["Editor/Commands/GoneCommand.cs"] = "Gone",
        });
        var warnings = new List<string>();

        categories.ReportUnusedRules(new[] { "Editor/Commands/Scenes/SceneCommands.cs" }, warnings.Add);

        Assert.Contains(warnings, w => w.Contains("GoneCommand.cs"));
        Assert.DoesNotContain(warnings, w => w.Contains("Scenes/"));
    }

    [Fact]
    public void Load_reads_rules_and_order_from_the_sidecar()
    {
        var path = WriteSidecar("""
            {
              "rules": { "Editor/Commands/Observability/": "Console & logs" },
              "order": [ "Console & logs" ]
            }
            """);

        var categories = Categories.Load(path, notes.Add);

        Assert.Equal("Console & logs", categories.SectionFor("Editor/Commands/Observability/ConsoleCommands.cs"));
        Assert.True(categories.SortKey("Console & logs") < categories.SortKey("VFX"));
    }

    [Fact]
    public void Load_rejects_a_missing_file() =>
        Assert.Throws<FileNotFoundException>(() => Categories.Load(Path.Combine(directory, "categories.json"), notes.Add));

    [Fact]
    public void Load_rejects_a_duplicated_rule()
    {
        // JsonSerializer would keep the last value and silently drop the first — a config error that
        // must surface, not vanish.
        var path = WriteSidecar("""
            {
              "rules": {
                "Editor/Commands/Scenes/": "Scenes",
                "Editor/Commands/Scenes/": "Levels"
              },
              "order": []
            }
            """);

        var exception = Assert.Throws<InvalidOperationException>(() => Categories.Load(path, notes.Add));
        Assert.Contains("Editor/Commands/Scenes/", exception.Message);
    }

    [Fact]
    public void Load_rejects_a_duplicated_order_entry()
    {
        var path = WriteSidecar("""
            {
              "rules": {},
              "order": [ "Scenes", "Scenes" ]
            }
            """);

        var exception = Assert.Throws<InvalidOperationException>(() => Categories.Load(path, notes.Add));
        Assert.Contains("Scenes", exception.Message);
    }

    private string WriteSidecar(string json)
    {
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "categories.json");
        File.WriteAllText(path, json);
        return path;
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // A scanner still holding a freshly written file must not fail a test that already passed.
        }
    }
}
