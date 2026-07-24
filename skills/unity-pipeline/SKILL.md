---
name: unity-pipeline
description: Use when automating a running Unity Editor from the terminal via UnityCLI — entering play mode, running builds or tests, executing editor commands for scenes, GameObjects, prefabs, assets, materials, packages, or settings, polling build/recompile/bake status — or when `unity command` or `unity status` reports no connected Editor or a missing Pipeline package.
---

# Driving the Unity Editor via UnityCLI

The Unity Pipeline package (`com.unity.pipeline`) runs a local HTTP server inside the Editor; `unity command` executes commands against it live — no relaunch, no batch mode. Prerequisite basics (PATH, `--json`, auth): see skill **unity-cli-core**.

## Prerequisites — check, don't install

Live control needs BOTH: UnityCLI on the machine AND the Unity Pipeline package (`com.unity.pipeline`) inside the target project. The CLI alone can install/open editors but cannot talk to a running Editor.

1. `unity status` — is an Editor running?
2. `unity pipeline list` — does the project show **Pipeline: Installed**?

If the package is missing, **stop and ask the user** — do not run the install to unblock yourself. `unity pipeline install` edits the project's `Packages/manifest.json` and triggers a recompile: a project change that lands in version control. Propose the exact command (`unity pipeline install --project-path <path>`; requires `unity auth login`) and continue only after approval. Same rule for `unity pipeline upgrade`. `unity pipeline list-versions` shows available versions.

## Discover, then execute

```bash
unity status                     # connected Editors: port, project, version, PID, state
unity command                    # list commands the Editor exposes (or: unity list)
unity command <name> --param value ...
```

- **The command list is dynamic** — it depends on package version, project code, and installed packages (e.g. Timeline-only commands). Never assume a command exists; list first. A wrong name fails with exit code 6 and prints the full available list.
- Multiple Editors open → disambiguate with `--project-path <path>` (env `UNITY_PROJECT_PATH`).
- Slow operations → raise `--timeout <seconds>` (default 30).
- `--json` wraps results as `{success, command, data: {result}}`.

Example round-trip:

```bash
unity --json command get_scene_hierarchy --project-path ./MyProject
unity command editor_play
unity command find_gameobjects --query "Player"
```

Play-mode state is verified with `editor_status` (its `playMode` field: `stopped` / `playing`) — `editor_play`/`editor_stop` only mutate.

## Conventions the commands follow

| Convention | Meaning |
|---|---|
| `--confirm true` | Destructive commands (delete_asset, set_player_settings, package_add/remove, clears/bakes) refuse to run without it |
| `--dry_run true` | Preview what a mutating command would do, without doing it |
| Async + status polling | Long operations return immediately; poll their status command: `build`→`build_status`, `recompile`→`recompile_status`, `run_tests`→`test_status`, bakes→`lighting_bake_status` / `navmesh_bake_status` / `occlusion_bake_status`, `switch_build_target`→`switch_build_target_status`, packages→`package_status` |
| Authoring root | File/asset-creating commands resolve and confine bare paths under a base folder inside `Assets/`; `get_authoring_root` / `set_authoring_root --root Assets` for full project access |
| Recompile before use | `create_script` produces a type only after `recompile` completes — poll `recompile_status`, then `attach_script` |

Full categorized command snapshot: [references/editor-commands.md](references/editor-commands.md).

## Headless (no running Editor)

These spawn an editor in batch mode instead of connecting to one:

```bash
unity build . --target StandaloneWindows64 --execute-method Builder.PerformBuild -o ./out
unity test . --mode EditMode --filter "MyNamespace" --output results.xml --timeout 1800
unity run . -- -executeMethod Tool.Run -quit      # raw editor args after --
unity run . --command my_command -- --arg value   # registered command, headless
```

- `unity build` **requires** `--execute-method` — Unity has no built-in command-line build.
- `--allow-install` downloads and installs the project's editor version if missing — multi-gigabyte; don't pass it without the user's OK.
- `unity test` writes an NUnit XML report; set `--timeout` or a hung run never exits.
- Against a **running** Editor, prefer `unity command build` / `unity command run_tests` (async, editor stays open).

## Common mistakes

| Mistake | Reality |
|---|---|
| Firing `build`/`bake`/`run_tests` and treating the immediate return as completion | They queue; only the `*_status` command tells you the outcome (full BuildReport in `build_status`) |
| Assuming a fixed command list | The list is per-project and per-version; `unity command` is the source of truth |
| Writing assets outside the authoring root | Paths are confined under it; check `get_authoring_root`, widen with `set_authoring_root` only when intended |
| Using `unity build` while an Editor already runs on the project | Spawning a second editor instance fails or fights the open one; use `unity command build` against the running Editor |
| Play-mode/domain-reload race after `package_add` or script edits | Poll `recompile_status` before the next command |
