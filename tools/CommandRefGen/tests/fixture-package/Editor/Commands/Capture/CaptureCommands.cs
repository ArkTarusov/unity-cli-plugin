using Unity.Pipeline.Commands;

namespace Fixture.Editor.Commands
{
    public static class CaptureCommands
    {
        // float.MinValue is the package's "leave Unity's own value alone" sentinel; the listing
        // renders it as -3.40282347e+38, and so must the generator.
        [CliCommand("capture_game_view", "Render a camera to a PNG.")]
        public static string CaptureGameView(
            [CliArg("width", "Output width in px.")] int width = 1280,
            [CliArg("height", "Output height in px.")] int height = 720,
            [CliArg("exposure", "Exposure override. Defaults to Unity's current value.")]
            float exposure = float.MinValue,
            [CliArg("save_path", "Project-relative PNG path.")] string savePath = null) => "";

#if UNITY_6000_7_OR_NEWER
        // Version-gated: the generator must mark the Unity floor instead of presenting this as
        // universally available. Note the argument name falls back to the C# parameter name.
        [CliCommand("capture_editor_element", "Capture a UI Toolkit VisualElement from an editor panel to a PNG.")]
        public static string CaptureEditorElement(
            [CliArg(Description = "Element selector: '#name', '.class', or a type name.")] string selector,
            [CliArg("output", "Output PNG path.")] string output = "") => "";
#endif

#if UNITY_6000_7_OR_NEWER && ENABLE_CAPTURE_PIPELINE
        // Two gates at once: a Unity floor plus a plain feature symbol.
        [CliCommand("capture_pipeline_debug", "Dump render-graph debug data for the active pipeline.")]
        public static string CapturePipelineDebug() => "";
#endif

        // A parameter with no [CliArg] is not part of the CLI surface; the generator leaves it
        // out and says so on stderr.
        [CliCommand("screenshot", "Write a screenshot of the focused window.", MainThreadRequired = false)]
        public static string Screenshot(
            [CliArg("path", "Output path.")] string path,
            System.Threading.CancellationToken cancellation = default) => path;
    }
}
