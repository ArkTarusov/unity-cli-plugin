namespace CommandRefGen;

/// <summary>
/// Diagnostics go to stderr so `--stdout` stays a clean document.
/// </summary>
internal static class Log
{
    private static readonly HashSet<string> Warnings = new(StringComparer.Ordinal);

    public static int WarningCount => Warnings.Count;

    public static void Info(string message) => Console.Error.WriteLine(message);

    /// <summary>
    /// Warns once per distinct message: a file is parsed twice (once per preprocessor pass),
    /// so the same finding is reached twice.
    /// </summary>
    public static void Warn(string message)
    {
        if (!Warnings.Add(message)) return;
        Console.Error.WriteLine($"warning: {message}");
    }

    public static void Summary()
    {
        if (Warnings.Count > 0)
            Console.Error.WriteLine($"{Warnings.Count} warning(s)");
    }
}
