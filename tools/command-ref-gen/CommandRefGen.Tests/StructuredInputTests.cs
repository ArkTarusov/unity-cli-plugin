using Xunit;

namespace CommandRefGen.Tests;

/// <summary>
/// A structured argument is passed as a JSON object, and its fields exist nowhere in a live listing, so
/// what the reference prints for them cannot be checked against a running editor — only against the rules
/// the package's own schema generator applies.
/// </summary>
public class StructuredInputTests : IDisposable
{
    private readonly string packageRoot = Path.Combine(Path.GetTempPath(), "commandrefgen-tests", Guid.NewGuid().ToString("N"));
    private readonly List<string> warnings = new();

    [Fact]
    public void Takes_the_members_of_every_partial_part_and_of_the_base_type()
    {
        var members = Expand("""
            namespace A
            {
                public abstract class SettingsBase : IStructuredCommandInput
                {
                    [CliArg("inherited", "From the base")] public string Inherited { get; set; }
                }

                public partial class ThingInput : SettingsBase
                {
                    [CliArg("first", "First half")] public string First { get; set; }
                }

                public partial class ThingInput
                {
                    [CliArg("second", "Second half")] public string Second { get; set; }
                }
            }
            """);

        Assert.Equal(new[] { "first", "second", "inherited" }, members.Select(m => m.Name));
    }

    [Fact]
    public void Finds_the_marker_on_whichever_partial_part_declares_it()
    {
        // The parts of a partial type are that one type, so reflection sees the interface whichever part
        // carries it; stopping at the first part would drop the fields of a perfectly ordinary DTO.
        var members = Expand("""
            namespace A
            {
                public class ThingInput : SettingsBase
                {
                    [CliArg("own", "Declared here")] public string Own { get; set; }
                }

                public partial class SettingsBase
                {
                    [CliArg("first", "From the first part")] public string First { get; set; }
                }

                public partial class SettingsBase : IStructuredCommandInput
                {
                    [CliArg("second", "From the part carrying the marker")] public string Second { get; set; }
                }
            }
            """);

        Assert.Equal(new[] { "own", "first", "second" }, members.Select(m => m.Name));
    }

    [Fact]
    public void Leaves_an_abstract_type_unexpanded_as_the_schema_generator_does()
    {
        var members = Expand("""
            namespace A
            {
                public abstract class ThingInput : IStructuredCommandInput
                {
                    [CliArg("value", "A value")] public string Value { get; set; }
                }
            }
            """);

        Assert.Empty(members);
        Assert.Empty(warnings);
    }

    [Fact]
    public void Reports_a_name_it_cannot_resolve_rather_than_merging_two_types()
    {
        // Two unrelated types share a simple name, which is all a parameter gives this tool to go on.
        // Printing the union of their members would describe an object the server does not accept.
        var members = Expand("""
            namespace A
            {
                public class ThingInput : IStructuredCommandInput
                {
                    [CliArg("alpha", "From the real input")] public string Alpha { get; set; }
                }
            }

            namespace B
            {
                public class ThingInput
                {
                    [CliArg("beta", "From an unrelated type")] public string Beta { get; set; }
                }
            }
            """);

        Assert.Empty(members);
        Assert.Contains(warnings, w => w.Contains("ThingInput") && w.Contains("more than one namespace"));
    }

    [Fact]
    public void Reports_the_name_even_when_the_other_declaration_is_an_interface()
    {
        var members = Expand("""
            namespace A
            {
                public class ThingInput : IStructuredCommandInput
                {
                    [CliArg("alpha", "From the real input")] public string Alpha { get; set; }
                }
            }

            namespace B
            {
                public interface ThingInput { }
            }
            """);

        Assert.Empty(members);
        Assert.Contains(warnings, w => w.Contains("ThingInput"));
    }

    [Fact]
    public void Reports_a_base_type_whose_name_it_cannot_resolve()
    {
        // The argument's own type is unambiguous, so only the base name is in doubt — inheriting the
        // members of whichever same-named type came first would document fields of an unrelated class.
        var members = Expand("""
            namespace A
            {
                public class ThingInput : SettingsBase, IStructuredCommandInput
                {
                    [CliArg("own", "Declared here")] public string Own { get; set; }
                }

                public class SettingsBase
                {
                    [CliArg("fromA", "From the intended base")] public string FromA { get; set; }
                }
            }

            namespace B
            {
                public class SettingsBase
                {
                    [CliArg("fromB", "From an unrelated type")] public string FromB { get; set; }
                }
            }
            """);

        Assert.Equal(new[] { "own" }, members.Select(m => m.Name));
        Assert.Contains(warnings, w => w.Contains("SettingsBase") && w.Contains("more than one namespace"));
    }

    [Fact]
    public void Skips_the_members_the_schema_generator_skips()
    {
        var members = Expand("""
            namespace A
            {
                public class ThingInput : IStructuredCommandInput
                {
                    [CliArg("kept", "Kept")] public string Kept { get; set; }
                    [Newtonsoft.Json.JsonIgnore] public string Ignored { get; set; }
                    public string ReadOnlyMember { get; }
                    public const string Constant = "no";
                    public static string Shared { get; set; }
                    internal string Internal { get; set; }
                    public readonly string ReadOnlyField;
                }
            }
            """);

        Assert.Equal(new[] { "kept" }, members.Select(m => m.Name));
    }

    [Fact]
    public void Names_a_member_by_CliArg_then_JsonProperty_then_itself()
    {
        var members = Expand("""
            namespace A
            {
                public class ThingInput : IStructuredCommandInput
                {
                    [CliArg("fromCliArg", "One")]
                    [Newtonsoft.Json.JsonProperty("ignoredHere")]
                    public string First { get; set; }

                    [Newtonsoft.Json.JsonProperty(PropertyName = "fromJsonProperty")]
                    public string Second { get; set; }

                    public string Third { get; set; }
                }
            }
            """);

        Assert.Equal(new[] { "fromCliArg", "fromJsonProperty", "Third" }, members.Select(m => m.Name));
    }

    [Theory]
    [InlineData("LayerAssignment[]", "LayerAssignment[]")]
    [InlineData("List<LayerAssignment>", "List<LayerAssignment>")]
    [InlineData("IReadOnlyList<LayerAssignment>", "IReadOnlyList<LayerAssignment>")]
    public void Expands_a_collection_member_through_to_its_element_type(string declared, string printed)
    {
        // The schema generator emits an array of object schemas for any of these, so the reader has to
        // reach the element type the same way rather than stopping at the collection.
        var members = Expand($$"""
            namespace A
            {
                public class ThingInput : IStructuredCommandInput
                {
                    [CliArg("layers", "Layer assignments")] public {{declared}} Layers { get; set; }
                }

                public class LayerAssignment : IStructuredCommandInput
                {
                    [CliArg("index", "Layer index", Required = true)] public int Index { get; set; }
                }
            }
            """);

        var layers = Assert.Single(members);
        Assert.Equal(printed, layers.Type);
        Assert.Equal("index", Assert.Single(layers.Members).Name);
    }

    [Fact]
    public void Reports_the_whole_object_when_the_marker_sits_behind_an_unresolvable_base()
    {
        // The marker is reachable only through the ambiguous base, so the type cannot even be classified:
        // what goes missing is every field, not just the inherited ones, and the message has to say so.
        var members = Expand("""
            namespace A
            {
                public class ThingInput : SettingsBase
                {
                    [CliArg("own", "Declared here")] public string Own { get; set; }
                }

                public class SettingsBase : IStructuredCommandInput
                {
                    [CliArg("inherited", "From the base")] public string Inherited { get; set; }
                }
            }

            namespace B
            {
                public class SettingsBase { }
            }
            """);

        Assert.Empty(members);
        Assert.Contains(warnings, w => w.Contains("documented without its fields"));
        Assert.DoesNotContain(warnings, w => w.Contains("inherited from it"));
    }

    [Fact]
    public void Reports_a_structured_input_it_can_read_no_members_from()
    {
        // A positional record's properties exist only after compilation, so a syntax reader sees an empty
        // type where the server's reflection sees two fields.
        var members = Expand("""
            namespace A
            {
                public sealed record ThingInput(int Index, string Name) : IStructuredCommandInput;
            }
            """);

        Assert.Empty(members);
        Assert.Contains(warnings, w => w.Contains("no readable members"));
    }

    [Fact]
    public void Stops_where_a_type_contains_itself()
    {
        var members = Expand("""
            namespace A
            {
                public class ThingInput : IStructuredCommandInput
                {
                    [CliArg("child", "Nested")] public ThingInput Child { get; set; }
                }
            }
            """);

        Assert.Empty(Assert.Single(members).Members);
    }

    private IReadOnlyList<CommandArg> Expand(string types)
    {
        Write("Editor/Commands/Commands.cs", """
            public static class Commands
            {
                [CliCommand("set_thing", "Change a thing")]
                public static void Set([CliArg("settings", "Changes")] ThingInput settings = null) { }
            }
            """);
        Write("Runtime/Common/Inputs.cs", types);

        return Assert.Single(Assert.Single(new CommandParser(warnings.Add).Parse(packageRoot)).Args).Members;
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
