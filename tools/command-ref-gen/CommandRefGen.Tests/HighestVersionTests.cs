using Xunit;

namespace CommandRefGen.Tests;

/// <summary>
/// Resolving `latest` decides which package version gets documented, so a wrong answer here silently
/// documents the wrong release. The ordering itself comes from NuGet.Versioning; what these cases pin is
/// that UPM's version strings mean to it what the tool assumes they mean.
/// </summary>
public class HighestVersionTests
{
    [Fact]
    public void Picks_the_highest_of_the_versions_the_registry_publishes() =>
        Assert.Equal(
            "0.4.0-exp.1",
            PackageRegistry.HighestVersion(
                new[] { "0.2.0-exp.2", "0.4.0-exp.1", "0.3.1-exp.1", "0.3.0-exp.1" },
                _ => Assert.Fail("no version should have been excluded")));

    [Theory]
    [InlineData("1.0.0", "1.0.0-exp.1")]
    [InlineData("0.10.0", "0.9.9")]
    [InlineData("1.0.0", "0.99.99")]
    [InlineData("1.0.0-exp.10", "1.0.0-exp.9")]
    [InlineData("1.0.0-beta", "1.0.0-1")]
    public void Orders_the_shapes_upm_publishes(string higher, string lower) =>
        Assert.Equal(higher, PackageRegistry.HighestVersion(new[] { lower, higher }, _ => { }));

    [Fact]
    public void Answers_with_the_string_the_registry_published()
    {
        // The result is looked up in the registry's own version map, so a normalised "1.2.0" would find
        // nothing where the registry published "1.2".
        Assert.Equal("1.2", PackageRegistry.HighestVersion(new[] { "1.1.9", "1.2" }, _ => { }));
    }

    [Fact]
    public void Excludes_and_reports_a_version_it_cannot_order()
    {
        var warnings = new List<string>();
        var highest = PackageRegistry.HighestVersion(new[] { "1.0.0", "1.0.x" }, warnings.Add);

        Assert.Equal("1.0.0", highest);
        Assert.Contains(warnings, w => w.Contains("1.0.x"));
    }

    [Fact]
    public void Fails_when_no_version_can_be_ordered() =>
        Assert.Throws<InvalidOperationException>(() => PackageRegistry.HighestVersion(new[] { "nightly" }, _ => { }));
}
