# unity-cli-plugin

Skills that teach AI coding agents to use [UnityCLI](https://docs.unity.com/en-us/unity-cli/use-unity-cli), Unity's standalone `unity` command. With the Pipeline package in a project, an agent can drive the running Editor — scenes, tests, builds — straight from the shell. Packaged as a Claude Code plugin, but the skills are plain `SKILL.md` files in the [Agent Skills](https://agentskills.io) format and work in other agent harnesses (Codex, Cursor, OpenCode, ...) too.

## Why

UnityCLI shipped after most models' knowledge cutoffs. An agent asked to automate Unity will typically reach for `Unity.exe -batchmode`, go looking for a CLI inside Unity Hub folders, or decide the tool "is not installed" because its shell session has a stale PATH. These skills fix that: they tell the agent where the binary lives, how to call it non-interactively with structured output, and what the CLI can actually do.

## Live Editor control

The interesting part is live Editor control. With the [Unity Pipeline package](https://docs.unity.com/en-us/unity-production-pipeline/local-tools-cli/unity-pipeline-package) in a project, `unity command` talks to the running Editor over a local HTTP server. On Unity 6000.4 that adds about 140 commands — scenes, GameObjects, prefabs, materials, animation, script creation, packages, tests, builds — plus any custom `[CliCommand]` methods the project registers. Any agent with a shell gets structured results and exit codes.

## CLI or MCP?

Unity exposes that same Editor API through two interfaces: CLI (`unity command`) and MCP (`unity mcp` starts the server, `unity mcp configure <client>` writes the client config). This plugin is built for the CLI interface — no MCP server in between. It can help set up the MCP server, but contains no skills for working with the Editor through MCP. If you drive the Editor through MCP, the editor-management and CLI-basics skills below are still useful; only the live-Editor skill is CLI-specific.

## Relation to Unity's official skills

Unity maintains its own [skills repo](https://github.com/Unity-Technologies/skills): a [unity-cli skill](https://github.com/Unity-Technologies/skills/tree/main/skills/unity-cli) plus skills beyond the CLI — guided new-project setup, UPM package management guidance, Unity Services backends, in-app purchases, ads mediation. This plugin overlaps with theirs only on the CLI and deliberately stays out of the rest (it covers the CLI's package add/remove commands, not package-selection workflows); its skills are scoped so the two skill sets can be installed side by side.

On the shared CLI ground the split is: their unity-cli is a command reference first — which commands exist and their flags, kept current with each beta. This plugin is an operations manual:

- the conventions a session runs on — `confirm`/`dry_run`, status polling for async commands, the authoring root for generated files, recompile before attaching a new script;
- verified failure modes — silently dropped arguments, `EBUSY` lock contention, batch-mode editors invisible to `unity status`;
- recovery — when the CLI seems missing (stale PATH), the Pipeline package is absent, or the Editor hangs on a modal dialog or crashes.

## Skills

| Skill | Covers |
|---|---|
| `unity-cli-core` | Finding the binary, non-interactive flags, JSON/TSV/NDJSON output, auth, command map |
| `unity-editors` | Editor and module installs, licenses, opening and creating projects (what Unity Hub does) |
| `unity-pipeline` | Driving the running Editor: play mode, builds, tests, scene and asset commands, status polling, resident headless editors, hang and crash recovery — plus headless one-shot `unity build`/`test`/`run` |

The skills never install anything on their own. When the CLI or the Pipeline package is missing, the agent is instructed to propose the install command and wait for the user.

## Install

Claude Code:

```
/plugin marketplace add ArkTarusov/unity-cli-plugin
/plugin install unity-cli@unity-cli-plugin
```

Codex, OpenCode, Cursor and other harnesses, via the [skills CLI](https://github.com/vercel-labs/skills):

```sh
npx skills add ArkTarusov/unity-cli-plugin
```

or copy `skills/*` into the harness's skills directory (`~/.codex/skills/`, `.opencode/skills/`, `~/.agents/skills/`, ...).

## What it looks like

> run the play mode tests and tell me what failed

With the skills, the agent follows the session conventions instead of guessing:

```sh
unity status                                              # a running Editor with the Pipeline package?
unity command run_tests --mode playmode --async_tests true
unity command test_status                                 # poll until finished, then read failures
```

Without them, it typically reaches for `Unity.exe -batchmode` — a second Editor instance that fails or fights the one already open.

## Requirements

- UnityCLI on the machine ([install guide](https://docs.unity.com/en-us/unity-cli/use-unity-cli)). This alone is enough for editor installs and project management.
- For live Editor control: Unity 6 (6000.0)+, `unity auth login`, and the Pipeline package in the project (`unity pipeline install`).

## Compatibility

Verified against UnityCLI 1.0.0-beta.3 and Unity Pipeline package 0.4.0-exp.1 on Unity 6000.4. Both are in beta and their command sets drift between releases, so the skills instruct the agent to check the live command set (`unity --help`, `unity command`) against what is documented.

## License

MIT — see [LICENSE](LICENSE).
