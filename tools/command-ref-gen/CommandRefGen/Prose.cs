using System.Text.RegularExpressions;

namespace CommandRefGen;

/// <summary>Text shaping shared by the parser and the writer.</summary>
public static partial class Prose
{
    /// <summary>
    /// Descriptions are never truncated — the cut-off details (default shader names, selector syntax)
    /// are exactly what a reader needs. Past this length the generator only complains, so an accidental
    /// wall of text in the package sources is visible instead of silently reshaping the reference.
    /// </summary>
    public const int LongDescriptionThreshold = 1000;

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRun();

    /// <summary>
    /// Collapses newlines and whitespace runs into single spaces so that a multi-line source description
    /// still renders as one markdown list item. Nothing is dropped.
    /// </summary>
    public static string Collapse(string value) => WhitespaceRun().Replace(value, " ").Trim();
}
