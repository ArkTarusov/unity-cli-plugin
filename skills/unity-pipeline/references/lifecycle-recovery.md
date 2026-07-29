# Editor lifecycle: hangs, modal dialogs, crash recovery

Verified on Unity `6000.4`, Unity Pipeline package `0.4.0-exp.1`, UnityCLI `1.0.0-beta.3`, Windows.

## Why the editor "hangs"

The Pipeline server runs inside the Editor; almost every command needs the Editor main thread. Anything that blocks the main thread — a **modal dialog** above all — makes commands time out while the process stays alive. `eval` cannot help: it needs the main thread too. Recovery is only possible from outside the process.

## Diagnosing: busy vs stuck vs modal

Signals, cheapest first:

1. `unity status` — process-level heartbeat, does **not** need the main thread. `ready` only means the server process is alive.
2. `console` (and every `*_status` poll command: `recompile_status`, `test_status`, `build_status`, bake/package statuses) — marked main-thread-free; they respond even while the main thread is busy.
3. `editor_status` — needs the main thread. This is the actual liveness probe.
4. Top-level windows of the Editor PID (from `unity status`) — a dialog is a window of class `#32770` owned by the same process; the main window class is `UnityContainerWndClass`.

Decision table:

| `unity status` | `console` | `editor_status` | `#32770` window | Meaning |
|---|---|---|---|---|
| ready | responds | responds | no | Editor fine |
| ready | responds | times out | no | Main thread busy (import/compile/bake) — wait, poll `*_status` |
| ready | responds | times out | yes | **Modal dialog mid-session** — main thread is in a modal loop |
| no instances | — | — | yes | **Modal during startup** — server never started; `unity status` misleadingly suggests installing the Pipeline package |
| no instances | — | — | no | Editor not running (or still loading) |

Windows: enumerate windows of the PID (PowerShell + `EnumWindows`/`GetClassName`, or any window-listing tool) and read the dialog title/text via UIAutomation. `FindWindow` by title is unreliable for Unity dialogs — match by PID + class instead. Unity dialogs are IMGUI-drawn: UIAutomation exposes text and button names but no `InvokePattern`; programmatic dismissal requires a physical mouse click at the button's bounding rect. On a user's machine, don't click — report the dialog title and options and let the user answer it.

## Known modal dialogs

| Dialog | Trigger | Prevention |
|---|---|---|
| Save-changes prompt | Editor quit / scene switch from the **UI** with unsaved changes | Follow the scene-switch protocol below; note the CLI `open_scene` command does *not* prompt — it silently discards |
| "Recovering Scene Backups" (Yes/No: copy backups to `Assets/_Recovery/`?) | Startup after the previous Editor process was killed while `Temp/__Backupscenes/` existed. The backup is written on **entering play mode**; a merely-dirty scene never writes one | Prefer a clean exit (below). After a forced kill, delete `<project>/Temp/__Backupscenes/` before relaunching — verified to suppress the dialog |

## Scene-switch protocol

`open_scene` **silently discards unsaved changes** of the scenes it replaces — no prompt, no warning, no error. Before any `open_scene` / scene-replacing operation:

```bash
unity command list_open_scenes        # check isDirty on every open scene
unity command save_all                # keep the changes …
# … or deliberately discard them (state so explicitly), then:
unity command open_scene --path Scenes/Other
```

If a dirty scene contains work you didn't author in this session, stop and ask before either saving or discarding.

## Clean shutdown

```bash
unity command save_all
unity command eval --code "UnityEditor.EditorApplication.Exit(0); return \"exiting\";"
```

`EditorApplication.Exit(0)` terminates immediately: no save prompt, no crash marker, no recovery dialog on next start (verified). The CLI call itself reports `Invalid response format from Pipeline server` — expected, the connection dies mid-response; it is the success signature, not an error.

Kill the process only when the main thread is already stuck (modal, deadlock). After a kill, apply the `Temp/__Backupscenes/` cleanup above. Note the changes in a dirty scene die with the process either way — a kill loses them without any recovery prompt unless play mode had been entered.

## Launching the editor without Unity Hub

`unity open` may fail with `EBUSY` (see below). The editor binary works directly and needs no Hub:

```powershell
& "C:\Program Files\Unity\Hub\Editor\<version>\Editor\Unity.exe" -projectPath "<project>"
```

The binary path of a running editor: `(Get-Process -Id <pid>).Path`. When no editor runs, `unity editors -i` lists install paths — unless it is itself EBUSY-broken, in which case the default install root above usually applies.

Add `-batchmode` (and no `-quit`) to run headless: the editor stays resident and serves Pipeline commands, but **never registers in `unity status` or `unity editors running`** — its lockfile heartbeat differs from a GUI editor's. Probe it with `unity command --project-path <project>`; the `unity status`-based signals in the diagnosis table above only apply to GUI editors. A resident editor of either kind holds a license seat until it exits.

Startup takes minutes on large projects: poll `unity status` until `state: ready` (GUI), or poll `unity command --project-path <project>` (batch). If it never becomes ready, check for a `#32770` window — a startup dialog blocks the server from starting at all.

## `EBUSY: resource busy or locked, rm`

Affects Hub-data commands (`editors`, `releases`, `templates`, `projects`, `doctor`, `open`) — sometimes as an instant error, sometimes as a multi-minute hang. Editor-connection commands (`status`, `command`, `list`, `pipeline list`) keep working.

Mechanism, in one line: the CLI and Unity Hub share `%APPDATA%\UnityHub\`; writes there are guarded by transient lock **directories** (`<file>.lock`, created per operation, removed right after), and EBUSY means removing a lock directory failed because another process holds a handle to it.

Recovery — check whether Unity Hub is running:

- **Hub running** — live contention for the shared store (EBUSY hits even with no stale locks at all). Don't touch the locks: close Hub, or stop running Hub-data commands alongside it, then retry.
- **Hub not running** — any remaining `.lock` directories are stale (their owner is dead): remove them and retry.

```powershell
Remove-Item "$env:APPDATA\UnityHub\*.lock" -Recurse -Force   # only with Hub closed and no CLI running
```

Do not kill a slow Hub-data command mid-run (shell timeout included): a CLI invocation killed mid-operation leaves its own fresh lock directories behind, manufacturing the stale state above for every later run.
