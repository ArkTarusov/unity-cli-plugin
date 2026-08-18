using Xunit;

namespace CommandRefGen.Tests;

/// <summary>
/// The exit code is the whole contract an automated caller sees, and `--strict` exists so that a warning
/// cannot hide behind a zero.
/// </summary>
public class ExitCodeTests
{
    [Fact]
    public void Leaves_a_clean_run_alone() =>
        Assert.Equal(ExitCode.Success, ExitCode.Settle(ExitCode.Success, strict: true, warnings: 0));

    [Fact]
    public void Leaves_warnings_alone_without_strict() =>
        Assert.Equal(ExitCode.Success, ExitCode.Settle(ExitCode.Success, strict: false, warnings: 3));

    [Fact]
    public void Turns_a_successful_run_that_warned_into_a_failure() =>
        Assert.Equal(ExitCode.Warned, ExitCode.Settle(ExitCode.Success, strict: true, warnings: 1));

    [Theory]
    [InlineData(ExitCode.Failure)]
    [InlineData(ExitCode.OutOfDate)]
    public void Keeps_a_code_that_already_says_something_more_specific(int code) =>
        Assert.Equal(code, ExitCode.Settle(code, strict: true, warnings: 1));
}
