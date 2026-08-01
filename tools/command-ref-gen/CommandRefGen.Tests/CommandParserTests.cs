using Xunit;

namespace CommandRefGen.Tests;

/// <summary>
/// The parser decides every name, type, default and flag that reaches the reference. Running it against
/// the real package proves it agrees with that package, not that it applies the rules the package's
/// <c>CommandRegistry</c> applies — where the two happen to coincide today, only a written-out case can
/// tell them apart.
/// </summary>
public class CommandParserTests : IDisposable
{
    private readonly string packageRoot = Path.Combine(Path.GetTempPath(), "commandrefgen-tests", Guid.NewGuid().ToString("N"));
    private readonly List<string> warnings = new();

    [Fact]
    public void Reads_the_name_description_and_flags_from_the_attribute()
    {
        var command = Assert.Single(Parse("""
            [CliCommand("editor_play", "Enter play mode", MainThreadRequired = false)]
            public static void Play() { }
            """));

        Assert.Equal("editor_play", command.Name);
        Assert.Equal("Enter play mode", command.Description);
        Assert.False(command.MainThreadRequired);
        Assert.False(command.RuntimeOnly);
    }

    [Fact]
    public void Defaults_the_attribute_flags_the_way_the_attribute_declares_them()
    {
        var command = Assert.Single(Parse("""
            [CliCommand("editor_play", "Enter play mode")]
            public static void Play() { }
            """));

        Assert.True(command.MainThreadRequired);
        Assert.False(command.RuntimeOnly);
    }

    [Fact]
    public void Collapses_a_description_split_across_lines()
    {
        var command = Assert.Single(Parse("""
            [CliCommand("menu",
                "Execute a menu item, " +
                "or list the available ones")]
            public static void Menu() { }
            """));

        Assert.Equal("Execute a menu item, or list the available ones", command.Description);
    }

    [Fact]
    public void Prefers_the_csharp_default_over_the_one_on_the_attribute()
    {
        // CommandRegistry.DiscoverParameters reads param.DefaultValue first and only falls back to the
        // attribute, so a disagreement between the two must resolve towards the C# default.
        var command = Assert.Single(Parse("""
            [CliCommand("console", "Read the console")]
            public static void Console(
                [CliArg("tail", "How many entries", DefaultValue = 25)] int tail = 100) { }
            """));

        Assert.Equal("100", Assert.Single(command.Args).DefaultValue);
    }

    [Fact]
    public void Falls_back_to_the_attribute_default_when_the_parameter_has_none()
    {
        var command = Assert.Single(Parse("""
            [CliCommand("console", "Read the console")]
            public static void Console(
                [CliArg("level", "Minimum severity", DefaultValue = "warn")] string level) { }
            """));

        Assert.Equal("\"warn\"", Assert.Single(command.Args).DefaultValue);
    }

    [Fact]
    public void Marks_an_argument_required_exactly_as_the_server_would()
    {
        var args = Assert.Single(Parse("""
            [CliCommand("open_scene", "Open a scene")]
            public static void Open(
                [CliArg("path", "Scene path", Required = true)] string path = "",
                [CliArg("additive", "Load additively")] bool additive = false,
                int bare) { }
            """)).Args;

        // With an attribute the flag decides, even against a C# default; without one, having no C#
        // default is what makes the argument required.
        Assert.Equal(new[] { true, false, true }, args.Select(a => a.Required));
    }

    [Fact]
    public void Names_and_describes_an_unattributed_parameter_after_itself()
    {
        var arg = Assert.Single(Assert.Single(Parse("""
            [CliCommand("thing", "Do a thing")]
            public static void Thing(string somePath) { }
            """)).Args);

        Assert.Equal("somePath", arg.Name);
        Assert.Equal("Parameter: somePath", arg.Description);
    }

    [Theory]
    [InlineData("string", "String")]
    [InlineData("bool", "Boolean")]
    [InlineData("float", "Single")]
    [InlineData("int?", "Int32")]
    [InlineData("float[][]", "Single[][]")]
    [InlineData("ObjectRef", "ObjectRef")]
    public void Renders_the_declared_type_the_way_the_listing_does(string declared, string expected)
    {
        var arg = Assert.Single(Assert.Single(Parse($$"""
            [CliCommand("thing", "Do a thing")]
            public static void Thing([CliArg("value", "A value")] {{declared}} value) { }
            """)).Args);

        Assert.Equal(expected, arg.Type);
    }

    [Theory]
    [InlineData("int tail = default")]
    [InlineData("int tail = default(int)")]
    public void Refuses_a_default_that_names_no_value(string parameter)
    {
        // `default` parses as a literal whose token text is the word itself; folding it would print
        // (default "default") where the server reports 0.
        Assert.Throws<UnsupportedExpressionException>(() => Parse($$"""
            [CliCommand("console", "Read the console")]
            public static void Console([CliArg("tail", "How many entries")] {{parameter}}) { }
            """));
    }

    [Fact]
    public void Reads_a_name_and_description_written_as_named_arguments()
    {
        var command = Assert.Single(Parse("""
            [CliCommand(description: "Enter play mode", name: "editor_play")]
            public static void Play() { }
            """));

        Assert.Equal("editor_play", command.Name);
        Assert.Equal("Enter play mode", command.Description);
    }

    [Fact]
    public void Separates_the_commands_the_editor_hides_from_its_listing()
    {
        var commands = Parse("""
            [CliCommand("editor_play", "Enter play mode")]
            public static void Play() { }

            [CliCommand("quit", "Quit the application", RuntimeOnly = true)]
            public static void Quit() { }
            """);

        Assert.Equal(new[] { false, true }, commands.OrderBy(c => c.Name).Select(c => c.RuntimeOnly));
    }

    [Fact]
    public void Records_the_condition_a_command_is_compiled_under()
    {
        var command = Assert.Single(Parse("""
            #if UNITY_6000_7_OR_NEWER
            [CliCommand("capture_editor_element", "Capture an element")]
            public static void Capture() { }
            #endif
            """));

        Assert.Equal(new[] { "UNITY_6000_7_OR_NEWER" }, command.Gates);
    }

    [Fact]
    public void Reports_a_command_left_in_an_inactive_branch()
    {
        // Every symbol a file mentions is defined, so the #else body never reaches the tree; dropping it
        // without a word would take a command out of the reference invisibly.
        var commands = Parse("""
            #if UNITY_6000_7_OR_NEWER
            [CliCommand("modern", "New way")]
            public static void Modern() { }
            #else
            [CliCommand("legacy", "Old way")]
            public static void Legacy() { }
            #endif
            """);

        Assert.Equal("modern", Assert.Single(commands).Name);
        Assert.Contains(warnings, w => w.Contains("inactive conditional branch"));
    }

    [Fact]
    public void Skips_and_reports_a_non_static_command_method()
    {
        var commands = Parse("""
            [CliCommand("instance", "Never registered")]
            public void Instance() { }
            """);

        Assert.Empty(commands);
        Assert.Contains(warnings, w => w.Contains("non-static"));
    }

    [Fact]
    public void Reports_a_command_declared_outside_the_documented_roots()
    {
        var commands = Parse(
            "[CliCommand(\"editor_play\", \"Enter play mode\")] public static void Play() { }",
            strayPath: "Editor/Tools/StrayCommands.cs",
            stray: "[CliCommand(\"stray\", \"Somewhere else\")] public static void Stray() { }");

        Assert.Equal("editor_play", Assert.Single(commands).Name);
        Assert.Contains(warnings, w => w.Contains("Editor/Tools/StrayCommands.cs") && w.Contains("'Stray'"));
    }

    [Fact]
    public void Ignores_a_command_in_a_directory_unity_does_not_compile()
    {
        // Unity compiles nothing inside a directory whose name ends with '~', so a [CliCommand] there is
        // not part of the package: documenting it would advertise a command no editor answers to, and it
        // is not a stray to be reported either.
        var commands = Parse(
            "[CliCommand(\"editor_play\", \"Enter play mode\")] public static void Play() { }",
            strayPath: "Samples~/HotReload/SampleCommands.cs",
            stray: "[CliCommand(\"sample\", \"Only in a sample\")] public static void Sample() { }");

        Assert.Equal("editor_play", Assert.Single(commands).Name);
        Assert.Empty(warnings);
    }

    [Fact]
    public void Reads_a_command_from_a_directory_that_merely_happens_to_be_called_Tests()
    {
        // Only the package's own Tests assembly, at the root, is out of scope. A directory of that name
        // inside a compiled assembly is compiled like any other, so a command there is a real command.
        var commands = Parse(
            "[CliCommand(\"editor_play\", \"Enter play mode\")] public static void Play() { }",
            strayPath: "Editor/Commands/Tests/TestCommands.cs",
            stray: "[CliCommand(\"run_tests\", \"Run the tests\")] public static void Run() { }");

        Assert.Equal(new[] { "editor_play", "run_tests" }, commands.Select(c => c.Name).OrderBy(n => n, StringComparer.Ordinal));
        Assert.Empty(warnings);
    }

    [Fact]
    public void Reports_the_same_argument_declared_twice()
    {
        Parse("""
            [CliCommand("log", "Write a message")]
            public static void Log(
                [CliArg("message", "Text")] string message = "",
                [CliArg("message", "Text again")] string other = "") { }
            """);

        Assert.Contains(warnings, w => w.Contains("'message'"));
    }

    [Fact]
    public void Expands_a_structured_argument_into_the_fields_its_schema_advertises()
    {
        var settings = Assert.Single(Assert.Single(Parse(
            """
            [CliCommand("set_tags_layers", "Change tags")]
            public static void Set([CliArg("settings", "Changes")] TagsLayersInput settings = null) { }
            """,
            strayPath: "Runtime/Common/Inputs.cs",
            stray: """
            public class TagsLayersInput : IStructuredCommandInput
            {
                [CliArg("addTags", "Tags to add")] public string[] AddTags { get; set; }
                [Newtonsoft.Json.JsonProperty(PropertyName = "renamed")] public int Renamed { get; set; }
                [Newtonsoft.Json.JsonIgnore] public int Hidden { get; set; }
                public int ReadOnlyMember { get; }
                [CliArg("layers", "Layer assignments")] public LayerAssignment[] Layers { get; set; }
            }

            public class LayerAssignment : IStructuredCommandInput
            {
                [CliArg("index", "Layer index", Required = true)] public int Index { get; set; }
            }
            """)).Args);

        Assert.Equal(new[] { "addTags", "renamed", "layers" }, settings.Members.Select(m => m.Name));
        Assert.Equal("index", Assert.Single(settings.Members.Last().Members).Name);
        Assert.True(settings.Members.Last().Members.Single().Required);
    }

    private List<CommandInfo> Parse(string body, string? strayPath = null, string? stray = null)
    {
        Write("Editor/Commands/Commands.cs", $"public static class Commands {{\n{body}\n}}");
        if (strayPath is not null && stray is not null)
            Write(strayPath, stray.Contains("class ") ? stray : $"public static class Stray {{\n{stray}\n}}");

        return new CommandParser(warnings.Add).Parse(packageRoot);
    }

    private void Write(string relativePath, string contents)
    {
        var path = Path.Combine(packageRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, contents);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(packageRoot))
                Directory.Delete(packageRoot, recursive: true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // A scanner still holding a freshly written file must not fail a test that already passed.
        }
    }
}
