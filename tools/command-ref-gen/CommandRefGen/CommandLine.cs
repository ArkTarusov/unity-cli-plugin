namespace CommandRefGen;

/// <summary>Parsed command line for the generator.</summary>
internal sealed record Options(
    string Version,
    string? Output,
    string? Package,
    string? Registry,
    bool Check,
    bool KeepSources,
    bool Strict);

internal static class CommandLine
{
    /// <summary>Returns null when the caller asked for usage; throws when the command line is unusable.</summary>
    public static Options? Parse(string[] args)
    {
        string? version = null, output = null, package = null, registry = null;
        var check = false;
        var keepSources = false;
        var strict = false;

        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--version":
                    version = Next(args, ref i);
                    break;
                case "--output":
                    output = Next(args, ref i);
                    break;
                case "--package":
                    package = Next(args, ref i);
                    break;
                case "--registry":
                    registry = Next(args, ref i);
                    break;
                case "--check":
                    check = true;
                    break;
                case "--keep-sources":
                    keepSources = true;
                    break;
                case "--strict":
                    strict = true;
                    break;
                case "--help":
                case "-h":
                    return null;
                default:
                    throw new ArgumentException($"unknown argument '{args[i]}'");
            }
        }

        // An empty command line is a caller that meant to pass arguments and passed none — reporting
        // success there would let a script believe the reference was checked when nothing ran.
        if (version is null)
            throw new ArgumentException("--version is required: a published version, 'latest', or 'current'");

        return new Options(version, output, package, registry, check, keepSources, strict);
    }

    public static void PrintUsage()
    {
        Console.WriteLine("""
            Regenerates the editor command reference from the com.unity.pipeline package sources.

              dotnet run --project tools/command-ref-gen/CommandRefGen -- --version latest

              --version <v|latest|current>
                                    package version to document. 'latest' picks the highest published
                                    version; 'current' reuses the version the output file records
              --output <path>       file to write (default: skills/unity-pipeline/references/editor-commands.md)
              --package <name>      UPM package name (default: com.unity.pipeline)
              --registry <url>      registry base URL (default: https://packages.unity.com)
              --check               report the diff and exit 2 if the file is out of date, writing nothing
              --keep-sources        leave the unpacked package in the temp directory
              --strict              turn a successful run that warned into exit 3; 1 and 2 keep their
                                    meaning, since they say something more specific
            """);
    }

    private static string Next(string[] args, ref int index)
    {
        if (index + 1 >= args.Length)
            throw new ArgumentException($"{args[index]} needs a value");

        return args[++index];
    }
}
