using System.Globalization;

namespace CommandRefGen;

/// <summary>
/// Minimal semver-2.0 precedence, enough to pick the highest entry out of a UPM
/// version listing (`0.4.0-exp.1` &lt; `0.4.0-exp.2` &lt; `0.4.0`).
/// Unparseable versions sort below every parseable one, ordered by ordinal string.
/// </summary>
internal sealed class SemVer : IComparable<SemVer>
{
    public string Raw { get; }
    private readonly int[] _core;
    private readonly string[] _pre;
    private readonly bool _valid;

    private SemVer(string raw, int[] core, string[] pre, bool valid)
    {
        Raw = raw;
        _core = core;
        _pre = pre;
        _valid = valid;
    }

    public static SemVer Parse(string raw)
    {
        var body = raw;

        // Build metadata is ignored for precedence.
        var plus = body.IndexOf('+');
        if (plus >= 0) body = body[..plus];

        string[] pre = [];
        var dash = body.IndexOf('-');
        if (dash >= 0)
        {
            pre = body[(dash + 1)..].Split('.');
            body = body[..dash];
        }

        var parts = body.Split('.');
        var core = new int[3];
        var valid = parts.Length is >= 1 and <= 3;
        for (var i = 0; i < parts.Length && valid; i++)
        {
            if (int.TryParse(parts[i], NumberStyles.None, CultureInfo.InvariantCulture, out var n))
                core[i] = n;
            else
                valid = false;
        }

        return new SemVer(raw, core, pre, valid);
    }

    public int CompareTo(SemVer? other)
    {
        if (other is null) return 1;

        if (_valid != other._valid) return _valid ? 1 : -1;
        if (!_valid) return string.CompareOrdinal(Raw, other.Raw);

        for (var i = 0; i < 3; i++)
        {
            var c = _core[i].CompareTo(other._core[i]);
            if (c != 0) return c;
        }

        // A release outranks any prerelease of the same core version.
        if (_pre.Length == 0 || other._pre.Length == 0)
            return (other._pre.Length == 0 ? 1 : 0) - (_pre.Length == 0 ? 1 : 0);

        for (var i = 0; i < Math.Min(_pre.Length, other._pre.Length); i++)
        {
            var a = _pre[i];
            var b = other._pre[i];
            var aNum = int.TryParse(a, NumberStyles.None, CultureInfo.InvariantCulture, out var an);
            var bNum = int.TryParse(b, NumberStyles.None, CultureInfo.InvariantCulture, out var bn);

            int c;
            if (aNum && bNum) c = an.CompareTo(bn);
            else if (aNum != bNum) c = aNum ? -1 : 1; // numeric identifiers rank below alphanumeric
            else c = string.CompareOrdinal(a, b);

            if (c != 0) return c;
        }

        return _pre.Length.CompareTo(other._pre.Length);
    }

    public override string ToString() => Raw;
}
