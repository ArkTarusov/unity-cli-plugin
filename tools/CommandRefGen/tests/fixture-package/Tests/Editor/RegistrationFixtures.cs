using Unity.Pipeline.Commands;

namespace Fixture.Tests
{
    // The package's test assembly registers throwaway commands to prove registration works.
    // None of these may reach the reference document.
    public static class RegistrationFixtures
    {
        [CliCommand("log_editor", "Test fixture: log a line from the editor.")]
        public static string LogEditor(
            [CliArg("message", "Anything.")] string message = "hi") => message;

        [CliCommand("test_types", "Test fixture: echo one argument of every supported type.")]
        public static string TestTypes(
            [CliArg("i", "int")] int i = 0,
            [CliArg("f", "float")] float f = 0f,
            [CliArg("b", "bool")] bool b = false) => "";

        [CliCommand("test_structured", "Test fixture: return a structured payload.")]
        public static string TestStructured() => "";
    }
}
