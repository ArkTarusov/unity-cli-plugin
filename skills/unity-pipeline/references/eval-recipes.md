# eval recipes

`eval` executes arbitrary C# on the Editor main thread and is the standard escape hatch when a dedicated command is missing, hidden from the listing, or behaves unexpectedly. `eval_file` runs a file instead of an inline string.

Rules:

- The code is a statement block; end with `return <expr>;` to get a value back (in `result.result`). Compilation errors come back in `result.diagnostics`.
- Runs on the main thread — unavailable while the Editor is compiling, busy, or stuck in a modal dialog (see [lifecycle-recovery.md](lifecycle-recovery.md)).
- Quote carefully: the whole snippet is one `--code` argument; inner double quotes need shell escaping.
- Prefer a dedicated command when one exists — `eval` bypasses `--dry_run`/`--confirm` safety conventions.

## Force asset import / script compilation

Files created **outside** pipeline commands — shell redirection, an agent's file tools, external generators — are invisible to the AssetDatabase until an import runs: a `.cs` written that way is not compiled and `recompile` reports `up_to_date`. (`import_asset` does not help: its `source` argument imports an *external* file into the project.) After writing project files directly:

```bash
unity command eval --code "UnityEditor.AssetDatabase.Refresh(); return \"refreshed\";"
# then poll: unity command recompile_status
```

`write_text_file` and `create_script` import what they write themselves (`write_text_file` ends with `AssetDatabase.ImportAsset(ForceUpdate)`) and do not need this.

## Switch scenes without the save prompt

`EditorSceneManager.OpenScene` at API level never prompts — it silently discards unsaved changes (same behavior as the `open_scene` command):

```bash
unity command eval --code "var s = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(\"Assets/Scenes/Main.unity\", UnityEditor.SceneManagement.OpenSceneMode.Single); return s.name;"
```

Check `list_open_scenes` for `isDirty` first — see the scene-switch protocol in [lifecycle-recovery.md](lifecycle-recovery.md).

## Clean editor shutdown

`eval EditorApplication.Exit(0)` — but save first and read the caveats: the full procedure (and why the CLI's `Invalid response format` reply means success) lives in [lifecycle-recovery.md](lifecycle-recovery.md), section "Clean shutdown".

## Simulate input through the Input System

Try `simulate_pointer` / `simulate_key` first — they drive a virtual Input System device and cover most cases (schemas in [editor-commands.md](editor-commands.md), hidden from the CLI listing). Fall back to this recipe when code polls a specific real device (`Mouse.current`) and ignores the virtual one — it queues state on the real device (play mode, Input System package):

```bash
unity command eval --code "
var mouse = UnityEngine.InputSystem.Mouse.current;
var state = new UnityEngine.InputSystem.LowLevel.MouseState { position = new UnityEngine.Vector2(640, 360) };
state = state.WithButton(UnityEngine.InputSystem.LowLevel.MouseButton.Left, true);
UnityEngine.InputSystem.InputSystem.QueueStateEvent(mouse, state);
UnityEngine.InputSystem.InputSystem.Update();
return \"queued\";"
```

Same pattern for keyboards: `KeyboardState` + `QueueStateEvent(Keyboard.current, state)`.

## Read private/internal state

Serialized data is better read via `get_serialized_fields` / `get_component_properties`; reflection covers the rest:

```bash
unity command eval --code "
var go = UnityEngine.GameObject.Find(\"Player\");
var comp = go.GetComponent(\"PlayerController\");
var f = comp.GetType().GetField(\"_health\", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
return f.GetValue(comp).ToString();"
```

## Register a project-defined CLI command

Any static method in the project becomes a first-class command via attributes from the Pipeline package — auto-registered after compilation, listed with a full schema, callable like a built-in:

```csharp
using Unity.Pipeline.Commands;

public static class MyCommands
{
    [CliCommand("my_probe", "One-line description shown in the listing")]
    public static string Probe(
        [CliArg("text", "Argument description")] string text = "default")
    {
        return "echo: " + text;
    }
}
```

`CliCommandAttribute` also has `MainThreadRequired` and `RuntimeOnly` properties. For a recurring `eval` snippet, promoting it to a `[CliCommand]` gives typed arguments and discoverability.
