# unity-cli-plugin

Skills that teach AI coding agents to use [UnityCLI](https://docs.unity.com/en-us/unity-cli/use-unity-cli), Unity's standalone `unity` command. Packaged as a Claude Code plugin, but the skills are plain `SKILL.md` files in the [Agent Skills](https://agentskills.io) format and work in other harnesses too.

## Why

UnityCLI shipped after most models' knowledge cutoffs. An agent asked to automate Unity will typically reach for `Unity.exe -batchmode`, go looking for a CLI inside Unity Hub folders, or decide the tool "is not installed" because its shell session has a stale PATH. These skills fix that: they tell the agent where the binary lives, how to call it non-interactively with JSON output, and what the CLI can actually do.

The interesting part is live Editor control. With the [Unity Pipeline package](https://docs.unity.com/en-us/unity-production-pipeline/local-tools-cli/unity-pipeline-package) in a project, `unity command` talks to the running Editor over a local HTTP server. On Unity 6000.4 that surface is about 140 commands: scenes, GameObjects, prefabs, materials, animation, script creation, packages, tests, builds. It is the same command set that MCP-based Unity integrations expose, without the MCP server in between — any agent with a shell gets structured results and exit codes.

Unity exposes that same Editor API through two interfaces: CLI (`unity command`) and MCP (`unity mcp` starts the server, `unity mcp configure <client>` writes the client config). This plugin is built for the CLI interface. It can help set up the MCP server, but contains no skills for working with the Editor through MCP. If you work through the CLI, install everything; if you prefer MCP, skip `unity-pipeline` — `unity-cli-core` and `unity-editors` are still useful.

Unity maintains its own [unity-cli skill](https://github.com/Unity-Technologies/skills/tree/main/skills/unity-cli). The difference: theirs is reference material — which commands exist and their flags, kept current with new betas. This plugin is skills for the work itself: the conventions a session runs on (`confirm`/`dry_run`, status polling for async commands, the authoring root, recompile before attaching a new script) and recovery when things break — the agent that can't find an installed CLI (stale PATH), the wrong command name, the missing Pipeline package.

## Skills

| Skill | Covers |
|---|---|
| `unity-cli-core` | Finding the binary, non-interactive flags, JSON output, auth, command map |
| `unity-editors` | Editor and module installs, licenses, opening and creating projects (what Unity Hub does) |
| `unity-pipeline` | Driving the running Editor: play mode, builds, tests, scene and asset commands, status polling |

The skills never install anything on their own. When the CLI or the Pipeline package is missing, the agent is instructed to propose the install command and wait for the user.

## Install

Claude Code:

```
/plugin marketplace add ArkTarusov/unity-cli-plugin
/plugin install unity-cli@unity-cli-plugin
```

Codex, OpenCode, Cursor and other harnesses, via the [skills CLI](https://github.com/vercel-labs/skills):

```
npx skills add ArkTarusov/unity-cli-plugin
```

or copy `skills/*` into the harness's skills directory (`~/.codex/skills/`, `.opencode/skills/`, `~/.agents/skills/`, ...).

## Requirements

- UnityCLI on the machine ([install guide](https://docs.unity.com/en-us/unity-cli/use-unity-cli)). This alone covers editor installs and project management.
- For live Editor control: Unity 6.0+, `unity auth login`, and the Pipeline package in the project (`unity pipeline install`).

## Notes

UnityCLI and the Pipeline package are in beta and their command sets drift between releases, so the skills have the agent check the live state (`unity --help`, `unity command`) against what they document. Verified against UnityCLI 1.0.0-beta.3 and Unity Pipeline package 0.4.0-exp.1 on Unity 6000.4.

## License

MIT
