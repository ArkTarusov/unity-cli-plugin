using Unity.Pipeline.Commands;

namespace Fixture.Runtime.Commands
{
    public static class InputCommands
    {
        // RuntimeOnly commands are filtered out of the live listing, so they get their own
        // section rather than their category's.
        [CliCommand("simulate_key", "Simulate a keyboard key event (Input System).", RuntimeOnly = true)]
        public static string SimulateKey(
            [CliArg("key", "Input System Key name, e.g. Space, W, Enter, LeftArrow")] string key,
            [CliArg("action", "down | up | press (down+up)")] string action = "press") => "";

        [CliCommand("quit", "Gracefully quit the Unity application.", RuntimeOnly = true)]
        public static string Quit(
            [CliArg(Description = "Exit code for the application")] int exitCode = 0) => "";

        // A RuntimeOnly command that is not a Unity version gate but a feature-symbol gate.
#if ENABLE_INPUT_SYSTEM
        [CliCommand("simulate_pointer", "Simulate a mouse/pointer event at screen coordinates (Input System).",
            RuntimeOnly = true)]
        public static string SimulatePointer(
            [CliArg("x", "Screen X in pixels (origin bottom-left)")] float x,
            [CliArg("y", "Screen Y in pixels (origin bottom-left)")] float y,
            [CliArg("action", "move | down | up | click (down+up)")] string action = "click") => "";
#endif

        // Declared in both preprocessor branches, so it exists either way and must not be
        // reported as gated.
#if UNITY_6000_7_OR_NEWER
        [CliCommand("set_timescale", "Set the time scale for the application.", RuntimeOnly = true)]
        public static string SetTimeScaleModern(
            [CliArg("scale", "Time scale multiplier (0.0 to pause, 1.0 for normal speed)")] float scale) => "";
#else
        [CliCommand("set_timescale", "Set the time scale for the application.", RuntimeOnly = true)]
        public static string SetTimeScaleLegacy(
            [CliArg("scale", "Time scale multiplier (0.0 to pause, 1.0 for normal speed)")] float scale) => "";
#endif
    }
}
