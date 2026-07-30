using Unity.Pipeline.Commands;

namespace Fixture.Editor.Commands
{
    // A `Commands/Tests/` directory is a category of real commands (run_tests and friends) and
    // must survive the Tests/ exclusion, which only targets the package's test assembly.
    public static class TestRunCommands
    {
        [CliCommand("run_tests", "Run the project's tests. Async: poll test_status.")]
        public static string RunTests(
            [CliArg("mode", "editmode | playmode")] string mode = "editmode",
            [CliArg("filter", "Test name filter.")] string filter = "") => "";

        [CliCommand("test_status", "Report the state of the last test run.", MainThreadRequired = false)]
        public static string TestStatus() => "";
    }
}
