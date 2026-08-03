# CommandRefGen

Regenerates [`skills/unity-pipeline/references/editor-commands.md`](../../../skills/unity-pipeline/references/editor-commands.md) from the published `com.unity.pipeline` package sources. No Unity Editor, no project, no captured `unity command` dump — only the .NET 8 SDK and network access to the public UPM registry.

```bash
dotnet run --project tools/command-ref-gen/CommandRefGen -- --version latest
```

| Option | Meaning |
| --- | --- |
| `--version <v\|latest\|current>` | Package version to document. `latest` picks the highest published version; `current` reuses the version the output file already records. Required. |
| `--output <path>` | File to write. Defaults to the reference file in this repository. |
| `--package <name>` | UPM package name. Default `com.unity.pipeline`. A scoped name (`@scope/pkg`) is rejected: the name becomes a directory name under the temp directory, so it may not contain a path separator. |
| `--registry <url>` | Registry base URL. Default `https://packages.unity.com`. |
| `--check` | Print the diff and exit 2 if the file is out of date; write nothing. |
| `--keep-sources` | Leave the unpacked package in the temp directory for inspection. |
| `--strict` | Turn a successful run that warned into exit 3. |

Exit codes: `0` success, `1` failure, `2` `--check` found the file out of date, `3` `--strict` and an otherwise successful run warned. A run that fails or finds the file out of date keeps its own code even under `--strict`, because 1 and 2 say something more specific than "something warned"; a caller that needs to know about warnings regardless reads the `N warning(s)` line on stderr.

## Pinning a check to the documented version

The generator stamps the file it writes with a machine-readable line, emitted by the generator itself rather than by a template so that rewording the templates cannot break reading it back:

```
<!-- generated-from: com.unity.pipeline@0.4.0-exp.1 -->
```

`--version current` reads that line back. Combined with `--check` it answers "does this generator still produce the committed file?" — a question about the generator, not about the registry:

```bash
dotnet run --project tools/command-ref-gen/CommandRefGen -- --version current --check
```

Use `--version latest --check` instead to answer the other question: "has the package moved on?" That one legitimately fails whenever a new version is published, so the two belong to different triggers: the first to whatever gates a change to this tool, the second to a release watch. Neither runs automatically from this repository — nothing here schedules them.

## What it does

1. Reads `<registry>/<package>` and resolves the version to a `dist.tarball` URL. `latest` picks the highest one by semantic version — UPM declares semver, and the ordering is `NuGet.Versioning`'s rather than this tool's own.
2. Downloads and unpacks the tarball into a temp directory.
3. Parses the package's `.cs` files with Roslyn and reads the `[CliCommand]` / `[CliArg]` attributes from the syntax tree — attributes span several lines and defaults may reference constants declared in another file, so regular expressions are not enough. Two kinds of directory are left out: the root `Tests/` assembly, which holds registration fixtures (`log_editor`, `test_types`, `test_structured`) rather than shipped commands, and anything under a directory whose name ends with `~` (`Samples~`, `Documentation~`), which Unity does not compile at all. A directory merely *named* `Tests` inside a compiled assembly is read like any other. Commands are taken from `Editor/Commands/` and `Runtime/Commands/`; the remaining files supply constants and DTO types. A `[CliCommand]` found anywhere else is reported as a warning instead of being dropped.
4. Expands the DTO types a command takes as a single structured argument, so the reference shows the fields of that JSON object instead of an opaque type name.
5. Writes the reference file and prints a summary of what changed: commands added and removed, changed descriptions, and per-argument type/default/description changes.

## Fidelity to a live editor

The generated metadata mirrors what a running Pipeline server answers for `unity --json command`, because it reproduces the same rules the package's own `CommandRegistry` applies:

- an argument's name falls back to the C# parameter name when `[CliArg]` omits it;
- an argument is required when `[CliArg(Required = true)]` says so, or — with no attribute — when the parameter has no C# default;
- the C# parameter default wins; the attribute's `DefaultValue` is only used for parameters that have none;
- the printed type is the framework name (`String`, `Boolean`, `Single[]`), not the C# keyword.

What the metadata says is the same; how it reads is this tool's own, in two places where the server's raw text tells a reader nothing:

- a floating-point default is printed in its shortest round-trippable form, so `float.MinValue` reads `-3.4028235e+38` where a listing captured from an editor showed `-3.40282347e+38` — the same number, written with the digit count that editor's JSON serializer happened to use;
- a nullable parameter is printed as its underlying type (`Int32`), where the listing reports ``Nullable`1``. That an argument is optional is already carried by the absence of the required marker;
- a generic type is written out (`List<String>`) rather than as the arity form `` List`1 ``, whose literal backtick would collide with the ones around argument names and break the line.

Three things the live listing cannot give and this tool can: commands declared `RuntimeOnly = true` (hidden from an editor's listing but executable), commands compiled out on the Unity version that produced a dump — those get an availability note such as *(Unity 6000.7+ only)* derived from the `#if` they sit under — and the fields of a structured argument.

## Structured arguments

A command that needs several related values takes a single parameter whose type implements `IStructuredCommandInput`, passed as a JSON object. A live listing prints only the type name for it (`TagsLayersInput`), leaving the fields visible solely inside the per-command JSON schema. The generator expands them as nested bullets:

```markdown
- `settings` TagsLayersInput — Tag/layer changes to make.
  - `addTags` String[] — Tag names to add.
  - `setLayers` LayerAssignment[] — User layer assignments (index 8-31).
    - `index`\* Int32 — Layer index (8-31 for user layers).
```

The members are the ones `JsonSchemaGenerator` reflects over: public instance fields and read/write properties, minus `[JsonIgnore]`, named by `[CliArg]`, then by Newtonsoft's `[JsonProperty]`, then by the member itself. Members carry no default — the schema has no place for one. Arrays and lists are expanded through to their element type; a self-referential type stops where the package's own schema generator stops.

## Editing the output

The output file is generated end to end and must not be hand-edited. The two places to change instead:

- `templates/` — the header comment, the preamble paragraphs and the RuntimeOnly section intro. Placeholders: `{{PACKAGE}}`, `{{VERSION}}`, `{{LISTED_COUNT}}`, `{{RUNTIME_ONLY_COUNT}}`, `{{TOTAL_COUNT}}`, and — for linking to the hidden-command section without hardcoding its title — `{{RUNTIME_ONLY_SECTION}}` and `{{RUNTIME_ONLY_ANCHOR}}`. A placeholder the generator does not fill in is reported rather than left in the file.
- `annotations.json` — per-command field notes appended to the description generated from the sources. Use it only for behaviour the sources do not state; an annotation whose command disappears is reported as a warning.
- `categories.json` — the section a command is documented under, as path-prefix rules (longest match wins, so a file rule overrides the directory rule around it) plus the order the sections appear in.

A source path matching no rule takes its directory name under the commands root as the section title (`Editor/Commands/VFX/…` → "VFX"), so a directory a new package version adds gets a usable section without waiting for a rule; the run prints a note suggesting a deliberate title. Only a root-level file with no rule is filed under "Other" and reported as a warning, and a rule matching no source in the current package version is reported as stale.

## Building and testing

`tools/command-ref-gen/CommandRefGen.sln` ties the tool and its tests together — open that in an IDE, or from the command line:

```bash
dotnet build tools/command-ref-gen/CommandRefGen.sln
dotnet test tools/command-ref-gen/CommandRefGen.sln
```

It is the only .NET code in the repository: the plugin itself is the `skills/` tree, and `tools/` holds what maintains it.

The tests cover the parts that decide what lands in the file and can be exercised without Unity, a registry, or a network: attribute reading, constant folding, version ordering, the change summary, and the unpacking of an untrusted archive. The parser tests state the rules in cases the real package does not distinguish — where a `[CliArg]` default and a C# default disagree, for instance — so that a change to those rules fails here rather than silently in a future package version. Running the generator against the real package with `--version current --check` is the integration test.

## Warnings

Warnings go to stderr. By default they do not change the exit code; `--strict` turns any of them into exit 3, because most of them mean the output may be incomplete and a caller polling the exit code alone would not see that. Among them: a command declared outside the directories commands are read from, a command left in an inactive `#if` branch, a command on a non-static method, a root-level source file no section rule matches, a section rule matching no source in the package version, a type name that resolves to more than one declaration, a duplicate command or argument name, a stale annotation, a template placeholder the generator does not fill in, a version the registry publishes that is not a semantic version, and a description long enough (over 1000 characters) to suggest something went wrong upstream. Descriptions are never truncated. One warning is about the run rather than the output: a temporary directory that could not be removed afterwards.

A construct the tool cannot evaluate — an attribute argument that is not a compile-time constant it understands — is a hard error with the file and line, not a guess.
