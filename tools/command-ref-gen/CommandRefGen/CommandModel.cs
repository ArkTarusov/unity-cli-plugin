namespace CommandRefGen;

/// <summary>One <c>[CliArg]</c>-described parameter of a command.</summary>
/// <param name="Name">CLI argument name (<c>--name value</c>).</param>
/// <param name="Description">Human text, whitespace already collapsed.</param>
/// <param name="Type">.NET type name as the running server reports it (<c>Type.Name</c>), e.g. String, Boolean, Single[].</param>
/// <param name="Required">True when the argument must be supplied.</param>
/// <param name="DefaultValue">Default as a JSON literal (e.g. <c>"idle"</c>, <c>true</c>, <c>0</c>), or null when there is none.</param>
/// <param name="Members">
/// Fields of the nested object this argument carries, empty for a plain value. A structured argument is
/// passed as a JSON object, and its members are what the caller actually has to fill in.
/// </param>
public sealed record CommandArg(
    string Name,
    string Description,
    string Type,
    bool Required,
    string? DefaultValue,
    IReadOnlyList<CommandArg> Members);

/// <summary>One <c>[CliCommand]</c>-marked method found in the package sources.</summary>
/// <param name="Name">Command name as typed on the CLI.</param>
/// <param name="Description">Human text, whitespace already collapsed.</param>
/// <param name="MainThreadRequired">False when the command runs while the main thread is busy.</param>
/// <param name="RuntimeOnly">True when the editor server hides the command from its listing.</param>
/// <param name="Args">Arguments in declaration order.</param>
/// <param name="Gates">Preprocessor conditions the method is compiled under, outermost first.</param>
/// <param name="SourcePath">Package-relative path of the declaring file, forward slashes.</param>
/// <param name="SourceLine">1-based line of the attribute in that file.</param>
public sealed record CommandInfo(
    string Name,
    string Description,
    bool MainThreadRequired,
    bool RuntimeOnly,
    IReadOnlyList<CommandArg> Args,
    IReadOnlyList<string> Gates,
    string SourcePath,
    int SourceLine);
