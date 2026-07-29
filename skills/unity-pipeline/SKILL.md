---
name: unity-pipeline
description: Use when automating a running Unity Editor from the terminal via UnityCLI — entering play mode, running builds or tests, executing editor commands for scenes, GameObjects, prefabs, assets, materials, or settings, adding or removing UPM packages in the running Editor, polling build/recompile/bake status — or when `unity command` or `unity status` reports no connected Editor or a missing Pipeline package, or the Editor hangs or shows a modal dialog.
---

# Driving the Unity Editor via UnityCLI

The Unity Pipeline package (`com.unity.pipeline`) runs a local HTTP server inside the Editor; `unity command` executes commands against it live — no relaunch, no batch mode. The exposed surface is full authoring (scenes, GameObjects, prefabs, materials, animation, script creation, packages, tests, builds) — the same command set MCP-based Unity integrations wrap, reachable without an MCP bridge. Prerequisite basics (PATH, `--json`, auth): see skill **unity-cli-core**.

## Argument syntax — the #1 source of silent failures

The **only** working form is `--flag value` with snake_case names:

```bash
unity command set_transform --gameobject Player --position "[1,2,3]"
```

Every other form — `key=value`, positional, `-- key=value`, kebab-case (`--save-path`) — is **dropped silently**: the command reports success and runs with defaults. Verified failure mode: a click command "succeeds" while clicking at (0,0); a `path=...` argument becomes a literally-named folder. Missing *required* parameters do fail loudly (400) — only unknown/malformed forms vanish.

Two mandatory habits:

1. **Verify the echo.** Every response carries `data.parameters` — the arguments the server actually received. After any mutating or surprising call, check your arguments are in there. An empty `parameters: {}` when you passed arguments means they were dropped.
2. **Verify the schema on doubt.** Argument names are not uniform across commands (`delete_asset --asset`, but `open_scene --path`). [references/editor-commands.md](references/editor-commands.md) lists the core commands with arguments; on a mismatch, the live truth is `unity --json command` (full JSON schemas, per-project).

## Prerequisites — check, don't install

Live control needs BOTH: UnityCLI on the machine AND the Unity Pipeline package (`com.unity.pipeline`) inside the target project. The CLI alone can install/open editors but cannot talk to a running Editor.

1. `unity status` — is an Editor running? GUI editors only: an editor launched with `-batchmode` serves commands but never appears here — probe it with `unity command --project-path <path>` instead.
2. `unity pipeline list` — does the project show **Pipeline: Installed**?

If the package is missing, **stop and ask the user** — do not run the install to unblock yourself. `unity pipeline install` edits the project's `Packages/manifest.json` and triggers a recompile: a project change that lands in version control. Propose the exact command (`unity pipeline install --project-path <path>`; requires `unity auth login`) and continue only after approval. Same rule for `unity pipeline upgrade`. `unity pipeline list-versions` shows available versions.

## Discover, then execute

```bash
unity status                     # connected Editors: port, project, version, PID, state
unity command                    # list commands the Editor exposes (or: unity list)
unity command <name> --param value ...
```

- **The command list is dynamic and incomplete.** It depends on package version, project code, and installed packages — and ten `RuntimeOnly` commands (`simulate_pointer`, `simulate_key`, `runtime_status`, `capture_runtime_element`, `set_timescale`, `set_target_framerate`, `quit`, `log`, `hotreload_status`, `cleanup_hotreload`), intended for Player connections (`--runtime`), are filtered out of the editor listing yet execute fine against the editor (`capture_runtime_element` only exists on Unity 6000.7+). Their schemas exist **only** in [references/editor-commands.md](references/editor-commands.md) (section "RuntimeOnly commands") — the CLI cannot show them. Absence from the list proves nothing; a genuinely unknown name fails with exit code 6 and prints the available list.
- Projects can define their own commands: a static method with `[CliCommand]`/`[CliArg]` attributes auto-registers after compilation, schema included ([references/eval-recipes.md](references/eval-recipes.md)).
- Multiple Editors open → disambiguate with `--project-path <path>` (env `UNITY_PROJECT_PATH`).
- Slow operations → raise `--timeout <seconds>` (default 30).
- `--json` wraps results as `{success, command, data: {parameters, result}}`.

Play-mode state is verified with `editor_status` (its `playMode` field) — `editor_play`/`editor_stop` only mutate.

## Conventions the commands follow

| Convention | Meaning |
|---|---|
| `--confirm true` | Destructive commands (delete_asset, set_player_settings, package_add/remove, clears/bakes) refuse to run without it |
| `--dry_run true` | Preview what a mutating command would do, without doing it |
| Async + status polling | Long operations return immediately; poll their status command: `build`→`build_status`, `recompile`→`recompile_status`, `run_tests`→`test_status`, bakes→`*_bake_status`, `switch_build_target`→`switch_build_target_status`, packages→`package_status` |
| Authoring root | File/asset-creating commands resolve and confine bare paths under a base folder inside `Assets/`; `get_authoring_root` / `set_authoring_root --root Assets` for full project access |
| Recompile before use | `create_script` produces a type only after `recompile` completes — poll `recompile_status`, then `attach_script`. Files written by anything **other** than pipeline commands (shell, external tools) stay invisible to the AssetDatabase until `eval AssetDatabase.Refresh()` ([references/eval-recipes.md](references/eval-recipes.md)); `write_text_file` imports what it writes itself |
| Main-thread-free subset | `console` and all `*_status` poll commands respond even while the Editor main thread is busy — use them to tell "busy" from "stuck" ([references/lifecycle-recovery.md](references/lifecycle-recovery.md)) |

## Scene changes lose data silently

`open_scene` (and API-level `EditorSceneManager.OpenScene`) **discards unsaved changes without any prompt**. Protocol before replacing scenes: `list_open_scenes` → any `isDirty` scene → `save_all`, or explicitly decide to discard; if the dirty work isn't yours, ask. Details and editor shutdown/recovery: [references/lifecycle-recovery.md](references/lifecycle-recovery.md).

## Hangs and modal dialogs

A modal dialog blocks the Editor main thread: commands time out while `unity status` still reports `ready` (heartbeat needs no main thread). Diagnosis table, dialog inventory, prevention, clean-shutdown (`save_all` + `eval EditorApplication.Exit(0)`), and crash-recovery procedure: [references/lifecycle-recovery.md](references/lifecycle-recovery.md).

## Verification and capture

- State read while the Editor is playing measures whatever `Update` wrote last frame — for stable assertions, read with play mode stopped.
- `capture_game_view` renders a **camera** to PNG (base64 inline by default, `--save_path` for a file); `capture_scene_view` renders the Scene View; `screenshot --view game|scene` captures the actual view to a file. UI on a Screen Space Overlay canvas is not part of any camera's render — camera-based capture misses it.
- `simulate_pointer` / `simulate_key` drive a **virtual** Input System device (hidden from the listing — schemas in [references/editor-commands.md](references/editor-commands.md)). Passing arguments in any form other than `--x 10 --y 20` silently clicks at (0,0) — the argument-drop trap above. If gameplay code still doesn't react (e.g. it polls a specific device), queue state on the real device via `eval` ([references/eval-recipes.md](references/eval-recipes.md)).

## Tests

Against a running Editor: `unity command run_tests --mode all|editor|playmode` (async with `--async_tests true`, poll `test_status`). Headless `unity test` uses different mode names: `--mode EditMode|PlayMode`. The two are not interchangeable.

## Headless (no running Editor)

These spawn an editor in batch mode instead of connecting to one:

```bash
unity build . --target StandaloneWindows64 --execute-method Builder.PerformBuild -o ./out
unity test . --mode EditMode --filter "MyNamespace" --output results.xml --timeout 1800
unity run . -- -executeMethod Tool.Run -quit      # raw editor args after --
unity run . --command my_command -- --arg value   # registered command, one-shot: boots, runs, exits
```

- `unity build` **requires** `--execute-method` — Unity has no built-in command-line build.
- `--allow-install` downloads and installs the project's editor version if missing — multi-gigabyte; don't pass it without the user's OK.
- `unity test` writes an NUnit XML report; set `--timeout` or a hung run never exits.
- `unity run --command` mixes the editor log into stdout — parse with `--format ndjson`.
- Against a **running** Editor, prefer `unity command build` / `unity command run_tests` (async, editor stays open).

### Resident headless editor (agent / SSH build box)

For many commands against one project, a fresh batch boot per command is wasteful. Launch the editor binary with `-batchmode` and **no `-quit`** — it loads the project, stays resident, and serves the Pipeline API like a GUI editor (~sub-second round-trips, no domain reload per call):

```bash
"<editor-binary>" -batchmode -projectPath <project> -logFile editor.log &
# PowerShell: Start-Process "<editor-binary>" -ArgumentList '-batchmode','-projectPath','<project>','-logFile','editor.log'
unity command --project-path <project>       # reachability probe + command list
```

- Such an editor is **invisible to `unity status` and `unity editors running`** (its lockfile heartbeat differs from a GUI editor's) — always target it via `--project-path` and probe with `unity command`/`unity list`, never `status`.
- A bare `unity run <project>` (no `--command`) is NOT a way to get one: batch runs to completion and exits.
- Any resident editor — batch or GUI — holds a license seat until it exits; one-shot `unity run --command` releases it on exit.
- Binary paths per install: `unity editors -i` (the `location` field). Launching directly also sidesteps `unity open` EBUSY failures ([references/lifecycle-recovery.md](references/lifecycle-recovery.md)).

## Common mistakes

| Mistake | Reality |
|---|---|
| Passing `key=value`, positional, or kebab-case arguments | Silently dropped; command "succeeds" with defaults. Only `--snake_case value` works; check the `parameters` echo |
| Firing `build`/`bake`/`run_tests` and treating the immediate return as completion | They queue; only the `*_status` command tells the outcome (full BuildReport in `build_status`) |
| "Command absent from the list → it doesn't exist" | The list is per-project AND hides some registered commands; a truly unknown name exits with code 6 |
| Switching scenes without checking `isDirty` | `open_scene` silently discards unsaved changes |
| Writing assets outside the authoring root | Paths are confined under it; check `get_authoring_root`, widen with `set_authoring_root` only when intended |
| Using `unity build` while an Editor already runs on the project | Spawning a second editor instance fails or fights the open one; use `unity command build` against the running Editor |
| Play-mode/domain-reload race after `package_add` or script edits | Poll `recompile_status` before the next command |
| Treating `unity status: ready` as "Editor is responsive" | It only means the server process is alive; the main thread may be stuck — see [references/lifecycle-recovery.md](references/lifecycle-recovery.md) |
| Concluding "no Editor" from an empty `unity status` | A `-batchmode` editor serves commands but never registers in `status` — probe `unity command --project-path <path>` before spawning a new editor |
