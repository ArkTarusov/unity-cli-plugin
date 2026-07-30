using System.Text;

namespace CommandRefGen;

/// <summary>
/// Generates skills/unity-pipeline/references/editor-commands.md from the com.unity.pipeline
/// package sources on the public UPM registry. No Unity Editor, no `unity --json command` dump.
/// </summary>
internal static class Program
{
    private const int ExitError = 1;
    private const int ExitOutOfDate = 3;

    public static async Task<int> Main(string[] args)
    {
        Options options;
        try
        {
            options = Options.Parse(args);
        }
        catch (UsageException e)
        {
            if (e.Message.Length > 0) Console.Error.WriteLine($"error: {e.Message}\n");
            Console.Error.WriteLine(Options.Usage);
            return e.Message.Length > 0 ? ExitError : 0;
        }

        var temp = (string?)null;
        try
        {
            var repoRoot = FindRepoRoot();
            var outputPath = Path.GetFullPath(options.Output ?? Path.Combine(repoRoot, Options.DefaultOutput));
            var annotationsPath = Path.GetFullPath(
                options.Annotations ?? Path.Combine(repoRoot, Options.DefaultAnnotations));

            var (packageRoot, version) = await AcquirePackageAsync(options, path => temp = path);

            var commands = new SourceParser(options.MaxDescription).Parse(packageRoot);
            if (commands.Count == 0)
                throw new InvalidOperationException(
                    $"no [CliCommand] methods found under '{packageRoot}' — is this the right package?");

            var annotations = Annotations.Load(annotationsPath);
            foreach (var orphan in annotations.Orphans(commands.Select(c => c.Name)))
                Log.Warn($"annotations: '{orphan}' matches no command in {version}; the note is unused");

            var generated = MarkdownWriter.Render(commands, version, annotations);
            var previous = File.Exists(outputPath) ? await File.ReadAllTextAsync(outputPath) : null;

            Console.Error.WriteLine();
            Console.Error.WriteLine(DiffReporter.Report(previous, generated, Relative(outputPath)));
            Console.Error.WriteLine();

            var upToDate = previous is not null && string.Equals(previous, generated, StringComparison.Ordinal);

            if (options.ToStdout)
            {
                Console.Out.Write(generated);
            }
            else if (options.Check)
            {
                if (upToDate)
                {
                    Log.Info($"{Relative(outputPath)} is up to date with {version}");
                }
                else
                {
                    Log.Info($"{Relative(outputPath)} is out of date with {version}; " +
                             "rerun without --check to update it");
                    Log.Summary();
                    return ExitOutOfDate;
                }
            }
            else if (upToDate)
            {
                Log.Info($"{Relative(outputPath)} already matches {version}; left untouched");
            }
            else
            {
                Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
                await File.WriteAllTextAsync(outputPath, generated, new UTF8Encoding(false));
                Log.Info($"wrote {Relative(outputPath)} — {commands.Count} commands from {version}");
            }

            Log.Summary();
            return 0;
        }
        catch (Exception e) when (e is InvalidOperationException
                                      or IOException
                                      or HttpRequestException
                                      or UnauthorizedAccessException
                                      or System.Text.Json.JsonException)
        {
            Console.Error.WriteLine($"error: {e.Message}");
            if (e is HttpRequestException)
                Console.Error.WriteLine(
                    "hint: the registry is reachable without auth; if egress is filtered, fetch the tarball " +
                    "elsewhere and pass it with --tarball, or unpack it and pass --source-dir.");
            return ExitError;
        }
        finally
        {
            if (temp is not null && Directory.Exists(temp))
            {
                if (options.KeepTemp) Log.Info($"kept unpacked package at {temp}");
                else TryDelete(temp);
            }
        }
    }

    /// <summary>Resolves the package to read, downloading it unless an offline input was given.</summary>
    private static async Task<(string PackageRoot, string Version)> AcquirePackageAsync(
        Options options, Action<string> registerTemp)
    {
        if (options.SourceDir is { } dir)
        {
            if (!Directory.Exists(dir))
                throw new InvalidOperationException($"--source-dir '{dir}' does not exist");

            var root = UpmRegistry.FindPackageRoot(Path.GetFullPath(dir));
            return (root, VersionOf(options, root));
        }

        var temp = Path.Combine(Path.GetTempPath(), "CommandRefGen-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(temp);
        registerTemp(temp);

        string tarball;
        var version = options.Version;

        if (options.Tarball is { } local)
        {
            if (!File.Exists(local))
                throw new InvalidOperationException($"--tarball '{local}' does not exist");
            tarball = Path.GetFullPath(local);
        }
        else
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
            http.DefaultRequestHeaders.UserAgent.ParseAdd("CommandRefGen/1.0");

            var resolved = await UpmRegistry.ResolveAsync(
                http, options.Registry, options.Package, string.IsNullOrEmpty(version) ? "latest" : version);
            version = resolved.Version;
            tarball = await UpmRegistry.DownloadTarballAsync(http, resolved.TarballUrl, temp);
        }

        var unpacked = Path.Combine(temp, "unpacked");
        UpmRegistry.ExtractTarball(tarball, unpacked);

        var packageRoot = UpmRegistry.FindPackageRoot(unpacked);
        return (packageRoot, options.Tarball is null ? version : VersionOf(options, packageRoot));
    }

    /// <summary>Prefers package.json for offline inputs; an explicit --version overrides it.</summary>
    private static string VersionOf(Options options, string packageRoot)
    {
        var declared = UpmRegistry.ReadPackageVersion(packageRoot);

        if (!string.IsNullOrEmpty(options.Version) && options.Version != "latest")
        {
            if (declared is not null && declared != options.Version)
                Log.Warn($"--version {options.Version} does not match the package.json version {declared}; " +
                         "labelling the document with the requested version");
            return options.Version;
        }

        if (declared is not null) return declared;

        Log.Warn($"{packageRoot}/package.json has no version; labelling the document 'unknown'");
        return "unknown";
    }

    /// <summary>Walks up from the working directory to the repository root.</summary>
    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        for (var d = dir; d is not null; d = d.Parent)
            if (Directory.Exists(Path.Combine(d.FullName, ".git")) || File.Exists(Path.Combine(d.FullName, ".git")))
                return d.FullName;
        return dir.FullName;
    }

    private static string Relative(string path)
    {
        var relative = Path.GetRelativePath(Directory.GetCurrentDirectory(), path);
        return relative.StartsWith("..", StringComparison.Ordinal) ? path : relative;
    }

    private static void TryDelete(string dir)
    {
        try
        {
            Directory.Delete(dir, recursive: true);
        }
        catch (IOException e)
        {
            Log.Warn($"could not remove the temp directory {dir}: {e.Message}");
        }
    }
}
