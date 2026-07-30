using System.Formats.Tar;
using System.IO.Compression;
using System.Text.Json;

namespace CommandRefGen;

/// <summary>
/// Reads the public UPM registry (no auth) and unpacks package tarballs.
/// </summary>
internal static class UpmRegistry
{
    /// <summary>Resolves a version to its tarball URL. <paramref name="requested"/> may be "latest".</summary>
    public static async Task<(string Version, string TarballUrl)> ResolveAsync(
        HttpClient http, string registry, string package, string requested)
    {
        var url = $"{registry}/{package}";
        Log.Info($"registry: GET {url}");

        await using var stream = await http.GetStreamAsync(url);
        using var doc = await JsonDocument.ParseAsync(stream);

        if (!doc.RootElement.TryGetProperty("versions", out var versions)
            || versions.ValueKind != JsonValueKind.Object)
            throw new InvalidOperationException($"{url}: no 'versions' object in the registry response");

        var names = versions.EnumerateObject().Select(p => p.Name).ToList();
        if (names.Count == 0)
            throw new InvalidOperationException($"{url}: 'versions' is empty");

        var version = requested is "latest"
            ? names.OrderBy(SemVer.Parse).Last()
            : requested;

        if (!versions.TryGetProperty(version, out var entry))
            throw new InvalidOperationException(
                $"version '{version}' not found in {url}. Available: {string.Join(", ", names.OrderBy(SemVer.Parse))}");

        if (!entry.TryGetProperty("dist", out var dist)
            || !dist.TryGetProperty("tarball", out var tarball)
            || tarball.GetString() is not { Length: > 0 } tarballUrl)
            throw new InvalidOperationException($"version '{version}' has no dist.tarball in {url}");

        if (requested is "latest")
            Log.Info($"registry: latest of {names.Count} versions is {version}");

        return (version, tarballUrl);
    }

    public static async Task<string> DownloadTarballAsync(HttpClient http, string tarballUrl, string destDir)
    {
        Log.Info($"registry: GET {tarballUrl}");
        var path = Path.Combine(destDir, "package.tgz");

        await using var response = await http.GetStreamAsync(tarballUrl);
        await using var file = File.Create(path);
        await response.CopyToAsync(file);

        return path;
    }

    /// <summary>Unpacks a gzipped tar into <paramref name="destDir"/>.</summary>
    public static void ExtractTarball(string tarballPath, string destDir)
    {
        Directory.CreateDirectory(destDir);
        using var file = File.OpenRead(tarballPath);
        using var gzip = new GZipStream(file, CompressionMode.Decompress);
        // TarFile rejects entries that would escape destDir.
        TarFile.ExtractToDirectory(gzip, destDir, overwriteFiles: true);
    }

    /// <summary>
    /// Finds the directory holding the package's package.json. UPM tarballs nest everything
    /// under `package/`, but an unpacked directory passed via --source-dir may be the root itself.
    /// </summary>
    public static string FindPackageRoot(string dir)
    {
        if (File.Exists(Path.Combine(dir, "package.json")))
            return dir;

        var nested = Directory.EnumerateDirectories(dir)
            .Where(d => File.Exists(Path.Combine(d, "package.json")))
            .OrderBy(d => d, StringComparer.Ordinal)
            .ToList();

        return nested.Count switch
        {
            1 => nested[0],
            0 => throw new InvalidOperationException($"no package.json found in '{dir}' or its immediate subdirectories"),
            _ => throw new InvalidOperationException(
                $"several packages found under '{dir}': {string.Join(", ", nested.Select(Path.GetFileName))}"),
        };
    }

    /// <summary>Reads the version field out of a package root's package.json, if present.</summary>
    public static string? ReadPackageVersion(string packageRoot)
    {
        var path = Path.Combine(packageRoot, "package.json");
        if (!File.Exists(path)) return null;

        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            return doc.RootElement.TryGetProperty("version", out var v) ? v.GetString() : null;
        }
        catch (JsonException e)
        {
            Log.Warn($"{path}: unreadable ({e.Message})");
            return null;
        }
    }
}
