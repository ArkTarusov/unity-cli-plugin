---
name: unity-cli-core
description: Use when a task involves UnityCLI — Unity's standalone `unity` terminal command — or any automation built on the `unity` binary. Also use when `unity` returns "command not found", when unsure whether the tool exists, when scripting Unity with JSON/TSV output, or when checking Unity Editor connection status, sign-in state, or CLI diagnostics. Not for in-project SDK work with no CLI surface — Unity Services (UGS) backends, in-app purchases, or ads mediation.
---

# UnityCLI core

UnityCLI is a standalone `unity` binary from Unity Technologies (experimental, first public betas in 2026). It replaces the Unity Hub CLI and adds live control of a running Unity Editor. It is NOT `Unity.exe` (the editor binary with `-batchmode` flags) and NOT Unity Hub.

Two other skills cover the specialized halves:
- **unity-editors** — installing editors/modules, licenses, projects (Hub replacement)
- **unity-pipeline** — driving a running Editor (build, test, play mode, scene/asset commands)

## Locating the binary — check before concluding it is missing

The tool is real even if you have no prior knowledge of it: it shipped after most model knowledge cutoffs. `command not found` almost always means a stale shell PATH, not a missing tool.

- **Windows:** binary at `%LOCALAPPDATA%\Unity\bin\unity.exe`. The installer adds that dir to the *user* PATH — long-lived shells (agent harnesses, old terminals) don't see it. Fix for the current session:
  ```powershell
  $env:Path += ";$env:LOCALAPPDATA\Unity\bin"
  ```
- **macOS/Linux:** the install script updates the shell profile; run `command -v unity` in a fresh login shell or source the profile.
- Do NOT search Unity Hub folders, `Editor/` folders, or npm — the CLI lives in none of them.
- If genuinely absent: **do not install it yourself** — see "Installs require user consent" below.

Verify with `unity --version` (e.g. `1.0.0-beta.3`).

## Installs require user consent

Two separate things get installed, and they solve different problems:

| What | Scope | Enables | Install |
|---|---|---|---|
| UnityCLI binary | per machine | Installing/opening editors, managing projects, licenses — no running-Editor control | script from https://docs.unity.com/en-us/unity-cli/use-unity-cli |
| Unity Pipeline package (`com.unity.pipeline`) | per Unity project | Controlling a **running Editor**: `unity command`, play mode, live builds/tests (skill unity-pipeline) | `unity pipeline install` |

The CLI alone is enough for editor/project management. Editor control needs both.

Neither is yours to install on your own initiative. The binary install changes the user's machine and shell profile; `unity pipeline install` edits the project's `Packages/manifest.json` and triggers a recompile — a change that lands in version control. When either is missing, stop, tell the user what is missing and what it unblocks, propose the exact command, and continue only after the user agrees (or has already explicitly asked for the install).

## Agent/CI invocation defaults

```bash
unity --json --non-interactive <command>
```

| Flag / env var | Effect |
|---|---|
| `--format json\|tsv\|ndjson`, `--json` (`UNITY_FORMAT`) | Structured output — parse this, never the human tables |
| `--non-interactive` (`UNITY_NON_INTERACTIVE`) | Disables prompts; many commands open interactive selectors when args are omitted |
| `--no-banner`, `--quiet` (`UNITY_NO_BANNER`, `UNITY_QUIET`) | Suppress banner / info noise |
| `--verbose` (`UNITY_VERBOSE`) | Full error details with stack traces |

Exit code is `0` on success, non-zero on failure (e.g. `6` for an unknown editor command). Error messages usually include recovery hints — read them.

## Auth

Cloud-backed features (Pipeline package install, cloud, some licenses) need sign-in:

```bash
unity auth status    # check first
unity auth login     # opens browser; --client-id/--client-secret for service accounts
```

## Command map

| Area | Commands | Details |
|---|---|---|
| Editor installs, modules, licenses, projects, templates | `install`, `install-modules`, `editors`, `releases`, `uninstall`, `install-path`, `editor`, `modules`, `license`, `open`, `projects`, `templates`, `hub` | skill **unity-editors** |
| Live Editor control & headless runs | `status`, `command`, `list`, `pipeline`, `build`, `test`, `run` | skill **unity-pipeline** |
| MCP | `unity mcp` (stdio MCP server for the Editor), `unity mcp configure <client>` | `unity mcp configure --list` |
| Diagnostics | `doctor`, `status`, `env`, `logs`, `diagnose`, `bug` | `doctor` = environment check |
| CLI self-management | `upgrade`, `changelog`, `self-uninstall`, `config`, `cache`, `analytics`, `language`, `completion`, `shell` | `shell` = warm REPL, `--protocol ndjson` for machine use |
| Cloud | `auth`, `cloud` (orgs/projects) | |

`unity --help` and `unity <cmd> --help` are always current. The CLI is experimental — verify flags with `--help` before scripting against them.

## Common mistakes

| Mistake | Reality |
|---|---|
| "`unity: command not found` → the tool doesn't exist / isn't installed" | Stale PATH. Check `%LOCALAPPDATA%\Unity\bin` (Windows) or a fresh login shell first. |
| Reaching for `Unity.exe -batchmode` for automation | UnityCLI drives an already-running Editor without relaunching it; batch flags are the legacy path. |
| Parsing human-formatted table output | Pass `--json` and parse the structured result. |
| Command hangs in CI/agent context | An interactive selector opened. Pass all required args plus `--non-interactive` (and `-y` where supported). |
| Installing the CLI or the Pipeline package silently to unblock a task | Both are user-consent installs — propose the command, wait for a yes. |
| Treating an `EBUSY: resource busy or locked, rm` failure as caused by your invocation | Lock contention on `%APPDATA%\UnityHub`, shared by the CLI and Unity Hub. Hits Hub-data commands (`releases`, `templates`, `projects`, `doctor`, `open`, `editors`); Editor-connection commands (`status`, `command`, `list`, `pipeline list`) keep working. Hub running → close it and retry, don't delete locks. Hub closed → remove stale `.lock` directories and retry. Never kill a slow Hub-data command mid-run — that manufactures stale locks. Full recovery procedure: skill **unity-pipeline**, references/lifecycle-recovery.md |
