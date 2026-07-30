namespace CommandRefGen;

/// <summary>Thrown for bad command lines; the message is printed without a stack trace.</summary>
internal sealed class UsageException(string message) : Exception(message);

internal sealed class Options
{
    public const string DefaultRegistry = "https://packages.unity.com";
    public const string DefaultPackage = "com.unity.pipeline";
    public const string DefaultOutput = "skills/unity-pipeline/references/editor-commands.md";
    public const string DefaultAnnotations = "tools/CommandRefGen/annotations.json";

    public string Registry { get; private set; } = DefaultRegistry;
    public string Package { get; private set; } = DefaultPackage;

    /// <summary>Package version to document, or "latest" for the highest entry in the registry.</summary>
    public string Version { get; private set; } = "latest";

    public string? Output { get; private set; }
    public string? Annotations { get; private set; }

    /// <summary>Offline input: a local package tarball instead of a registry download.</summary>
    public string? Tarball { get; private set; }

    /// <summary>Offline input: an already-unpacked package directory.</summary>
    public string? SourceDir { get; private set; }

    /// <summary>Report the diff but do not write; exit 3 when the output would change.</summary>
    public bool Check { get; private set; }

    /// <summary>Write the document to stdout instead of the output file.</summary>
    public bool ToStdout { get; private set; }

    public bool KeepTemp { get; private set; }

    /// <summary>
    /// Emergency ceiling on a description, in characters. Descriptions are never silently
    /// truncated: hitting this prints a warning naming the command or argument.
    /// </summary>
    public int MaxDescription { get; private set; } = 1000;

    public static string Usage => """
        CommandRefGen — generates the Unity Pipeline command reference from package sources.

        Usage:
          dotnet run --project tools/CommandRefGen -- [options]

        Input (pick one; the registry is the default):
          --version <v|latest>   Package version to document. "latest" resolves to the highest
                                 version in the registry listing. Default: latest.
          --tarball <path>       Read a local package tarball (.tgz) instead of downloading.
          --source-dir <path>    Read an already-unpacked package directory.
          --package <id>         Package id. Default: com.unity.pipeline.
          --registry <url>       UPM registry base URL. Default: https://packages.unity.com.

        Output:
          --output <path>        Markdown file to own. Default: skills/unity-pipeline/references/editor-commands.md.
          --stdout               Write the document to stdout; leave the output file alone.
          --check                Do not write. Exit 3 if the output file is out of date.
          --annotations <path>   Field notes merged into descriptions.
                                 Default: tools/CommandRefGen/annotations.json (skipped if absent).
          --max-description <n>  Emergency description ceiling in characters. Default: 1000.
          --keep-temp            Keep the unpacked package directory and print its path.
          -h, --help             Print this help.

        Exit codes: 0 ok · 1 error · 3 --check found the output file out of date.
        """;

    public static Options Parse(string[] args)
    {
        var o = new Options();
        var sawVersion = false;

        for (var i = 0; i < args.Length; i++)
        {
            var a = args[i];
            string Next(string name) => ++i < args.Length
                ? args[i]
                : throw new UsageException($"{name} needs a value");

            switch (a)
            {
                case "--version": o.Version = Next(a); sawVersion = true; break;
                case "--package": o.Package = Next(a); break;
                case "--registry": o.Registry = Next(a).TrimEnd('/'); break;
                case "--output": o.Output = Next(a); break;
                case "--annotations": o.Annotations = Next(a); break;
                case "--tarball": o.Tarball = Next(a); break;
                case "--source-dir": o.SourceDir = Next(a); break;
                case "--check": o.Check = true; break;
                case "--stdout": o.ToStdout = true; break;
                case "--keep-temp": o.KeepTemp = true; break;
                case "--max-description":
                    var raw = Next(a);
                    if (!int.TryParse(raw, out var max) || max < 1)
                        throw new UsageException($"--max-description needs a positive integer, got '{raw}'");
                    o.MaxDescription = max;
                    break;
                case "-h" or "--help":
                    throw new UsageException("");
                default:
                    throw new UsageException($"unknown option '{a}'");
            }
        }

        if (o.Tarball is not null && o.SourceDir is not null)
            throw new UsageException("--tarball and --source-dir are mutually exclusive");

        // Offline inputs carry their own version in package.json; an explicit --version would
        // silently mislabel the document, so only accept one when it is not the default.
        if (!sawVersion && (o.Tarball is not null || o.SourceDir is not null))
            o.Version = "";

        return o;
    }
}
