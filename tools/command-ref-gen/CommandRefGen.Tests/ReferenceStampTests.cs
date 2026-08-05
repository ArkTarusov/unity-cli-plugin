using Xunit;

namespace CommandRefGen.Tests;

/// <summary>
/// The stamp is what `--version current` reads to learn which package version the committed file
/// documents. Writing it and reading it back live in different places, so only a round trip catches a
/// format change made on one side alone.
/// </summary>
public class ReferenceStampTests : IDisposable
{
    private readonly string path = Path.Combine(Path.GetTempPath(), "commandrefgen-tests", Guid.NewGuid().ToString("N") + ".md");

    [Fact]
    public void Reads_back_the_version_it_wrote()
    {
        Write(ReferenceStamp.Format("com.unity.pipeline", "0.4.0-exp.1"));

        Assert.Equal("0.4.0-exp.1", ReferenceStamp.Read(path, "com.unity.pipeline"));
    }

    [Fact]
    public void Finds_the_stamp_after_a_crlf_checkout()
    {
        Write($"<!--\r\nheader\r\n-->\r\n\r\n{ReferenceStamp.Format("com.unity.pipeline", "0.4.0-exp.1")}\r\n\r\n# Title\r\n");

        Assert.Equal("0.4.0-exp.1", ReferenceStamp.Read(path, "com.unity.pipeline"));
    }

    [Fact]
    public void Refuses_a_file_that_does_not_exist() =>
        Assert.Throws<InvalidOperationException>(() => ReferenceStamp.Read(path, "com.unity.pipeline"));

    [Fact]
    public void Refuses_a_file_written_before_the_stamp_existed()
    {
        Write("# Editor command reference\n\nNo stamp here.\n");

        Assert.Throws<InvalidOperationException>(() => ReferenceStamp.Read(path, "com.unity.pipeline"));
    }

    [Fact]
    public void Refuses_a_file_generated_from_another_package()
    {
        Write(ReferenceStamp.Format("com.unity.other", "0.4.0-exp.1"));

        var failure = Assert.Throws<InvalidOperationException>(() => ReferenceStamp.Read(path, "com.unity.pipeline"));
        Assert.Contains("com.unity.other", failure.Message);
    }

    private void Write(string contents)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, contents);
    }

    public void Dispose()
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // A scanner still holding a freshly written file must not fail a test that already passed.
        }
    }
}
