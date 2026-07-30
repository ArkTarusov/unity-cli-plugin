namespace CommandRefGen;

/// <summary>One `[CliArg]`-annotated parameter of a command.</summary>
internal sealed class CliArgInfo
{
    public required string Name { get; init; }

    /// <summary>CLR-style type name as the CLI listing shows it (`String`, `Int32`, `ObjectId[]`).</summary>
    public required string Type { get; init; }

    /// <summary>Rendered default, or null when the argument has none.</summary>
    public string? Default { get; init; }

    public bool Required { get; init; }

    public string Description { get; init; } = "";

    /// <summary>Compared by the diff reporter; description text is compared separately.</summary>
    public string Signature => $"{Type}|{Default ?? "-"}|{(Required ? "req" : "opt")}";
}

/// <summary>One `[CliCommand]`-annotated method.</summary>
internal sealed record CommandInfo
{
    public required string Name { get; init; }
    public string Description { get; init; } = "";

    /// <summary>False when the attribute sets `MainThreadRequired = false`.</summary>
    public bool MainThreadRequired { get; init; } = true;

    /// <summary>True when the attribute sets `RuntimeOnly = true` — these are hidden from the editor listing.</summary>
    public bool RuntimeOnly { get; init; }

    /// <summary>Category directory under `Commands/`, or null when the source sits outside one.</summary>
    public string? CategoryDir { get; init; }

    /// <summary>Minimum Unity version from an enclosing `#if UNITY_x_y_OR_NEWER`, e.g. "6000.7".</summary>
    public string? MinUnityVersion { get; init; }

    /// <summary>Enclosing preprocessor conditions that are not Unity version gates.</summary>
    public IReadOnlyList<string> Conditions { get; init; } = [];

    /// <summary>Package-relative source path, for warnings.</summary>
    public required string SourcePath { get; init; }

    public int SourceLine { get; init; }

    public List<CliArgInfo> Args { get; init; } = [];
}
