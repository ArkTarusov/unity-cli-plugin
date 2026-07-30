# CommandRefGen

Generates [`skills/unity-pipeline/references/editor-commands.md`](../../skills/unity-pipeline/references/editor-commands.md)
from the `com.unity.pipeline` package sources. No Unity Editor, no license, no
`unity --json command` dump — a machine with the .NET 8 SDK is the whole requirement.

The package is public on the UPM registry with no auth, and its C# sources carry every
`[CliCommand]`/`[CliArg]` declaration, including the `RuntimeOnly` commands the live listing
filters out and the version-gated ones that only exist on newer Unity. The source is therefore
a strict superset of the live listing, and the only source that needs no running editor.

## Usage

```bash
# document the newest version in the registry, updating the reference file in place
dotnet run --project tools/CommandRefGen -- --version latest

# pin a version
dotnet run --project tools/CommandRefGen -- --version 0.4.0-exp.1

# CI drift check: exits 3 when the reference file is out of date
dotnet run --project tools/CommandRefGen -- --version latest --check
```

A version bump is a rerun with a different `--version`. There is no code to edit.

Every run prints a diff summary against the file already on disk — added, removed and changed
commands and arguments — plus warnings, to stderr. `--stdout` puts the document on stdout so
stderr stays a clean log.

Offline, when the registry is unreachable (filtered egress, an air-gapped runner):

```bash
dotnet run --project tools/CommandRefGen -- --tarball ./com.unity.pipeline-0.4.0-exp.1.tgz
dotnet run --project tools/CommandRefGen -- --source-dir ./unpacked/package
```

Both read the version from the package's `package.json`. `--help` lists every option.

## What it owns

The generator owns the output file end to end: the do-not-edit banner, the preamble, the
Contents list, every category section and the RuntimeOnly section. Nothing in it is
hand-maintained, which is the drift trap the old two-step pipeline (a dump plus a hand-spliced
RuntimeOnly section) existed to create.

Field notes that are true but not derivable from the source — behaviour observed in the field,
links to the other skill files — live in [`annotations.json`](annotations.json) and are merged
into the matching command's entry. That keeps hand-maintained prose in a file the generator
reads, never in the file it writes. A note whose command no longer exists is reported as unused.

## Rules it applies

- **Roslyn, not regexes.** Attributes span lines, descriptions are built by concatenation or
  written as verbatim strings, and defaults live in two places.
- **Defaults.** The attribute's `DefaultValue` wins; otherwise the C# parameter default applies.
  Well-known constants are folded, including the package's `float.MinValue` "leave Unity's own
  value alone" sentinel, which renders as `-3.40282347e+38` exactly as the live listing does.
- **Required.** An explicit `Required` (or `IsRequired`) on `[CliArg]` decides it. Without one,
  an argument with no default of any kind is required. The first run against a new package
  version reports any argument whose required-ness moved, in the diff summary.
- **Descriptions are never shortened.** Newlines and whitespace runs collapse to single spaces
  so a multi-line description cannot break the markdown list, and that is the only edit. An
  emergency ceiling (`--max-description`, 1000 chars) exists so a runaway string cannot wreck
  the file; hitting it prints a warning naming the command — it is never a silent cut.
- **Version gates.** A command compiled under `#if UNITY_6000_7_OR_NEWER` is marked with its
  Unity floor instead of being presented as universally available. Non-version gates
  (`#if ENABLE_INPUT_SYSTEM`) are reported as conditional. Each file is parsed twice — once with
  every `#if` symbol defined, once with none — so `#else` and `#if !SYMBOL` branches are reached
  too; a command declared in both branches is reported as ungated, because it exists either way.
- **`Tests/` is excluded.** The package's test assembly registers throwaway commands
  (`log_editor`, `test_types`, `test_structured`) to prove registration works. A
  `Commands/Tests/` directory is a different thing — that is where `run_tests` and friends live,
  and it is kept.
- **Categories come from the source tree**, from the directory under `Commands/`. Display names
  and section order live in `Categories.cs`; an unmapped directory still gets a section, with its
  name humanized and a warning naming it, so the mapping is extended on purpose rather than by
  accident.
- **Project-defined commands are out of scope.** The reference documents the package surface.

## Tests

```bash
tools/CommandRefGen/tests/run.sh            # compare against the golden file
tools/CommandRefGen/tests/run.sh --update   # accept the current output as the new golden
```

`tests/fixture-package/` is a small source tree that exercises each rule above — multi-line
attributes, both default sources, an explicit `Required` on an argument that has a default,
the `float.MinValue` sentinel, a verbatim multi-line description, a `#if` version gate, a
compound gate, a command declared in both branches of an `#if`, `RuntimeOnly`,
`MainThreadRequired = false`, a parameter with no `[CliArg]`, an unmapped category directory,
and a `Tests/` assembly whose commands must not appear. The script also asserts the warnings
the fixture is built to provoke, and both `--check` outcomes.

CI runs it before the generator touches the real reference file
([`.github/workflows/command-ref.yml`](../../.github/workflows/command-ref.yml)).
