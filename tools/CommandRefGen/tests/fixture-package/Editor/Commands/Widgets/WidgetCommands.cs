using Unity.Pipeline.Commands;

namespace Fixture.Editor.Commands
{
    // An unmapped category directory: the generator humanizes the name, places the section after
    // the known ones, and warns so the mapping can be extended on purpose.
    public static class WidgetCommands
    {
        [CliCommand("widget_poke", "Poke a widget.")]
        public static string WidgetPoke(
            [CliArg("id", "Widget id.", Required = true)] int id = -1) => "";
    }
}
