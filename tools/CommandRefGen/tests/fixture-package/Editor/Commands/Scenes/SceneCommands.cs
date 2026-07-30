using Unity.Pipeline.Commands;

namespace Fixture.Editor.Commands
{
    public static class SceneCommands
    {
        // Baseline: attribute split over several lines, a required argument with no default,
        // and an optional one whose default comes from the C# parameter.
        [CliCommand(
            "open_scene",
            "Open a scene by asset path, replacing the current one.")]
        public static string OpenScene(
            [CliArg("path", "Project-relative scene path, e.g. Assets/Scenes/Main.unity")]
            string path,
            [CliArg("additive", "Load the scene additively instead of replacing the open one.")]
            bool additive = false)
        {
            return path + additive;
        }

        // The attribute's DefaultValue wins over the C# parameter default, and Required is set
        // explicitly on an argument that nonetheless has a default.
        [CliCommand("save_scene", "Save the open scene.")]
        public static string SaveScene(
            [CliArg("path", "Where to save. Defaults to the scene's current path.", DefaultValue = "")]
            string path = "unused",
            [CliArg("mode", "Save mode.", Required = true)] int mode = 0)
        {
            return path + mode;
        }

        // A verbatim, multi-line description must collapse to one line, and a description built
        // by concatenation must be folded rather than dumped as source text.
        [CliCommand("list_open_scenes", @"List every open scene.
             Reports name, path,
             and isDirty for each.")]
        public static string ListOpenScenes() => "";

        [CliCommand("set_active_scene", "Set the active scene. " + "Affects where new objects are created.")]
        public static string SetActiveScene(
            [CliArg("name", "Scene name.")] string name) => name;

        // MainThreadRequired = false, and an argument list carrying an array type.
        [CliCommand("scene_status", "Report scene load state.", MainThreadRequired = false)]
        public static string SceneStatus(
            [CliArg("paths", "Scene paths to report on. Omit for every open scene.")]
            string[] paths = null) => "";
    }
}
