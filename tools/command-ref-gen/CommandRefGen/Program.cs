using System.Text.Json;
using CommandRefGen;

const string DefaultPackage = "com.unity.pipeline";
const string DefaultRegistry = "https://packages.unity.com";
const string DefaultOutputRelativePath = "skills/unity-pipeline/references/editor-commands.md";

Options options;
try
{
    var parsed = CommandLine.Parse(args);
    if (parsed is null)
    {
        CommandLine.PrintUsage();
        // Parse returns null only for an explicit --help; anything unusable throws.
        return 0;
    }

    options = parsed;
}
catch (ArgumentException exception)
{
    Console.Error.WriteLine($"error: {exception.Message}");
    CommandLine.PrintUsage();
    return ExitCode.Failure;
}

var warnings = 0;
void Warn(string message)
{
    warnings++;
    Console.Error.WriteLine($"warning: {message}");
}

// HttpClient.Timeout only bounds the wait for response headers; reading the body and unpacking the
// archive run afterwards, so they get a deadline of their own instead of hanging indefinitely.
var registryDeadline = TimeSpan.FromMinutes(10);
using var deadline = new CancellationTokenSource(registryDeadline);

var exitCode = await Generate();

if (warnings > 0)
    Console.Error.WriteLine($"{warnings} warning(s)");

return ExitCode.Settle(exitCode, options.Strict, warnings);

// The whole job, so that the exit code is settled only after the temp directory has been cleared: a
// warning raised while cleaning up counts like any other.
async Task<int> Generate()
{
    string? workingDirectory = null;
    try
    {
        var packageName = PathSafe("--package", options.Package ?? DefaultPackage);
        var registryUrl = options.Registry ?? DefaultRegistry;
        var outputPath = Path.GetFullPath(options.Output ?? Path.Combine(RepoRoot(), DefaultOutputRelativePath.Replace('/', Path.DirectorySeparatorChar)));

        using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
        var registry = new PackageRegistry(http, registryUrl);
        var catalogue = await registry.FetchVersionsAsync(packageName, deadline.Token);

        var wantsLatest = options.Version.Equals("latest", StringComparison.OrdinalIgnoreCase);
        var version = options.Version.ToLowerInvariant() switch
        {
            "latest" => PackageRegistry.HighestVersion(catalogue.Tarballs.Keys, Warn),
            "current" => ReferenceStamp.Read(outputPath, packageName),
            _ => options.Version,
        };

        // With --version latest the version string comes from the registry document, i.e. from remote
        // data, and it ends up in a path that is deleted recursively — keep it inside the temp directory.
        version = PathSafe("--version", version);

        if (!catalogue.Tarballs.TryGetValue(version, out var tarballUrl))
        {
            Console.Error.WriteLine(
                $"error: {packageName} has no version {version}; published: {string.Join(", ", catalogue.Tarballs.Keys)}");
            return ExitCode.Failure;
        }

        if (wantsLatest && catalogue.LatestTag is { Length: > 0 } tag && tag != version)
            Warn($"registry dist-tag 'latest' is {tag} but the highest published version is {version} — using {version}");

        Console.WriteLine($"{packageName}@{version}");
        Console.WriteLine($"source:  {tarballUrl}");

        // Per process, so that two runs of the same version — a release watch and a manual one — cannot
        // delete and unpack the same directory underneath each other.
        workingDirectory = Path.Combine(Path.GetTempPath(), "commandrefgen", $"{packageName}@{version}-{Environment.ProcessId}");
        if (Directory.Exists(workingDirectory))
            Directory.Delete(workingDirectory, recursive: true);

        var packageRoot = await registry.DownloadAndExtractAsync(tarballUrl, workingDirectory, deadline.Token);

        var commands = new CommandParser(Warn).Parse(packageRoot);
        if (commands.Count == 0)
        {
            Console.Error.WriteLine($"error: no [CliCommand] methods found under {packageRoot} — the package layout has changed");
            return ExitCode.Failure;
        }

        var toolDirectory = AppContext.BaseDirectory;
        var annotations = Annotations.Load(Path.Combine(toolDirectory, "annotations.json"), Warn);
        annotations.ReportUnused(commands.Select(c => c.Name), Warn);

        // Notes go to stdout, not into Warn: a derived section title is designed behaviour, and turning
        // it into a warning would make --strict fail the day the package adds a directory — exactly the
        // human-free run the derivation exists for.
        var categories = Categories.Load(Path.Combine(toolDirectory, "categories.json"), n => Console.WriteLine($"note: {n}"));
        categories.ReportUnusedRules(commands.Where(c => !c.RuntimeOnly).Select(c => c.SourcePath), Warn);

        var templates = MarkdownWriter.TemplateSet.Load(Path.Combine(toolDirectory, "templates"));
        var markdown = new MarkdownWriter(annotations, categories, Warn).Render(commands, version, packageName, templates);

        var previous = File.Exists(outputPath) ? File.ReadAllText(outputPath).Replace("\r\n", "\n") : null;
        var listed = commands.Count(c => !c.RuntimeOnly);

        Console.WriteLine($"parsed:  {commands.Count} commands ({listed} listed, {commands.Count - listed} RuntimeOnly)");
        Console.WriteLine($"output:  {outputPath}");
        Console.WriteLine();

        var summary = ReferenceDiff.Summarise(previous, markdown, Warn);
        Console.WriteLine(summary.Length > 0 ? summary : "no command or argument changes against the current file");

        if (options.Check)
        {
            if (previous == markdown)
            {
                Console.WriteLine("check: the reference is up to date");
                return ExitCode.Success;
            }

            Console.Error.WriteLine("check: the reference is out of date — rerun without --check to update it");
            return ExitCode.OutOfDate;
        }

        var outputDirectory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(outputDirectory))
            Directory.CreateDirectory(outputDirectory);

        File.WriteAllText(outputPath, markdown);
        Console.WriteLine($"written: {outputPath}");
        return ExitCode.Success;
    }
    catch (OperationCanceledException) when (deadline.IsCancellationRequested)
    {
        Console.Error.WriteLine(
            $"error: {options.Registry ?? DefaultRegistry} did not answer within {registryDeadline.TotalMinutes:0} minutes — check the network and the registry");
        return ExitCode.Failure;
    }
    catch (Exception exception) when (exception
                                      is UnsupportedExpressionException
                                      or InvalidOperationException
                                      or ArgumentException
                                      or HttpRequestException
                                      or JsonException
                                      // A --registry value that is not a URL at all.
                                      or FormatException
                                      or IOException
                                      // A body that is not a gzip stream at all — a truncated download,
                                      // or a proxy page served with 200 in place of the tarball.
                                      or InvalidDataException
                                      or UnauthorizedAccessException
                                      or OperationCanceledException)
    {
        Console.Error.WriteLine($"error: {exception.Message}");
        return ExitCode.Failure;
    }
    finally
    {
        if (workingDirectory is not null)
        {
            if (options.KeepSources)
                Console.WriteLine($"sources kept in {workingDirectory}");
            else
                RemoveWorkingDirectory(workingDirectory);
        }
    }
}

// Clearing the temp directory must not replace the result of the run it cleans up after: a file still
// held open by a scanner or an indexer is worth a warning, not a crash on the way out.
void RemoveWorkingDirectory(string directory)
{
    try
    {
        if (Directory.Exists(directory))
            Directory.Delete(directory, recursive: true);
    }
    catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
    {
        Warn($"could not remove the temporary directory {directory}: {exception.Message}");
    }
}

// Rejects a value that would escape the temp directory it is about to be pasted into.
static string PathSafe(string argument, string value)
{
    if (value.Length == 0)
        throw new ArgumentException($"{argument} is empty");

    if (value.Contains("..", StringComparison.Ordinal) ||
        value.Contains('/') || value.Contains('\\') ||
        value.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
    {
        throw new ArgumentException($"{argument} value '{value}' is not a usable path component");
    }

    return value;
}

// Walks up from the executable until the checkout that holds the reference file is found.
static string RepoRoot()
{
    var directory = new DirectoryInfo(AppContext.BaseDirectory);
    while (directory is not null)
    {
        if (Directory.Exists(Path.Combine(directory.FullName, ".git")) ||
            File.Exists(Path.Combine(directory.FullName, ".git")))
        {
            return directory.FullName;
        }

        directory = directory.Parent;
    }

    throw new InvalidOperationException(
        "cannot locate the repository root from the executable path — pass --output explicitly");
}
