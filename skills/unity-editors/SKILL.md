---
name: unity-editors
description: Use when installing or uninstalling Unity Editor versions or modules from the terminal, listing installed editors or available Unity releases, activating Unity licenses, opening projects or creating project skeletons from the command line (`unity projects new/create` mechanics — not a guided design-a-new-game flow), or replacing Unity Hub on CI machines and headless workers.
---

# Unity editors, modules, projects (Hub replacement)

`unity` subcommands for everything Unity Hub does. Prerequisite basics (PATH, `--json`, `--non-interactive`, auth): see skill **unity-cli-core**.

Consent rule: `install`, `install-modules`, `uninstall`, and `--allow-install` change the user's machine (multi-gigabyte downloads, UAC prompts on Windows). Run them when installing is what the user asked for; when an install is merely a means to unblock your own task, propose the exact command and wait for approval.

## Quick reference

```bash
unity editors -i                       # installed editors
unity editors -r                       # available releases (also: unity releases --lts)
unity editors running                  # running GUI Editor instances + open project (version, PID); does not list -batchmode editors
unity install 6000.3.7f1 -m ios android --accept-eula -y
unity install lts                      # version aliases: lts, latest streams
unity install-modules -e 6000.3.7f1 -m webgl        # add modules later
unity install-modules -e 6000.3.7f1 -l              # list module IDs
unity uninstall 6000.3.7f1
unity install-path                     # get/set where editors install
unity editor add "C:\Path\To\Editor"   # register an editor installed outside the Hub
unity open ./MyProject                 # opens with the project's editor version
unity open . --build-target Android
unity projects list
unity projects require . --json        # assert project's editor version is installed; installs if missing
unity projects new MyGame --json       # CI-friendly project creation (create = interactive)
unity templates list
unity license status
unity license activate
```

## Non-interactive rules (CI, agents)

Omitting arguments starts interactive pickers. Always pass:
- an explicit version (or alias like `lts`) — never bare `unity install`
- `-y` / `--yes` to auto-select the first match
- `--accept-eula` for module license agreements
- `--non-interactive` globally

`--dry-run` on `install` / `install-modules` prints what would download without installing.

## Gotchas

| Situation | Handling |
|---|---|
| Windows install into protected path | Triggers a UAC elevation helper; `--no-elevate` makes it fail instead of prompting (better in CI) |
| Editor version missing for a project | `unity projects require <path>` or the `--allow-install` flag on `build`/`test`/`run`/`open` installs it on demand |
| Version from `ProjectVersion.txt` wrong for the task | `--editor-version <ver>` overrides; `-e/--editor-path` pins an exact binary |
| Module IDs unknown | `unity modules list <version>` or `unity install-modules -e <version> -l` |
| Interrupted download | `unity install --resume`; cache location via `unity cache info` |
| `unity open` / `unity editors` fail with `EBUSY` | Lock contention on the shared `%APPDATA%\UnityHub` store — see the EBUSY row in skill **unity-cli-core** for the recovery procedure. Interim workaround: launch the editor binary directly (`& "<install-path>\Editor\Unity.exe" -projectPath <project>`) |
