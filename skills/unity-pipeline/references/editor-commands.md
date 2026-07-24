# Editor command snapshot

Snapshot from Unity Pipeline package `0.4.0-exp.1` on Unity `6000.4`. The live list is dynamic (package version, project code, optional packages) — `unity command` with no arguments is always the source of truth; this file only helps pick a likely name before verifying.

Conventions: destructive commands need `--confirm true`; mutating commands usually accept `--dry_run true`; async commands pair with a `*_status` poll command.

## Editor & play mode

| Command | Purpose |
|---|---|
| `editor_play` / `editor_pause` / `editor_stop` | Enter / pause / exit play mode |
| `editor_status` | Editor state |
| `editor_focus` | Bring the Editor window to the foreground |
| `set_autotick` | Keep the editor ticking while unfocused |
| `set_timescale`, `set_target_framerate` | Runtime time controls |
| `save_all`, `quit` | Save everything / quit the Editor |
| `menu` | Execute a menu item |
| `get_selection`, `set_selection` | Editor selection |
| `capture_scene_view`, `capture_game_view`, `screenshot` | PNG captures (base64 inline or `--save_path`) |

## Console & logs

`get_console_logs` (`--severity`, `--limit`), `clear_console`, `log`, `console`.

## Scenes

`create_scene`, `open_scene`, `save_scene`, `set_active_scene`, `list_open_scenes`, `get_scene_hierarchy`, `add_scene_to_build`, `remove_scene_from_build`.

## GameObjects & components

| Command | Purpose |
|---|---|
| `create_gameobject`, `create_gameobjects` | Single / batch creation (primitives, positions) |
| `find_gameobjects`, `delete_gameobject`, `rename_gameobject` | Lookup and lifecycle |
| `set_parent`, `set_active`, `set_transform`, `set_tag`, `set_layer` | Common properties |
| `add_component`, `remove_component`, `attach_script` | Components (attach_script: by type or script path; needs compiled type) |
| `get_component_properties`, `set_component_properties` | Serialized properties as JSON |
| `get_serialized_fields`, `set_serialized_field` | Field-level access |

## Prefabs

`create_prefab`, `create_prefab_variant`, `instantiate_prefab`, `unpack_prefab`, `apply_prefab_overrides`, `revert_prefab_overrides`, `save_prefab_contents` (isolated prefab-stage edit, nested-safe).

## Assets & files

| Command | Purpose |
|---|---|
| `create_asset` | New ScriptableObject/asset by type |
| `import_asset`, `delete_asset`, `rename_asset`, `move_asset`, `copy_asset` | Asset lifecycle (rename keeps GUID) |
| `find_assets`, `search` | Asset lookup / Unity Search query |
| `create_folder`, `read_text_file`, `write_text_file` | File ops under the authoring root |
| `get_import_settings`, `set_import_settings` | Importer settings (texture/model/audio, platform overrides) |
| `get_authoring_root`, `set_authoring_root` | Base folder confining bare paths |

## Scripts & compilation

`create_script`, `recompile` → `recompile_status`, `eval`, `eval_file`, `reload_file`, `reload_file_override`, `hotreload_status`, `cleanup_hotreload`.

## Tests

`run_tests` → `test_status`, `list_tests`, `cancel_tests`.

## Build

| Command | Purpose |
|---|---|
| `build` → `build_status` | Async Player build; `build_status` retains the full BuildReport |
| `list_build_targets`, `switch_build_target` → `switch_build_target_status` | Target management (switch = full reimport + domain reload) |
| `list_build_profiles`, `get_build_settings`, `set_build_settings` | Build configuration |
| `get_player_settings`, `set_player_settings` | PlayerSettings (backend/API-level changes trigger domain reload) |

## Packages (UPM)

`package_add`, `package_remove` (async → `package_status`), `package_list`, `package_search`, `package_resolve`.

## Materials & shaders

`get_material_properties`, `set_material_properties`, `list_shaders`, `get_shader_properties`.

## Animation & Timeline

`create_animation_clip`, `get_animation_clip`, `set_animation_curve`, `remove_animation_curve`, `create_animator_controller`, `get_animator_controller`, `add_animator_parameter` / `add_animator_state` / `add_animator_transition` / `add_animator_layer`; `create_timeline`, `get_timeline`, `add_timeline_track`, `add_timeline_clip` (Timeline package required).

## Baking (all async)

- Lighting: `bake_lighting` → `lighting_bake_status`, `cancel_lighting_bake`, `clear_baked_lighting`, `get_lighting_settings`, `set_lighting_settings`
- NavMesh: `bake_navmesh` → `navmesh_bake_status`, `cancel_navmesh_bake`, `clear_navmesh`, `bake_navmesh_surfaces`, `get_navmesh_settings`, `set_navmesh_settings`
- Occlusion: `bake_occlusion_culling` → `occlusion_bake_status`, `cancel_occlusion_bake`, `clear_occlusion_culling`

## Project settings

`get_quality_settings` / `set_quality_settings`, `get_time_settings` / `set_time_settings`, `get_physics_settings` / `set_physics_settings`, `get_audio_settings` / `set_audio_settings`, `get_graphics_settings` / `set_graphics_settings`, `get_input_settings` / `set_input_settings`, `get_tags_layers` / `set_tags_layers`, `get_performance_stats`.

## Play-mode runtime

`runtime_status`, `simulate_pointer`, `simulate_key`.
