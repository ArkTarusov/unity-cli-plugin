using System.Formats.Tar;
using System.IO.Compression;
using System.Text;
using Xunit;

namespace CommandRefGen.Tests;

/// <summary>
/// The tarball is downloaded from a public registry, so its entry names are untrusted input to a write
/// loop. These tests pin what the extraction actually does with them.
/// </summary>
public class PackageExtractionTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "commandrefgen-tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task Unpacks_the_package_directory_npm_tarballs_nest_everything_under()
    {
        var archive = TarGz(("package/Editor/Commands/Thing.cs", "// source"));

        var packageRoot = await PackageRegistry.ExtractAsync(archive, root, CancellationToken.None);

        Assert.Equal(Path.Combine(root, "package"), packageRoot);
        Assert.Equal("// source", await File.ReadAllTextAsync(Path.Combine(packageRoot, "Editor", "Commands", "Thing.cs")));
    }

    [Fact]
    public async Task Falls_back_to_the_destination_when_the_archive_has_no_package_directory()
    {
        var archive = TarGz(("Thing.cs", "// source"));

        Assert.Equal(root, await PackageRegistry.ExtractAsync(archive, root, CancellationToken.None));
    }

    [Fact]
    public async Task Reports_a_body_that_is_not_an_archive_at_all()
    {
        // A truncated download, or a proxy answering 200 with an HTML page in place of the tarball. The
        // exception type is what the entry point's catch filter has to name to turn this into an error
        // message rather than a stack trace.
        var notAnArchive = new MemoryStream("<html>Sign in to continue</html>"u8.ToArray());

        await Assert.ThrowsAsync<InvalidDataException>(() => PackageRegistry.ExtractAsync(notAnArchive, root, CancellationToken.None));
    }

    [Fact]
    public async Task Refuses_an_entry_that_would_land_outside_the_destination()
    {
        var archive = TarGz(("../escaped.txt", "owned"));

        await Assert.ThrowsAnyAsync<IOException>(() => PackageRegistry.ExtractAsync(archive, root, CancellationToken.None));
        Assert.False(File.Exists(Path.Combine(root, "..", "escaped.txt")));
    }

    private static Stream TarGz(params (string Name, string Content)[] entries)
    {
        var tar = new MemoryStream();
        using (var writer = new TarWriter(tar, leaveOpen: true))
        {
            foreach (var (name, content) in entries)
            {
                var entry = new PaxTarEntry(TarEntryType.RegularFile, name)
                {
                    DataStream = new MemoryStream(Encoding.UTF8.GetBytes(content)),
                };
                writer.WriteEntry(entry);
            }
        }

        tar.Position = 0;
        var gzipped = new MemoryStream();
        using (var gzip = new GZipStream(gzipped, CompressionMode.Compress, leaveOpen: true))
            tar.CopyTo(gzip);

        gzipped.Position = 0;
        return gzipped;
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // A scanner still holding a freshly written file must not fail a test that already passed.
        }
    }
}
