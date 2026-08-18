using System.Text.RegularExpressions;

namespace CommandRefGen;

/// <summary>
/// The machine-readable line the generator writes into the reference file recording which package
/// version produced it. It is emitted by the generator itself rather than by a template, so that
/// rewording the templates cannot break reading it back.
///
/// Reading it back is what <c>--version current</c> does: regenerate against the version the file
/// already documents. That keeps an automated <c>--check</c> honest about generator changes without
/// turning red the moment a new package version is published.
/// </summary>
public static partial class ReferenceStamp
{
    [GeneratedRegex(@"^<!-- generated-from: (?<package>[^@\s]+)@(?<version>\S+) -->$", RegexOptions.Multiline)]
    private static partial Regex StampLine();

    /// <summary>Formats the stamp for <paramref name="packageName"/> at <paramref name="version"/>.</summary>
    public static string Format(string packageName, string version) =>
        $"<!-- generated-from: {packageName}@{version} -->";

    /// <summary>Reads the version recorded in <paramref name="path"/>.</summary>
    /// <exception cref="InvalidOperationException">The file is missing, unstamped, or documents another package.</exception>
    public static string Read(string path, string expectedPackage)
    {
        if (!File.Exists(path))
            throw new InvalidOperationException($"--version current needs an existing reference file; {path} does not exist");

        var match = StampLine().Match(File.ReadAllText(path).Replace("\r\n", "\n"));
        if (!match.Success)
        {
            throw new InvalidOperationException(
                $"{path} carries no 'generated-from' line — it predates this generator, so pass an explicit --version once");
        }

        var package = match.Groups["package"].Value;
        if (package != expectedPackage)
        {
            throw new InvalidOperationException(
                $"{path} was generated from {package}, not {expectedPackage} — pass --package {package} or an explicit --version");
        }

        return match.Groups["version"].Value;
    }
}
