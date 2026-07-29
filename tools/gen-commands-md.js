// Generates the categorized part of skills/unity-pipeline/references/editor-commands.md
// from a `unity --json command` dump. Usage:
//   unity --json command --project-path <project> > cmds.json
//   node tools/gen-commands-md.js cmds.json out.md
// The "RuntimeOnly commands" section is not generated — those commands are absent
// from the dump; their schemas come from the com.unity.pipeline package source
// (Runtime/Commands/*.cs) and are appended manually. Re-attach that section after
// regenerating, and update the version line in the preamble.
const fs = require('fs');

const dump = JSON.parse(fs.readFileSync(process.argv[2], 'utf8'));
const cmds = dump.data.commands.slice().sort((a, b) => a.name.localeCompare(b.name));

const CATEGORIES = [
  ['Editor & play mode', ['editor_play','editor_pause','editor_stop','editor_status','editor_focus','set_autotick','save_all','menu','get_selection','set_selection']],
  ['Capture', ['capture_scene_view','capture_game_view','screenshot']],
  ['Console & logs', ['get_console_logs','clear_console','console','log']],
  ['Scenes', ['create_scene','open_scene','save_scene','set_active_scene','list_open_scenes','get_scene_hierarchy','add_scene_to_build','remove_scene_from_build']],
  ['GameObjects & components', ['create_gameobject','create_gameobjects','find_gameobjects','delete_gameobject','rename_gameobject','set_parent','set_active','set_transform','set_tag','set_layer','add_component','remove_component','attach_script','get_component_properties','set_component_properties','get_serialized_fields','set_serialized_field']],
  ['Prefabs', ['create_prefab','create_prefab_variant','instantiate_prefab','unpack_prefab','apply_prefab_overrides','revert_prefab_overrides','save_prefab_contents']],
  ['Assets & files', ['create_asset','import_asset','delete_asset','rename_asset','move_asset','copy_asset','find_assets','search','create_folder','read_text_file','write_text_file','get_import_settings','set_import_settings','get_authoring_root','set_authoring_root']],
  ['Scripts & compilation', ['create_script','recompile','recompile_status','eval','eval_file','reload_file','reload_file_override','hotreload_status','cleanup_hotreload']],
  ['Tests', ['run_tests','test_status','list_tests','cancel_tests']],
  ['Build', ['build','build_status','list_build_targets','switch_build_target','switch_build_target_status','list_build_profiles','get_build_settings','set_build_settings','get_player_settings','set_player_settings']],
  ['Packages (UPM)', ['package_add','package_remove','package_list','package_search','package_resolve','package_status']],
  ['Materials & shaders', ['get_material_properties','set_material_properties','list_shaders','get_shader_properties']],
  ['Animation & Timeline', ['create_animation_clip','get_animation_clip','set_animation_curve','remove_animation_curve','create_animator_controller','get_animator_controller','add_animator_parameter','add_animator_state','add_animator_transition','add_animator_layer','create_timeline','get_timeline','add_timeline_track','add_timeline_clip']],
  ['Lighting bake', ['bake_lighting','lighting_bake_status','cancel_lighting_bake','clear_baked_lighting','get_lighting_settings','set_lighting_settings']],
  ['NavMesh bake', ['bake_navmesh','navmesh_bake_status','cancel_navmesh_bake','clear_navmesh','bake_navmesh_surfaces','get_navmesh_settings','set_navmesh_settings']],
  ['Occlusion bake', ['bake_occlusion_culling','occlusion_bake_status','cancel_occlusion_bake','clear_occlusion_culling']],
  ['Project settings', ['get_quality_settings','set_quality_settings','get_time_settings','set_time_settings','get_physics_settings','set_physics_settings','get_audio_settings','set_audio_settings','get_graphics_settings','set_graphics_settings','get_input_settings','set_input_settings','get_tags_layers','set_tags_layers','get_performance_stats']],
];

const catOf = new Map();
for (const [cat, names] of CATEGORIES) for (const n of names) catOf.set(n, cat);

const byCat = new Map(CATEGORIES.map(([c]) => [c, []]));
const uncategorized = [];
for (const c of cmds) {
  const cat = catOf.get(c.name);
  if (cat) byCat.get(cat).push(c); else uncategorized.push(c);
}
if (uncategorized.length) byCat.set('Other', uncategorized);

function fmtDefault(v) {
  if (v === null || v === undefined) return '';
  const s = JSON.stringify(v);
  return ` (default ${s})`;
}
function normDesc(s) {
  if (!s) return '';
  return s.replace(/\s+/g, ' ').trim();
}

let out = [];
out.push('# Editor command reference');
out.push('');
out.push('Generated from `unity --json command` against Unity Pipeline package `0.4.0-exp.1` on Unity `6000.4`. The live list is dynamic (package version, project code, optional packages, project-defined `[CliCommand]` methods) — on a name/argument mismatch, re-check against the live output of `unity --json command`; this file covers the common core.');
out.push('');
out.push('**The listing is not exhaustive**: some registered built-in commands (the play-mode input group: `simulate_pointer`, `simulate_key`, `runtime_status`, `set_timescale`, `set_target_framerate`, `quit`) do not appear in the 140-command listing but execute fine when called. Absence from the listing is not proof a command does not exist — a genuinely unknown name fails with exit code 6.');
out.push('');
out.push('Argument conventions: pass every argument as `--name value` (snake_case). `*` marks a required argument. Destructive commands need `--confirm true`; most mutating commands accept `--dry_run true`; async commands pair with a `*_status` poll command.');
out.push('');

for (const [cat, list] of byCat) {
  if (!list.length) continue;
  out.push(`## ${cat}`);
  out.push('');
  for (const c of list) {
    const flags = [];
    if (c.mainThreadRequired === false) flags.push('works while main thread is busy');
    const desc = normDesc(c.description);
    out.push(`### ${c.name}`);
    out.push(desc + (flags.length ? ` *(${flags.join(', ')})*` : ''));
    if (c.parameters && c.parameters.length) {
      for (const p of c.parameters) {
        const req = p.required ? '\\*' : '';
        const d = normDesc(p.description);
        out.push(`- \`${p.name}\`${req} ${p.type}${fmtDefault(p.defaultValue)}${d ? ' — ' + d : ''}`);
      }
    } else {
      out.push('- *(no arguments)*');
    }
    out.push('');
  }
}

fs.writeFileSync(process.argv[3], out.join('\n'));
console.log('written', process.argv[3], out.length, 'lines');
