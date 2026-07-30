<!--
GENERATED FILE — do not hand-edit any part of it, including the preamble and the
Contents list. Regenerate from the com.unity.pipeline package sources with:
    dotnet run --project tools/CommandRefGen -- --version latest
No Unity Editor and no `unity --json command` dump is involved. Field notes that are
not derivable from the package source (observed behaviour, links to other skill files)
live in tools/CommandRefGen/annotations.json — add them there, not here.

This copy still carries the content of the retired two-step pipeline (a live-editor dump
plus a hand-spliced RuntimeOnly section). The first CommandRefGen run replaces the whole
file, including these lines, and its diff summary reports what moved.
-->

# Editor command reference

Generated from `unity --json command` against Unity Pipeline package `0.4.0-exp.1` on Unity `6000.4`. The live list is dynamic (package version, project code, optional packages, project-defined `[CliCommand]` methods) — on a name/argument mismatch, re-check against the live output of `unity --json command`; this file covers the common core.

**The listing is not exhaustive — this file is more complete than `unity command`.** Commands whose `[CliCommand]` attribute sets `RuntimeOnly = true` are filtered out of the editor listing — they are designed for Unity **Player** connections (`unity command --runtime <player>`) — yet the editor server still executes them when called by name, in edit mode and play mode alike. The CLI never reveals their schemas; the [RuntimeOnly commands](#runtimeonly-commands-hidden-from-the-listing) section below, extracted from the package source, is the only schema source for them. Absence from the listing is not proof a command does not exist — a genuinely unknown name fails with exit code 6.

Argument conventions: pass every argument as `--name value` (snake_case). `*` marks a required argument. Destructive commands need `--confirm true`; most mutating commands accept `--dry_run true`; async commands pair with a `*_status` poll command.

## Contents

[Editor & play mode](#editor--play-mode) · [Capture](#capture) · [Console & logs](#console--logs) · [Scenes](#scenes) · [GameObjects & components](#gameobjects--components) · [Prefabs](#prefabs) · [Assets & files](#assets--files) · [Scripts & compilation](#scripts--compilation) · [Tests](#tests) · [Build](#build) · [Packages (UPM)](#packages-upm) · [Materials & shaders](#materials--shaders) · [Animation & Timeline](#animation--timeline) · [Lighting bake](#lighting-bake) · [NavMesh bake](#navmesh-bake) · [Occlusion bake](#occlusion-bake) · [Project settings](#project-settings) · [RuntimeOnly commands](#runtimeonly-commands-hidden-from-the-listing)

## Editor & play mode

### editor_focus
Bring the Unity Editor window to the foreground
- *(no arguments)*

### editor_pause
Pause Unity Editor play mode
- *(no arguments)*

### editor_play
Enter Unity Editor play mode
- *(no arguments)*

### editor_status
Get detailed Unity Editor status and state information
- *(no arguments)*

### editor_stop
Exit Unity Editor play mode
- *(no arguments)*

### get_selection
Read the current Editor selection as structured object identities.
- *(no arguments)*

### menu
Execute an Editor menu item by path, or list available items when no path is given
- `path` String (default "") — Menu item path to execute, e.g. "Assets/Reimport All". Omit to list available menu items.

### save_all
Save all open scenes that have unsaved changes.
- *(no arguments)*

### set_autotick
Keep the editor ticking while unfocused by forcing EditorApplication.SignalTick at a throttled rate
- `enable` Boolean (default true) — Enable (true) or disable (false) auto-tick mode
- `interval_ms` Int32 (default 16) — Minimum milliseconds between forced ticks. 0 = every update (max rate, pegs a CPU core). Default 16 (~60Hz).

### set_selection
Set the Editor selection to the given assets/scene objects.
- `instance_ids` ObjectId[] — Scene/loaded object instance IDs to select.
- `paths` String[] — Asset paths to select (e.g. Assets/Foo.prefab).

## Capture

### capture_game_view
Render a camera to a PNG. Returns it inline as base64, unless save_path is set (path-only result; pass include_inline_image=true to get both).
- `width` Int32 (default 1280) — Output width in px (default 1280; capped 4096).
- `height` Int32 (default 720) — Output height in px (default 720; capped 4096).
- `camera` String — Optional camera name; defaults to Camera.main, else the first enabled camera.
- `save_path` String — Optional project-relative path to write the PNG (e.g. Screenshots/foo.png). When set, the result omits the inline base64 image unless include_inline_image=true.
- `include_inline_image` Boolean (default false) — Also return the image inline as base64 when save_path is set (default false: path-only result). Only meaningful together with save_path.
- `max_resolution` Int32 (default 0) — Cap on the inline image's longest edge (e.g. 512). Only applies when an inline image is returned (no save_path, or save_path + include_inline_image=true); the save_path file keeps the requested resolution.

### capture_scene_view
Render the active Scene View to a PNG. Returns it inline as base64, unless save_path is set (path-only result; pass include_inline_image=true to get both).
- `width` Int32 (default 1280) — Output width in px (default 1280; capped 4096).
- `height` Int32 (default 720) — Output height in px (default 720; capped 4096).
- `save_path` String — Optional project-relative path to write the PNG (e.g. Screenshots/foo.png). When set, the result omits the inline base64 image unless include_inline_image=true.
- `include_inline_image` Boolean (default false) — Also return the image inline as base64 when save_path is set (default false: path-only result). Only meaningful together with save_path.
- `max_resolution` Int32 (default 0) — Cap on the inline image's longest edge (e.g. 512). Only applies when an inline image is returned (no save_path, or save_path + include_inline_image=true); the save_path file keeps the requested resolution.

### screenshot
Capture the Scene or Game view as a PNG and return its file path
- `view` String (default "game") — Which view to capture: 'game' (default) or 'scene'
- `output` String (default "") — Output PNG path (absolute, or relative to the project root). Defaults to a timestamped file under <project>/Temp/pipeline-screenshots/.
- `width` Int32 (default 0) — Output width in pixels. 0 (default) uses the view camera's current width.
- `height` Int32 (default 0) — Output height in pixels. 0 (default) uses the view camera's current height.

## Console & logs

### clear_console
Clear the captured log buffer and the Unity Editor console.
- *(no arguments)*

### console
Get captured Unity console output (Editor or Player; supports tail, level filtering, and follow via a cursor) *(works while main thread is busy)*
- `tail` Int32 (default 100) — Maximum number of most-recent entries to return
- `level` String (default "log") — Minimum severity to include: log | warn | error
- `since` Int64 (default -1) — Cursor: only return entries newer than this seq. Use the 'cursor' from a previous response to follow.

### get_console_logs
Read recently captured Editor console logs (structured).
- `severity` String (default "all") — Filter: all | log | warning | error. 'all' = every entry; 'log' = Log only; 'warning' = Warning only; 'error' = Error/Exception/Assert only.
- `limit` Int32 (default 100) — Max entries to return (most-recent first), capped at 1000.

## Scenes

### add_scene_to_build
Add a scene to the Build Settings scene list (idempotent). Optionally enable it.
- `path`\* String — Scene path to add (authoring-root relative; Assets/ prefix and .unity optional).
- `enabled` Boolean (default true) — Whether the scene is enabled in the build list.

### create_scene
Create a new scene and save it to the given path under the authoring root.
- `path`\* String — Scene path relative to the authoring root (default Assets/); the Assets/ prefix and the .unity extension are optional. e.g. Scenes/Level1
- `additive` Boolean (default false) — Open the new scene additively alongside currently open scenes instead of replacing them.
- `template` String (default "empty") — Initial contents: 'empty' (default) for a blank scene, or 'default' to seed a Main Camera + Directional Light matching Unity's built-in 3D template.

### get_scene_hierarchy
Return the GameObject tree of an open scene (or the active scene). Each node carries instanceId + hierarchyPath usable by GameObject commands.
- `path` String — Path of the open scene to snapshot (authoring-root relative; Assets/ prefix and .unity optional). Omit for the active scene.

### list_open_scenes
List all currently open scenes with their load/active/dirty state.
- *(no arguments)*

### open_scene
Open an existing scene from the given path.
- `path`\* String — Scene path relative to the authoring root (default Assets/); the Assets/ prefix and the .unity extension are optional.
- `additive` Boolean (default false) — Open additively alongside currently open scenes instead of replacing them.

### remove_scene_from_build
Remove a scene from the Build Settings scene list (idempotent).
- `path`\* String — Scene path to remove (authoring-root relative; Assets/ prefix and .unity optional).

### save_scene
Save an open scene. Saves the active scene when no path is given.
- `path` String — Path of the open scene to save (authoring-root relative; Assets/ prefix and .unity optional). Omit to save the active scene.

### set_active_scene
Set which open scene is the active scene (new objects are created in the active scene).
- `path`\* String — Path of an already-open scene to make active (authoring-root relative; Assets/ prefix and .unity optional).

## GameObjects & components

### add_component
Add a component (by type name) to a GameObject.
- `target`\* ObjectRef — Handle of the GameObject.
- `type`\* String — Component type name (e.g. 'Rigidbody' or 'UnityEngine.Camera').

### attach_script
Add a MonoBehaviour to a GameObject by its (compiled) type name OR by its script asset path. Provide exactly one of 'type' or 'script'. If the type isn't compiled yet, returns a recoverable error: recompile, poll recompile_status, then retry.
- `target`\* ObjectRef — Reference to the GameObject to add the component to (globalId/path/guid/instanceId/hierarchyPath).
- `type` String — Component type name to add, e.g. PlayerController or Game.Player.PlayerController. Must already be compiled. Mutually exclusive with 'script'.
- `script` String — Script asset path, e.g. 'Assets/Pool/Scripts/CueShooter.cs'. The backing class is resolved via MonoScript.GetClass(), so the class name may differ from the filename. Mutually exclusive with 'type'.

### create_gameobject
Create an empty GameObject or a built-in primitive (cube/sphere/capsule/cylinder/plane/quad) in the active scene.
- `name` String — Name for the new GameObject. Defaults to 'GameObject' (or the primitive name).
- `primitive` String — Optional primitive type: cube, sphere, capsule, cylinder, plane, quad. Omit for an empty GameObject.
- `parent` ObjectRef — Optional parent handle (globalId/path/guid/instanceId/hierarchyPath). The new object becomes a child of it.

### create_gameobjects
Batch-create N empty GameObjects or primitives in one call. Optional positions/rotations/scales are arrays of [x,y,z] (length must equal count). Returns the created identities.
- `name` String — Base name. With count>1 and no explicit names, objects are suffixed Name1..NameN.
- `primitive` String — Optional primitive type: cube, sphere, capsule, cylinder, plane, quad. Omit for empty GameObjects.
- `parent` ObjectRef — Optional parent handle. Every created object becomes a child of it.
- `count` Int32 (default 1) — How many GameObjects to create. Default 1.
- `positions` Single[][] — Local positions, one [x,y,z] per object. Length must equal count when supplied.
- `rotations` Single[][] — Local Euler rotations (degrees), one [x,y,z] per object. Length must equal count when supplied.
- `scales` Single[][] — Local scales, one [x,y,z] per object. Length must equal count when supplied.

### delete_gameobject
Delete a GameObject from the scene (reversible via Undo).
- `target`\* ObjectRef — Handle of the GameObject to delete.

### find_gameobjects
Find GameObjects in loaded scenes by name, tag, component type, and/or hierarchy path (filters are combined). Returns structured identities.
- `name` String — Exact name to match.
- `tag` String — Tag to match (e.g. 'Player').
- `type` String — Component type name to match (e.g. 'Rigidbody', 'UnityEngine.Camera').
- `hierarchy_path` String — Exact hierarchy path to match (e.g. '/Root/Child').
- `include_inactive` Boolean (default true) — Include inactive GameObjects. Default true.

### get_component_properties
Get a component's serialized properties as a JSON map. Address the component by handle, or by GameObject handle + type.
- `target`\* ObjectRef — Handle of the component, OR of the GameObject when 'type' is given.
- `type` String — Component type name on the target GameObject (omit when 'target' is a component handle).

### get_serialized_fields
Read serialized fields of a component/asset. Returns each top-level field's name, type and value (object references are returned as re-usable handles). Pass 'field' to read a single SerializedProperty path.
- `target`\* ObjectRef — Reference to the component or asset to read (globalId/path/guid/instanceId/hierarchyPath). May be a GameObject when 'component' is given.
- `field` String — Optional single SerializedProperty path to read (e.g. 'speed' or 'items.Array.data[0]'). Omit to read all top-level fields.
- `component` String — Component type name on the target GameObject (e.g. 'Rigidbody'). Use when 'target' is a GameObject; omit when 'target' is already a component handle.

### remove_component
Remove a component from a GameObject. Provide either a component handle (target) or a GameObject handle (target) plus a type name.
- `target`\* ObjectRef — Handle of the component to remove, OR of the GameObject when 'type' is given.
- `type` String — Component type name to remove from the target GameObject (omit when 'target' already points at a component).

### rename_gameobject
Rename a GameObject.
- `target`\* ObjectRef — Handle of the GameObject.
- `name`\* String — New name.

### set_active
Set a GameObject's active self-state (activeSelf).
- `target`\* ObjectRef — Handle of the GameObject.
- `active`\* Boolean — Desired active state.

### set_component_properties
Set serialized properties on a component (one Undo step). 'properties' maps property name -> value; object references accept an ObjectRef handle.
- `target`\* ObjectRef — Handle of the component, OR of the GameObject when 'type' is given.
- `properties`\* JObject — Map of serialized property name to value. Vectors/colors are arrays; object refs are handle objects.
- `type` String — Component type name on the target GameObject (omit when 'target' is a component handle).

### set_layer
Set a GameObject's layer by name or numeric index (0-31).
- `target`\* ObjectRef — Handle of the GameObject.
- `layer`\* String — Layer name (e.g. 'UI') or numeric index 0-31.

### set_parent
Reparent a GameObject under a new parent, or detach it to scene root when no parent is given.
- `target`\* ObjectRef — Handle of the GameObject to reparent.
- `parent` ObjectRef — Handle of the new parent. Omit (or empty) to move the object to the scene root.
- `world_position_stays` Boolean (default true) — Keep the object's world position when reparenting. Default true.

### set_serialized_field
Set a serialized field on a component/asset. Supports primitives, enums, Vector/Color/Rect/Bounds, object references (value = an ObjectRef: asset by guid/fileId/path or scene object by instanceId/hierarchyPath), and array elements via 'name.Array.data[i]' (or 'name.Array.size' to resize).
- `target`\* ObjectRef — Reference to the component or asset to modify (globalId/path/guid/instanceId/hierarchyPath). May be a GameObject when 'component' is given.
- `field`\* String — SerializedProperty path, e.g. 'speed', 'settings.speed', or 'waypoints.Array.data[0]'.
- `value`\* JToken — JSON value to assign. For object references pass an ObjectRef object (or null to clear). For enums pass the value name.
- `component` String — Component type name on the target GameObject (e.g. 'Rigidbody'). Use when 'target' is a GameObject; omit when 'target' is already a component handle.

### set_tag
Set a GameObject's tag (the tag must already exist in the project).
- `target`\* ObjectRef — Handle of the GameObject.
- `tag`\* String — Tag to assign (must exist in the Tag Manager).

### set_transform
Set a GameObject's local position/rotation(euler)/scale. Omitted channels are left unchanged.
- `target`\* ObjectRef — Handle of the GameObject to modify.
- `position` Single[] — Local position as [x,y,z].
- `rotation` Single[] — Local rotation as Euler angles [x,y,z] in degrees.
- `scale` Single[] — Local scale as [x,y,z].

## Prefabs

### apply_prefab_overrides
Apply a prefab instance's overrides back to its source prefab asset.
- `instance`\* ObjectRef — Reference to a prefab instance GameObject in a scene (instanceId/hierarchyPath/globalId).

### create_prefab
Save a GameObject as a prefab asset at a project path; the source becomes a connected instance.
- `source`\* ObjectRef — Reference to the source GameObject to save as a prefab (globalId/path/guid/instanceId/hierarchyPath).
- `path`\* String — Prefab asset path relative to the authoring root (the Assets/ prefix is optional and the .prefab extension is added if missing). e.g. Prefabs/Enemy or Prefabs/Enemy.prefab

### create_prefab_variant
Create a prefab variant asset that inherits from a base prefab.
- `base`\* ObjectRef — Reference to the base prefab asset (path/guid/globalId).
- `path`\* String — Variant prefab asset path relative to the authoring root (.prefab added if missing).

### instantiate_prefab
Instantiate a prefab asset into a loaded scene and return the created instance.
- `prefab`\* ObjectRef — Reference to the prefab asset to instantiate (path/guid/globalId).
- `scene_path` String — Optional path of a loaded scene to instantiate into; defaults to the active scene.
- `name` String — Optional name for the created instance; defaults to the prefab name.

### revert_prefab_overrides
Revert a prefab instance's overrides so it matches its source prefab asset.
- `instance`\* ObjectRef — Reference to a prefab instance GameObject in a scene (instanceId/hierarchyPath/globalId).

### save_prefab_contents
Open a prefab asset in an isolated prefab stage, apply a declarative edit, and save it back (nested-prefab safe).
- `prefab`\* ObjectRef — Reference to the prefab asset to edit (path/guid/globalId).
- `rename_child` String — Optional child name (relative path under the root, e.g. 'Body/Head') to rename.
- `new_name` String — New name for the child identified by rename_child.
- `set_active_child` String — Optional child name (relative path under the root) whose active state to set.
- `active` Boolean (default true) — Active state to apply when set_active_child is provided.

### unpack_prefab
Unpack a prefab instance into plain GameObjects (outermost level or completely).
- `instance`\* ObjectRef — Reference to a prefab instance GameObject in a scene (instanceId/hierarchyPath/globalId).
- `completely` Boolean (default false) — If true, unpack all nested prefab levels (Completely); if false, only the outermost level (OutermostRoot).

## Assets & files

### copy_asset
Copy an asset to a new path under the authoring root. The copy gets a fresh GUID.
- `asset`\* ObjectRef — Reference to the asset to copy (path / guid / globalId).
- `destination`\* String — Destination asset path relative to the authoring root, including extension. The Assets/ prefix is optional.
- `confirm` Boolean (default false) — Required (true) only when overwriting an existing asset at the destination path.
- `dry_run` Boolean (default false) — If true, validate inputs and report what would be copied without writing anything.

### create_asset
Create a new ScriptableObject (or other UnityEngine.Object) asset of the given type at a path under the authoring root.
- `path`\* String — Asset path relative to the authoring root, including extension (e.g. Data/Config.asset or Materials/Wall.mat). The Assets/ prefix is optional.
- `type`\* String — Fully-qualified or short type name to instantiate (e.g. UnityEngine.Material, MyGame.GameConfig). Must derive from UnityEngine.Object and be creatable.
- `shader` String — Material-only (ignored otherwise): shader name to assign (e.g. Standard, "Universal Render Pipeline/Lit"). When omitted, defaults to "Universal Render Pipeline/Lit" if a Scriptable Render Pipeline is active, otherwise the built-in "Standard" shader (falling back to "Standard" if URP/Lit is unavailable).
- `confirm` Boolean (default false) — Required (true) only when overwriting an existing asset at the path. Ignored when the path is empty.
- `dry_run` Boolean (default false) — If true, validate inputs and report what would be created without writing anything.

### create_folder
Create a folder under the authoring root (creates intermediate folders).
- `path`\* String — Folder path relative to the authoring root (default Assets/); the Assets/ prefix is optional. e.g. Gameplay/Enemies or Assets/Gameplay/Enemies

### delete_asset
Delete an asset from the project. Destructive: requires confirm=true.
- `asset`\* ObjectRef — Reference to the asset to delete (path / guid / globalId).
- `confirm` Boolean (default false) — Must be true to actually delete. Without it the command refuses (destructive guard).
- `dry_run` Boolean (default false) — If true, report the asset that would be deleted without deleting it.

### find_assets
Find assets by type and/or name and/or label, returning their path, GUID and type. At least one filter is required.
- `type` String — Type name to filter by (e.g. Material, GameObject, ScriptableObject, MyGame.GameConfig). Resolved to a System.Type and matched against each asset's actual main type.
- `name` String — Name substring to filter by (AssetDatabase name filter).
- `label` String — Asset label to filter by (AssetDatabase 'l:' filter).
- `search_in` String — Folder to scope the search to, relative to the authoring root (default: the authoring root).
- `limit` Int32 (default 200) — Maximum number of results to return (default 200).

### get_authoring_root
Get the base folder (under Assets/) that bare authoring paths resolve against.
- *(no arguments)*

### get_import_settings
Read an asset's import settings, structured by importer type (texture/model/audio), including the default-platform fields and (for textures/audio) one platform override block.
- `asset`\* ObjectRef — Reference to the asset whose importer to read (path / guid / globalId).
- `platform` String (default "Default") — Platform whose override to read: Default | Standalone | iOS | Android | WebGL | tvOS. Defaults to Default.

### import_asset
Import an external file (e.g. a texture, model, audio clip) into the project by copying it to a path under the authoring root, then importing it.
- `source`\* String — Absolute filesystem path to the external file to import.
- `path`\* String — Destination asset path relative to the authoring root, including extension. The Assets/ prefix is optional.
- `confirm` Boolean (default false) — Required (true) only when overwriting an existing asset at the destination path.
- `dry_run` Boolean (default false) — If true, validate inputs and report what would be imported without writing anything.

### move_asset
Move (or rename via a new path) an asset to a new location under the authoring root. Preserves the asset's GUID.
- `asset`\* ObjectRef — Reference to the asset to move (path / guid / globalId).
- `destination`\* String — Destination asset path relative to the authoring root, including extension. The Assets/ prefix is optional.
- `dry_run` Boolean (default false) — If true, validate the move (via AssetDatabase.ValidateMoveAsset) without performing it.

### read_text_file
Read a UTF-8 text file under the authoring root and return its contents.
- `path`\* String — Text file path relative to the authoring root. The Assets/ prefix is optional.
- `max_bytes` Int32 (default 1048576) — Reject files larger than this many bytes (default 1048576 = 1 MiB) to avoid huge payloads.

### rename_asset
Rename an asset in place (keeps it in the same folder, keeps its GUID).
- `asset`\* ObjectRef — Reference to the asset to rename (path / guid / globalId).
- `new_name`\* String — New file name WITHOUT a folder path. The extension is preserved if omitted.
- `dry_run` Boolean (default false) — If true, validate the rename without performing it.

### search
Run a Unity Search query and return structured results.
- `query`\* String — Unity Search query string, e.g. 't:Material', 'p: my asset', 'h: Main Camera'.
- `limit` Int32 (default 50) — Max results to return (capped 200).

### set_authoring_root
Set the base folder (under Assets/) that bare authoring paths resolve against and are confined to. Use 'Assets' for full project access.
- `root`\* String — Project-relative folder under Assets/, e.g. Assets/AgentWork. Use 'Assets' to allow the whole project.

### set_import_settings
Set import settings on an asset's AssetImporter (default platform top-level properties, or a texture/audio per-platform override) and re-import it.
- `asset`\* ObjectRef — Reference to the asset whose importer to edit (path / guid / globalId).
- `settings`\* JObject — JSON object of importer property/field names to values, e.g. {"isReadable": true, "textureType": "NormalMap"}. For platform != Default on a texture/audio importer, keys map onto the platform-settings struct (e.g. maxTextureSize, format, compressionFormat, quality, and overridden).
- `platform` String (default "Default") — Target platform: Default | Standalone | iOS | Android | WebGL | tvOS. Defaults to Default (top-level importer properties). A real platform writes a per-platform override (textures/audio only).
- `dry_run` Boolean (default false) — If true, validate which settings would apply (and which are unknown) without writing or re-importing.

### write_text_file
Write UTF-8 text to a file under the authoring root, then import it. Overwriting an existing file requires confirm=true.
- `path`\* String — Text file path relative to the authoring root, including extension. The Assets/ prefix is optional.
- `contents`\* String — The full text content to write (replaces the file).
- `confirm` Boolean (default false) — Required (true) only when overwriting an existing file at the path.
- `dry_run` Boolean (default false) — If true, validate inputs and report what would be written without writing anything.

## Scripts & compilation

### create_script
Create a new C# script (default base class MonoBehaviour) from a template under the authoring root. NOTE: the type does not exist until a recompile completes — to attach it, call recompile, poll recompile_status, then attach_script.
- `name`\* String — Class/file name without extension, e.g. PlayerController. Must be a valid C# identifier.
- `path` String — Folder (relative to the authoring root; the Assets/ prefix is optional) to write the .cs into. Defaults to the authoring root.
- `namespace` String — Optional namespace to wrap the class in. Omit for the global namespace.
- `base_class` String (default "MonoBehaviour") — Base class to derive from. Defaults to MonoBehaviour.
- `overwrite` Boolean (default false) — Overwrite the file if it already exists. Defaults to false (an existing file is an error).

### eval
Evaluate C# code dynamically using Roslyn compiler
- `code`\* String — C# code to evaluate
- `timeout` Int32 (default 5000) — Timeout in milliseconds

### eval_file
Evaluate C# code read from a .cs file on disk
- `file`\* String — Path to a .cs file to evaluate
- `timeout` Int32 (default 5000) — Timeout in milliseconds

### recompile
Force a script recompile (works while unfocused/minimized). Poll recompile_status for completion.
- `focus` Boolean (default false) — If true, bring the Editor to the foreground before compiling. Off by default.

### recompile_status
Get the status of the last recompile: idle | triggered | compiling | completed | up_to_date. *(works while main thread is busy)*
- *(no arguments)*

### reload_file
Compile and apply in-place [HotReload] edits from a source file
- `filename`\* String — Source file containing [HotReload] methods (e.g. Assets/Scripts/Player.cs)
- `timeout` Int32 (default 30000) — Compilation timeout in milliseconds
- `assemblyDir` String — Directory to save compiled assemblies to disk (optional, default is in-memory only)
- `pdb` Boolean (default false) — Emit debug symbols (portable PDB) mapped to the original source so breakpoints bind in your editor. Compiles unoptimized.

### reload_file_override
Compile and apply hot reload file changes immediately
- `filename`\* String — Hot reload source file to compile (e.g. PlayerTweaks.cs)
- `timeout` Int32 (default 30000) — Compilation timeout in milliseconds
- `assemblyDir` String — Directory to save compiled assemblies to disk (optional, default is in-memory only)

## Tests

### cancel_tests
Cancel running test execution
- *(no arguments)*

### list_tests
List all available tests (EditMode and/or PlayMode) without running them
- `mode` String (default "all") — Test mode: all, editor, playmode (default: all)

### run_tests
Execute Unity tests with filtering options
- `mode` String (default "all") — Test mode: all, editor, playmode (default: all)
- `filter` String (default "") — Test name filter pattern (case-insensitive partial match)
- `filter_type` String (default "testName") — Filter type: testName, assembly, category (default: testName)
- `include_explicit` Boolean (default false) — Include tests marked with [Explicit] attribute
- `async_tests` Boolean (default false) — Run asynchronously - return immediately, poll /test-status for results
- `timeout` Int32 (default 300) — Test execution timeout in seconds (default: 300)

### test_status
Get status of running async test execution *(works while main thread is busy)*
- *(no arguments)*

## Build

### build
Trigger an async Player build and report the full BuildReport. Returns immediately (queued); poll build_status until status is 'completed'. DetailedBuildReport is included by default unless 'options' is supplied. Use dry_run to validate without building. *(works while main thread is busy)*
- `target` String (default "") — BuildTarget name (e.g. StandaloneWindows64). Defaults to the active target. Must be installed.
- `outputPath` String (default "") — Output path (absolute, or relative to the project root). Defaults to the last/auto path.
- `profileName` String (default "") — Build Profile name to activate before building (Unity 6 only; ignored otherwise).
- `options` String[] — BuildOptions names. Omit to get just DetailedBuildReport; supplying any disables that default.
- `scenes` String[] — Scene asset paths to build (e.g. Assets/Scenes/Main.unity). Defaults to EditorBuildSettings.
- `confirm` Boolean (default false) — Acknowledge and run the build; without it the call is refused. Use dry_run to validate only.
- `dry_run` Boolean (default false) — Validate target/outputPath/scenes without building.

### build_status
Status of the current/most recent build: idle | queued | building | completed, with the full BuildReport (files, packedAssets, buildSteps, errors, warnings) once completed. Retained until the next build. *(works while main thread is busy)*
- *(no arguments)*

### get_build_settings
Read the current build configuration from EditorUserBuildSettings / EditorBuildSettings.
- *(no arguments)*

### get_player_settings
Read PlayerSettings (company/product/version, scripting backend, API level).
- *(no arguments)*

### list_build_profiles
List Build Profile assets in the project (Unity 6 only). Returns feature_unavailable on earlier versions.
- *(no arguments)*

### list_build_targets
List the known BuildTarget values with their group and whether build support is installed.
- *(no arguments)*

### set_build_settings
Set mutable EditorUserBuildSettings fields. Does NOT manage scenes (use add_scene_to_build / remove_scene_from_build) or switch target (use switch_build_target). Use dry_run to preview.
- `settings` SetBuildSettingsInput — Fields to change; omitted fields are left unchanged.
- `confirm` Boolean (default false) — Apply the changes. Without it the call is refused.
- `dry_run` Boolean (default false) — Preview the change without applying it.

### set_player_settings
Change PlayerSettings. Requires confirm=true; use dry_run to preview. Not undoable via Ctrl+Z. Scripting backend / API level changes trigger a domain reload.
- `settings` PlayerSettingsInput — Fields to change; omitted fields are left unchanged.
- `confirm` Boolean (default false) — Apply the change. Without it the call is refused.
- `dry_run` Boolean (default false) — Preview the change without applying it.

### switch_build_target
Switch the active build target (destructive, long-running: triggers a full reimport + domain reload). Requires confirm=true. Returns immediately; poll switch_build_target_status. *(works while main thread is busy)*
- `target`\* String (default "") — BuildTarget name to switch to (must be installed; see list_build_targets).
- `confirm` Boolean (default false) — Apply the switch. Without it the call is refused.

### switch_build_target_status
Status of the last target switch: idle | switching | completed (with success + activeBuildTarget). *(works while main thread is busy)*
- *(no arguments)*

## Packages (UPM)

### package_add
Add a UPM package by name@version, git URL, or 'file:' local path. Async by default (returns in_progress; poll package_status); pass wait=true to block until added. A recompile/domain reload follows — poll recompile_status. Requires confirm=true; use dry_run to preview. *(works while main thread is busy)*
- `identifier`\* String (default "") — Package to add: 'com.unity.foo@1.2.3', a git URL, or 'file:../Path'.
- `confirm` Boolean (default false) — Apply the change. Without it the call is refused.
- `dry_run` Boolean (default false) — Preview the change without applying it.
- `wait` Boolean (default false) — Block until the operation completes and return the result (synchronous). Default: return immediately and poll package_status.

### package_list
List packages by scope: installed (default) | available (registry) | all (both). Returns the full result synchronously — available/all block until the registry query completes. *(works while main thread is busy)*
- `scope` String (default "installed") — Which packages to list: installed (default) | available | all.
- `include_indirect` Boolean (default true) — Include indirect (transitive) installed dependencies (applies to scope=installed/all).
- `offline` Boolean (default false) — For available/all: query the local cache instead of the registry.

### package_remove
Remove a UPM package by name. Async by default (returns in_progress; poll package_status); pass wait=true to block until removed. A recompile/domain reload follows — poll recompile_status. Requires confirm=true; use dry_run to preview. *(works while main thread is busy)*
- `name`\* String (default "") — Package name to remove (e.g. com.unity.foo).
- `confirm` Boolean (default false) — Apply the change. Without it the call is refused.
- `dry_run` Boolean (default false) — Preview the change without applying it.
- `wait` Boolean (default false) — Block until the operation completes and return the result (synchronous). Default: return immediately and poll package_status.

### package_resolve
Resolve/refresh packages from the manifest (re-fetch and re-link). May trigger a recompile/domain reload — poll recompile_status. Its outcome is recorded for package_status.
- *(no arguments)*

### package_search
Search packages available in the registry. Provide a name (e.g. com.unity.foo) or omit to list all. Returns the full result synchronously (blocks until the registry query completes). *(works while main thread is busy)*
- `query` String (default "") — Package name to search for. Omit/empty to list all available packages.
- `offline` Boolean (default false) — Search the local cache only.

### package_status
Status of the last async package operation (add/remove/resolve): idle | in_progress | completed | failed, with the added package, manifest, and any error. *(works while main thread is busy)*
- *(no arguments)*

## Materials & shaders

### get_material_properties
Read a material's shader, render queue, enabled keywords, and all shader properties with their current values (Color as [r,g,b,a], Vector as [x,y,z,w], Texture as an object reference).
- `material`\* ObjectRef — Reference to the .mat asset (or a loaded material) to read (path / guid / globalId / instanceId).

### get_shader_properties
Introspect a shader's declared property list (name, description, type Color|Vector|Float|Range|TexEnv|Int, range, textureDimension, flags). Provide 'shader' (by name) OR 'material' (read the shader off that material).
- `shader` String — Shader name (e.g. "Universal Render Pipeline/Lit"). Provide this OR 'material'.
- `material` ObjectRef — Reference to a material to read the shader from instead of naming it. Provide this OR 'shader'.

### list_shaders
Discover available shaders so an agent can pick a valid name for set_material_properties / create_asset. Returns [{ name, assetPath|null, isBuiltin, isSupported }].
- `filter` String — Case-insensitive substring matched against the shader name (e.g. "URP", "Lit").
- `includeBuiltin` Boolean (default true) — Include built-in/engine shaders (those with no project asset path). Default true.
- `limit` Int32 (default 200) — Maximum number of shaders to return (default 200).

### set_material_properties
Set shader properties on a material (Float/Range/Int=number; Color=[r,g,b,a] or "#RRGGBBAA" hex; Vector=[x,y,z,w]; Texture=an object reference or null to clear), optionally reassign the shader, set the render queue, and toggle keywords. Unknown names / type mismatches are reported in unknown[].
- `material`\* ObjectRef — Reference to the .mat asset (or a loaded material) to edit (path / guid / globalId / instanceId).
- `shader` String — Reassign the material's shader by name (e.g. "Standard", "Universal Render Pipeline/Lit", or a Shader Graph shader name). Applied before properties so new property names resolve against the new shader.
- `properties` JObject — JSON object of shader property name -> value. Names must include the leading underscore (e.g. _BaseColor). Float/Range/Int=number; Color=[r,g,b,a] or hex string; Vector=[x,y,z,w]; Texture=an object reference {guid/path} or null.
- `renderQueue` Nullable`1 — Explicit render queue, or -1 to inherit from the shader. Omit to leave unchanged.
- `enableKeywords` String[] — Shader keywords to enable (e.g. _NORMALMAP, _EMISSION).
- `disableKeywords` String[] — Shader keywords to disable.
- `confirm` Boolean (default false) — Reserved for parity; editing an existing material is non-destructive and undoable, so it is not required.
- `dry_run` Boolean (default false) — If true, validate the shader, resolve property names and texture refs, and report applied[]/unknown[] without writing anything.

## Animation & Timeline

### add_animator_layer
Add a layer to an AnimatorController.
- `controller`\* ObjectRef — Reference to the AnimatorController to edit (path / guid / globalId).
- `name`\* String — Layer name.
- `weight` Single (default 1) — Layer weight (default 1).
- `blendingMode` String (default "Override") — Blending mode: Override | Additive (default Override).
- `dry_run` Boolean (default false) — If true, validate inputs without writing the layer.

### add_animator_parameter
Add a parameter (Float | Int | Bool | Trigger) to an AnimatorController. A duplicate name returns code 'duplicate_parameter'.
- `controller`\* ObjectRef — Reference to the AnimatorController to edit (path / guid / globalId).
- `name`\* String — Parameter name.
- `type`\* String — Parameter type: Float | Int | Bool | Trigger.
- `defaultValue` JToken — Default value for Float/Int/Bool (ignored for Trigger).
- `dry_run` Boolean (default false) — If true, validate inputs without writing the parameter.

### add_animator_state
Add a state to a layer, optionally with a motion (AnimationClip or BlendTree) and as the layer default. A layer name with no match returns code 'layer_not_found'.
- `controller`\* ObjectRef — Reference to the AnimatorController to edit (path / guid / globalId).
- `layer` JToken — Layer index (int) or name (string). Default 0 (Base Layer).
- `name`\* String — State name.
- `motion` ObjectRef — Optional AnimationClip or BlendTree asset to assign as the state's motion.
- `isDefault` Boolean (default false) — If true, set this state as the layer's default state.
- `position` JArray — Optional [x, y] node position in the graph (cosmetic).
- `dry_run` Boolean (default false) — If true, validate inputs without writing the state.

### add_animator_transition
Add a transition between two states (or from AnyState/Entry, to Exit) on a layer, with optional conditions. Validates that the states exist and each condition's parameter exists and its mode matches the parameter type.
- `controller`\* ObjectRef — Reference to the AnimatorController to edit (path / guid / globalId).
- `layer` JToken — Layer index (int) or name (string). Default 0 (Base Layer).
- `fromState`\* String — Source state name, or the special "AnyState" / "Entry".
- `toState`\* String — Destination state name, or the special "Exit".
- `conditions` JArray — Optional conditions: [{ parameter, mode: "If"|"IfNot"|"Greater"|"Less"|"Equals"|"NotEqual", threshold? }].
- `hasExitTime` Boolean (default false) — If true, the transition uses exit time (default false).
- `exitTime` Single (default 0) — Normalized exit time (0..1) when hasExitTime is set.
- `duration` Single (default 0.25) — Transition duration in seconds (default 0.25).
- `hasFixedDuration` Boolean (default true) — If true, duration is in seconds; otherwise normalized (default true).
- `dry_run` Boolean (default false) — If true, validate everything (states, parameters, mode/type) without writing the transition.

### add_timeline_clip
Add a clip to a named track on a TimelineAsset. For Animation tracks pass an AnimationClip asset; for Audio tracks an AudioClip. Requires the com.unity.timeline package.
- `timeline`\* ObjectRef — Reference to the TimelineAsset to edit (path / guid / globalId).
- `track`\* String — Target track name.
- `start`\* Single (default 0) — Clip start time in seconds.
- `duration`\* Single (default 0) — Clip duration in seconds.
- `asset` ObjectRef — Source asset: for Animation tracks an AnimationClip; for Audio tracks an AudioClip. Required for those track types.
- `dry_run` Boolean (default false) — If true, validate inputs without writing the clip.

### add_timeline_track
Add a track (Animation | Audio | Activation | Control | Playable | Signal | Marker) to a TimelineAsset, optionally nested under a parent group/track. Requires the com.unity.timeline package.
- `timeline`\* ObjectRef — Reference to the TimelineAsset to edit (path / guid / globalId).
- `trackType`\* String — Track type: Animation | Audio | Activation | Control | Playable | Signal | Marker.
- `name` String — Optional track display name.
- `parentTrack` String — Optional name of an existing group/track to nest the new track under.
- `dry_run` Boolean (default false) — If true, validate inputs without writing the track.

### create_animation_clip
Create an empty .anim AnimationClip asset under the authoring root, with an optional frame rate and loop flag.
- `path`\* String — Asset path ending in .anim, relative to the authoring root. The Assets/ prefix is optional.
- `frameRate` Single (default 60) — Sampling frame rate of the clip (default 60).
- `loop` Boolean (default false) — If true, set the clip's loop-time flag in its AnimationClipSettings (default false).
- `confirm` Boolean (default false) — Required (true) only when overwriting an existing asset at the path.
- `dry_run` Boolean (default false) — If true, validate inputs and report what would be created without writing anything.

### create_animator_controller
Create an .controller AnimatorController asset (with a default Base Layer) under the authoring root.
- `path`\* String — Asset path ending in .controller, relative to the authoring root. The Assets/ prefix is optional.
- `confirm` Boolean (default false) — Required (true) only when overwriting an existing asset at the path.
- `dry_run` Boolean (default false) — If true, validate inputs and report what would be created without writing anything.

### create_timeline
Create a .playable TimelineAsset under the authoring root (optional frame rate). Requires the com.unity.timeline package.
- `path`\* String — Asset path ending in .playable, relative to the authoring root. The Assets/ prefix is optional.
- `frameRate` Single (default 60) — Timeline frame rate (default 60).
- `confirm` Boolean (default false) — Required (true) only when overwriting an existing asset at the path.
- `dry_run` Boolean (default false) — If true, validate inputs and report what would be created without writing anything.

### get_animation_clip
Read an AnimationClip's metadata and all float curve bindings (optionally with keyframes).
- `clip`\* ObjectRef — Reference to the AnimationClip to read (path / guid / globalId).
- `includeKeys` Boolean (default false) — If true, include each binding's keyframes (default false).

### get_animator_controller
Read an AnimatorController's full structure: parameters, layers, states (with motion / default), and transitions (with conditions).
- `controller`\* ObjectRef — Reference to the AnimatorController to read (path / guid / globalId).

### get_timeline
Read a TimelineAsset's structure: frame rate, duration, and its tracks with their clips. Requires the com.unity.timeline package.
- `timeline`\* ObjectRef — Reference to the TimelineAsset to read (path / guid / globalId).

### remove_animation_curve
Remove a float curve binding from an AnimationClip (SetEditorCurve(clip, binding, null)). Destructive: requires confirm=true.
- `clip`\* ObjectRef — Reference to the AnimationClip to edit (path / guid / globalId).
- `path` String (default "") — GameObject path relative to the animated root the binding lives on. Empty string (default) targets the root.
- `type`\* String — Component type of the binding to remove, e.g. "Transform". Resolved via the component TypeResolver.
- `property`\* String — Curve property name to remove, e.g. "m_LocalPosition.x".
- `confirm` Boolean (default false) — Must be true to actually remove the binding (destructive guard).
- `dry_run` Boolean (default false) — If true, report the binding that would be removed without removing it.

### set_animation_curve
Add or replace a single float curve binding on an AnimationClip (via AnimationUtility.SetEditorCurve). Replacing an existing binding overwrites it rather than duplicating.
- `clip`\* ObjectRef — Reference to the AnimationClip to edit (path / guid / globalId).
- `path` String (default "") — GameObject path relative to the animated root the property lives on. Empty string (default) targets the root.
- `type`\* String — Component type the property lives on, e.g. "Transform", "UnityEngine.Light". Resolved via the component TypeResolver.
- `property`\* String — Curve property name, e.g. "m_LocalPosition.x", "m_LocalScale.y", "localEulerAnglesRaw.z".
- `keys`\* JArray — Keyframes: [{ time, value, inTangent?, outTangent?, weightedMode?: "None"|"In"|"Out"|"Both" }]. Omitted tangents default to 0 (flat); this is NOT Unity's Auto tangent mode.
- `dry_run` Boolean (default false) — If true, validate type/property/keys without writing the curve.

## Lighting bake

### bake_lighting
Trigger an async lightmap bake of the open scene(s) via Lightmapping.BakeAsync(). Returns immediately; poll lighting_bake_status until completed.
- `confirm` Boolean (default false) — Recommended (true): a bake overwrites existing lightmap data. Accepted for parity; not required.
- `dry_run` Boolean (default false) — If true, validate there is an open bakeable scene and return the current lighting settings without baking.

### cancel_lighting_bake
Cancel an in-progress lighting bake (Lightmapping.Cancel()).
- *(no arguments)*

### clear_baked_lighting
Clear baked lightmap data for the open scene(s). Destructive: requires confirm=true.
- `confirm` Boolean (default false) — Must be true to actually clear (destructive, not undoable via Unity's Undo).
- `include_disk_cache` Boolean (default false) — If true, also clear the GI disk cache (Lightmapping.ClearDiskCache()).
- `dry_run` Boolean (default false) — If true, report what would be cleared without clearing.

### get_lighting_settings
Read the active LightingSettings (lightmapper, bounces, resolution, directional mode, AO, etc.).
- *(no arguments)*

### lighting_bake_status
Get the status of the last lighting bake: idle | baking | completed. *(works while main thread is busy)*
- *(no arguments)*

### set_lighting_settings
Apply a subset of lighting settings to the active LightingSettings. Returns { applied[], unknown[] }.
- `settings`\* JObject — JSON object with a subset of lighting fields to set (same names/enums as get_lighting_settings).
- `dry_run` Boolean (default false) — If true, validate the keys and report applied/unknown without changing anything.

## NavMesh bake

### bake_navmesh
Trigger an async legacy NavMesh bake of the open scene(s) via UnityEditor.AI.NavMeshBuilder. Returns immediately; poll navmesh_bake_status until completed.
- `confirm` Boolean (default false) — Accepted for parity (a bake overwrites the existing NavMesh); not required.
- `dry_run` Boolean (default false) — If true, validate there is an open scene and return current NavMesh settings without baking.

### bake_navmesh_surfaces
Bake NavMeshSurface components (AI Navigation package). v1 stub: returns package_not_found when the package is absent.
- *(no arguments)*

### cancel_navmesh_bake
Cancel an in-progress NavMesh bake (NavMeshBuilder.Cancel()).
- *(no arguments)*

### clear_navmesh
Clear the baked NavMesh for the open scene(s). Destructive: requires confirm=true.
- `confirm` Boolean (default false) — Must be true to actually clear (destructive, not undoable via Unity's Undo).
- `dry_run` Boolean (default false) — If true, report what would be cleared without clearing.

### get_navmesh_settings
Read the default agent's legacy NavMesh bake settings (agentRadius/Height/Slope/Climb, minRegionArea, voxelSize).
- *(no arguments)*

### navmesh_bake_status
Get the status of the last NavMesh bake: idle | baking | completed. *(works while main thread is busy)*
- *(no arguments)*

### set_navmesh_settings
Apply a subset of legacy NavMesh bake settings to the default agent. Returns { applied[], unknown[] }.
- `settings`\* JObject — JSON object with a subset of NavMesh fields to set (same names as get_navmesh_settings).
- `dry_run` Boolean (default false) — If true, validate the keys and report applied/unknown without changing anything.

## Occlusion bake

### bake_occlusion_culling
Trigger an async occlusion-culling bake of the open scene(s) via StaticOcclusionCulling.GenerateInBackground(). Returns immediately; poll occlusion_bake_status until completed.
- `smallest_occluder` Single (default -3.40282347e+38) — Smallest object that will occlude others (meters). Defaults to Unity's current value.
- `smallest_hole` Single (default -3.40282347e+38) — Smallest gap geometry can have that the view can see through (meters). Defaults to Unity's current value.
- `backface_threshold` Single (default -3.40282347e+38) — Backface threshold (1-100); lower trims more backfaces. Defaults to Unity's current value.
- `confirm` Boolean (default false) — Accepted for parity (a bake overwrites existing occlusion data); not required.
- `dry_run` Boolean (default false) — If true, validate there is an open scene and report the parameters that would be used without baking.

### cancel_occlusion_bake
Cancel an in-progress occlusion bake (StaticOcclusionCulling.Cancel()).
- *(no arguments)*

### clear_occlusion_culling
Clear baked occlusion-culling data for the open scene(s). Destructive: requires confirm=true.
- `confirm` Boolean (default false) — Must be true to actually clear (destructive, not undoable via Unity's Undo).
- `dry_run` Boolean (default false) — If true, report what would be cleared without clearing.

### occlusion_bake_status
Get the status of the last occlusion bake: idle | baking | completed. *(works while main thread is busy)*
- *(no arguments)*

## Project settings

### get_audio_settings
Read project Audio settings (volume, rolloff scale, doppler factor).
- *(no arguments)*

### get_graphics_settings
Read GraphicsSettings (default render pipeline).
- *(no arguments)*

### get_input_settings
Read the legacy Input Manager axes (names and count).
- *(no arguments)*

### get_performance_stats
Read render, memory, and frame-timing stats (structured, read-only).
- *(no arguments)*

### get_physics_settings
Read Physics settings (gravity, solver iterations, bounce threshold).
- *(no arguments)*

### get_quality_settings
Read QualitySettings (current level, level names, vSync, anti-aliasing).
- *(no arguments)*

### get_tags_layers
Read the project's tags and (named) layers.
- *(no arguments)*

### get_time_settings
Read Time settings (fixedDeltaTime, maximumDeltaTime, timeScale).
- *(no arguments)*

### set_audio_settings
Change project Audio settings. Requires confirm=true; use dry_run to preview. Not undoable via Ctrl+Z.
- `settings` AudioSettingsInput — Fields to change; omitted fields are left unchanged.
- `confirm` Boolean (default false) — Apply the change. Without it the call is refused.
- `dry_run` Boolean (default false) — Preview the change without applying it.

### set_graphics_settings
Set the default render pipeline asset. Requires confirm=true; use dry_run to preview. Not undoable via Ctrl+Z.
- `settings` GraphicsSettingsInput — Fields to change; omitted fields are left unchanged.
- `confirm` Boolean (default false) — Apply the change. Without it the call is refused.
- `dry_run` Boolean (default false) — Preview the change without applying it.

### set_input_settings
Tune a legacy Input Manager axis (sensitivity/gravity/dead) by name. Requires confirm=true; use dry_run to preview. Not undoable via Ctrl+Z.
- `settings` InputAxisInput — Axis change. 'axis' selects the axis by name; omitted numeric fields are left unchanged.
- `confirm` Boolean (default false) — Apply the change. Without it the call is refused.
- `dry_run` Boolean (default false) — Preview the change without applying it.

### set_physics_settings
Change Physics settings. Requires confirm=true; use dry_run to preview. Not undoable via Ctrl+Z.
- `settings` PhysicsSettingsInput — Fields to change; omitted fields are left unchanged.
- `confirm` Boolean (default false) — Apply the change. Without it the call is refused.
- `dry_run` Boolean (default false) — Preview the change without applying it.

### set_quality_settings
Change QualitySettings. Requires confirm=true; use dry_run to preview. Not undoable via Ctrl+Z.
- `settings` QualitySettingsInput — Fields to change; omitted fields are left unchanged.
- `confirm` Boolean (default false) — Apply the change. Without it the call is refused.
- `dry_run` Boolean (default false) — Preview the change without applying it.

### set_tags_layers
Add/remove tags and assign user layer names (index 8-31). Requires confirm=true; use dry_run to preview. Not undoable via Ctrl+Z.
- `settings` TagsLayersInput — Tag/layer changes to make.
- `confirm` Boolean (default false) — Apply the change. Without it the call is refused.
- `dry_run` Boolean (default false) — Preview the change without applying it.

### set_time_settings
Change Time settings. Requires confirm=true; use dry_run to preview. Not undoable via Ctrl+Z.
- `settings` TimeSettingsInput — Fields to change; omitted fields are left unchanged.
- `confirm` Boolean (default false) — Apply the change. Without it the call is refused.
- `dry_run` Boolean (default false) — Preview the change without applying it.
## RuntimeOnly commands (hidden from the listing)

Extracted from the `com.unity.pipeline` `0.4.0-exp.1` package source (`Runtime/Commands/`) — `unity command` does not list them and cannot show their schemas. They execute against the editor server despite the `RuntimeOnly` flag (exception: `capture_runtime_element`, see its entry); they also work against Player connections (`--runtime <process>` / `--runtime-path <port file>`). All require the main thread.

### simulate_pointer
Simulate a mouse/pointer event at screen coordinates (Input System). **Feeds a virtual device, not the OS cursor — UI raycasts work, but game code polling `Mouse.current` sees the virtual mouse; verify the click landed via its response, not via assumption.**
- `x`\* Single — Screen X in pixels (origin bottom-left)
- `y`\* Single — Screen Y in pixels (origin bottom-left)
- `action` String (default "click") — move | down | up | click (down+up)
- `button` String (default "left") — left | right | middle

### simulate_key
Simulate a keyboard key event (Input System). Drives the running app.
- `key`\* String — Input System Key name, e.g. Space, W, Enter, LeftArrow
- `action` String (default "press") — down | up | press (down+up)

### runtime_status
Get comprehensive runtime application status.
- *(no arguments)*

### capture_runtime_element
Capture a UI Toolkit VisualElement (by selector) from a live runtime panel (UIDocument or PanelRenderer) to a PNG; returns path + base64. **Unity 6000.7+ only** — the command is compiled out (`#if UNITY_6000_7_OR_NEWER`) on older versions and does not exist there: calling it fails with exit code 6.
- `panel` String (default "") — Target panel name: PanelSettings asset name or host GameObject name. Optional when exactly one panel exists.
- `selector`\* String — Element selector: '#name', '.class', a type name (e.g. Button), descendant (space) / child ('>') chains, optional pseudo-states (:checked, :hover, :focus, :active, :enabled, :disabled, :not(...)).
- `output` String (default "") — Output PNG path (absolute, or relative to Application.persistentDataPath). Defaults to a timestamped file under Application.persistentDataPath.

### set_timescale
Set the time scale for the application.
- `scale`\* Single — Time scale multiplier (0.0 to pause, 1.0 for normal speed)

### set_target_framerate
Set the target frame rate for the application.
- `frameRate`\* Int32 — Target frame rate (-1 for platform default, 0 for unlimited)

### quit
Gracefully quit the Unity application. Against the editor this is the play-mode/app quit path — for shutting down the editor itself, prefer `eval EditorApplication.Exit(0)` (see [lifecycle-recovery.md](lifecycle-recovery.md)).
- `exitCode` Int32 (default 0) — Exit code for the application

### log
Write a message to Unity console.
- `message`\* String — Message to log to console
- `level` String (default "info") — Log level: info, warning, error

### hotreload_status
Show current hot reload registry status and statistics.
- *(no arguments)*

### cleanup_hotreload
Remove old hot reload DLL versions and clear registry.
- `assemblyDir`\* String — Directory containing assemblies to cleanup
- `force_domain_reload` Boolean (default true) — Force Unity domain reload after cleanup
