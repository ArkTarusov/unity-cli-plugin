namespace CommandRefGen;

/// <summary>How a finished run is reported to whoever called it.</summary>
public static class ExitCode
{
    /// <summary>The run did what it was asked to do.</summary>
    public const int Success = 0;

    /// <summary>The run could not do it: bad arguments, an unreachable registry, unreadable sources.</summary>
    public const int Failure = 1;

    /// <summary><c>--check</c> found the reference out of date.</summary>
    public const int OutOfDate = 2;

    /// <summary><c>--strict</c> and the run warned.</summary>
    public const int Warned = 3;

    /// <summary>
    /// Applies <c>--strict</c> to a finished run. Every warning means the reference may be incomplete —
    /// a command filed nowhere, a type that could not be resolved — which a caller polling the exit code
    /// alone would not see. Only a run that would otherwise report success is overridden: a failure or an
    /// out-of-date file already says something more specific than "something warned".
    /// </summary>
    public static int Settle(int code, bool strict, int warnings) =>
        strict && warnings > 0 && code == Success ? Warned : code;
}
