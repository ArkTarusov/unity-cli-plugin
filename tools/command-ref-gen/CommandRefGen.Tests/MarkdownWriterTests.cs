using Xunit;

namespace CommandRefGen.Tests;

/// <summary>
/// The writer's output is read both as raw markdown by an agent and as a rendered page by a person, and
/// the two disagree about what plain text is. These cases pin the places where that matters.
/// </summary>
public class MarkdownWriterTests
{
    private readonly List<string> warnings = new();

    [Fact]
    public void Makes_an_angle_bracketed_placeholder_survive_rendering()
    {
        // A markdown renderer reads <project> as an HTML tag and drops it, leaving a path with no root.
        var markdown = Render("Writes under <project>/Temp/screenshots.");

        Assert.Contains("Writes under `<project>`/Temp/screenshots.", markdown);
    }

    [Fact]
    public void Leaves_a_placeholder_that_already_sits_in_a_code_span()
    {
        // Wrapping it again would close the surrounding span early and break the rest of the line.
        var description = "Run `unity command --runtime <player> --json` first.";

        Assert.Contains(description, Render(description));
    }

    [Fact]
    public void Marks_up_a_placeholder_outside_a_code_span_while_leaving_the_span_alone()
    {
        var markdown = Render("Run `unity --runtime <player>` and write under <project>/Temp.");

        Assert.Contains("Run `unity --runtime <player>` and write under `<project>`/Temp.", markdown);
    }

    [Fact]
    public void Makes_a_generic_type_survive_rendering()
    {
        // A renderer reads <String> as an HTML tag and drops it, leaving the collection without its
        // element type.
        var markdown = Render("Nothing special.", argumentType: "List<String>");

        Assert.Contains("`output` `List<String>`", markdown);
    }

    [Fact]
    public void Leaves_a_plain_type_unquoted()
    {
        Assert.Contains("`output` String", Render("Nothing special."));
    }

    [Theory]
    [InlineData("{{VERSON}}")]
    [InlineData("{{Version}}")]
    [InlineData("{{package}}")]
    public void Reports_a_placeholder_the_templates_spell_wrong(string placeholder)
    {
        // Substitute matches its placeholders exactly, so a wrong spelling and a wrong case are equally
        // unfilled and would reach the committed file just as literally.
        Render("Nothing special.", preamble: $"Version {placeholder}.");

        Assert.Contains(warnings, w => w.Contains(placeholder));
    }

    [Fact]
    public void Reports_a_file_whose_preamble_links_to_a_section_it_will_not_contain()
    {
        Render("Nothing special.");

        Assert.Contains(warnings, w => w.Contains("no target"));
    }

    private string Render(string description, string preamble = "# Title", string argumentType = "String")
    {
        var command = new CommandInfo(
            "screenshot",
            description,
            MainThreadRequired: true,
            RuntimeOnly: false,
            Args: new[] { new CommandArg("output", description, argumentType, Required: false, DefaultValue: null, Array.Empty<CommandArg>()) },
            Gates: Array.Empty<string>(),
            SourcePath: "Editor/Commands/ScreenshotCommand.cs",
            SourceLine: 1);

        var templates = new MarkdownWriter.TemplateSet("<!-- header -->", preamble, "Runtime intro.");
        var categories = new Categories(
            new Dictionary<string, string> { ["Editor/Commands/ScreenshotCommand.cs"] = "Capture" },
            new[] { "Capture" },
            warnings.Add);
        return new MarkdownWriter(new Annotations(), categories, warnings.Add)
            .Render(new[] { command }, "0.4.0-exp.1", "com.unity.pipeline", templates);
    }
}
