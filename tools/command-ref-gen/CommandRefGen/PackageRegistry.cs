using System.Formats.Tar;
using System.IO.Compression;
using System.Text.Json;
using NuGet.Versioning;

namespace CommandRefGen;

/// <summary>
/// Talks to the public Unity UPM registry: lists published versions and unpacks a version's tarball.
/// No authentication and no Unity installation are involved — the package sources are the only input
/// the reference is built from.
/// </summary>
public sealed class PackageRegistry(HttpClient http, string registryBaseUrl)
{
    /// <summary>Registry metadata for one package: every published version and its tarball URL.</summary>
    public sealed record PackageVersions(IReadOnlyDictionary<string, string> Tarballs, string? LatestTag);

    /// <summary>Fetches the package document and extracts the version → tarball-URL map.</summary>
    public async Task<PackageVersions> FetchVersionsAsync(string packageName, CancellationToken cancellationToken)
    {
        var url = $"{registryBaseUrl.TrimEnd('/')}/{packageName}";
        await using var stream = await http.GetStreamAsync(url, cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

        if (!document.RootElement.TryGetProperty("versions", out var versions))
            throw new InvalidOperationException($"{url}: registry document has no 'versions' object");

        var tarballs = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var version in versions.EnumerateObject())
        {
            if (!version.Value.TryGetProperty("dist", out var dist) ||
                !dist.TryGetProperty("tarball", out var tarball) ||
                tarball.GetString() is not { Length: > 0 } tarballUrl)
            {
                throw new InvalidOperationException($"{url}: version {version.Name} has no dist.tarball");
            }

            tarballs[version.Name] = tarballUrl;
        }

        string? latestTag = null;
        if (document.RootElement.TryGetProperty("dist-tags", out var tags) &&
            tags.TryGetProperty("latest", out var latest))
        {
            latestTag = latest.GetString();
        }

        return new PackageVersions(tarballs, latestTag);
    }

    /// <summary>Downloads the tarball and unpacks it into <paramref name="destination"/>.</summary>
    /// <returns>The package root inside the archive (npm tarballs nest everything under <c>package/</c>).</returns>
    public async Task<string> DownloadAndExtractAsync(string tarballUrl, string destination, CancellationToken cancellationToken)
    {
        await using var response = await http.GetStreamAsync(tarballUrl, cancellationToken);
        return await ExtractAsync(response, destination, cancellationToken);
    }

    /// <summary>
    /// Unpacks a gzipped tar into <paramref name="destination"/>.
    ///
    /// The archive comes off the network, so its entry names are untrusted input to a write loop; an
    /// entry pointing outside the destination makes <c>TarFile</c> throw rather than write there, which
    /// the test suite pins.
    /// </summary>
    /// <returns>The package root inside the archive (npm tarballs nest everything under <c>package/</c>).</returns>
    public static async Task<string> ExtractAsync(Stream gzippedTar, string destination, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(destination);

        await using var gzip = new GZipStream(gzippedTar, CompressionMode.Decompress);
        await TarFile.ExtractToDirectoryAsync(gzip, destination, overwriteFiles: true, cancellationToken);

        var packageRoot = Path.Combine(destination, "package");
        return Directory.Exists(packageRoot) ? packageRoot : destination;
    }

    /// <summary>
    /// Picks the highest published version. UPM declares semantic versioning, so the ordering is
    /// <c>NuGetVersion</c>'s: a prerelease sorts below the release it precedes, and prerelease
    /// identifiers compare numerically where they are numbers. A version the parser rejects cannot be
    /// ordered against the rest, so it is reported and left out rather than silently treated as zero.
    /// </summary>
    public static string HighestVersion(IEnumerable<string> versions, Action<string> warn)
    {
        var ordered = new List<NuGetVersion>();
        foreach (var version in versions)
        {
            if (NuGetVersion.TryParse(version, out var parsed))
                ordered.Add(parsed);
            else
                warn($"version '{version}' is not a semantic version — excluded when resolving 'latest'");
        }

        if (ordered.Count == 0)
            throw new InvalidOperationException("the registry lists no version this tool can order");

        // OriginalVersion, not ToString(): the registry is keyed by the exact string it published.
        return ordered.Max()!.OriginalVersion!;
    }
}
