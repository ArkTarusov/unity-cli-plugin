using Xunit;

namespace CommandRefGen.Tests;

/// <summary>
/// The change summary is what makes a regeneration reviewable without reading the whole file diff, so a
/// change it fails to report is a change nobody looks at.
/// </summary>
public class ReferenceDiffTests
{
    // Normalised, because a raw string literal keeps the line endings of the file it is written in and
    // a checkout may hand them over as CRLF.
    private static readonly string Baseline = Lf("""
        ## Scenes

        ### open_scene
        Open a scene.
        - `path`\* String — Scene path.
        - `additive` Boolean (default false) — Load additively.

        ### save_scene
        Save the open scene.
        - *(no arguments)*
        """);

    [Fact]
    public void Reports_nothing_when_the_file_is_unchanged() =>
        Assert.Equal(string.Empty, Summarise(Baseline, Baseline));

    [Fact]
    public void Reports_an_added_command() =>
        Assert.Contains("+ close_scene", Summarise(Baseline, Baseline + "\n\n### close_scene\nClose it.\n- *(no arguments)*\n"));

    [Fact]
    public void Reports_a_removed_command() =>
        Assert.Contains("- save_scene", Summarise(Baseline, Baseline.Replace("### save_scene\nSave the open scene.\n- *(no arguments)*", "")));

    [Fact]
    public void Reports_a_command_that_moved_to_another_section()
    {
        var summary = Summarise(Baseline, Baseline.Replace("## Scenes", "## Assets & files"));

        Assert.Contains("section: \"Scenes\" -> \"Assets & files\"", summary);
    }

    [Fact]
    public void Reports_a_changed_default() =>
        Assert.Contains(
            "default false -> true",
            Summarise(Baseline, Baseline.Replace("(default false)", "(default true)")));

    [Fact]
    public void Reports_an_argument_that_became_required() =>
        Assert.Contains(
            "required False -> True",
            Summarise(Baseline, Baseline.Replace("`additive` Boolean", "`additive`\\* Boolean")));

    [Fact]
    public void Reports_a_field_of_a_structured_argument_by_its_path()
    {
        var before = "### set_tags_layers\nChange tags.\n- `settings` TagsLayersInput — Changes.\n  - `addTags` String[] — Tags to add.\n";
        var after = before + "  - `removeTags` String[] — Tags to remove.\n";

        Assert.Contains("+ arg settings.removeTags", Summarise(before, after));
    }

    [Fact]
    public void Reports_a_repeated_argument_instead_of_failing()
    {
        var warnings = new List<string>();
        var duplicated = "### log\nWrite a message.\n- `message`\\* String — Text.\n- `message`\\* String — Text.\n";

        var summary = ReferenceDiff.Summarise(duplicated, duplicated, warnings.Add);

        Assert.Equal(string.Empty, summary);
        Assert.Contains(warnings, w => w.Contains("message"));
    }

    [Fact]
    public void Counts_every_command_as_added_when_there_is_no_previous_file() =>
        Assert.Contains("2 added, 0 removed", ReferenceDiff.Summarise(null, Baseline, _ => { }));

    private static string Summarise(string before, string after) =>
        ReferenceDiff.Summarise(Lf(before), Lf(after), message => Assert.Fail($"unexpected warning: {message}"));

    private static string Lf(string text) => text.Replace("\r\n", "\n");
}
