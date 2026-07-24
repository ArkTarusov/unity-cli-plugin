# unity-cli-plugin

**A Claude Code plugin that teaches AI coding agents to drive Unity from the terminal with [UnityCLI](https://docs.unity.com/en-us/unity-cli/use-unity-cli)** — Unity's standalone `unity` command: installing Unity Editor versions and modules, opening projects, and controlling a running Unity Editor (play mode, builds, tests, scenes, assets) through the [Unity Pipeline package](https://docs.unity.com/en-us/unity-production-pipeline/local-tools-cli/unity-pipeline-package).

UnityCLI shipped after most LLM knowledge cutoffs, so out of the box an agent doesn't know the tool exists: it falls back to legacy `Unity.exe -batchmode` flags, searches Unity Hub folders for a CLI that isn't there, or concludes the tool "isn't installed" because of a stale shell PATH. These agent skills close that gap — locating the binary, agent/CI-friendly invocation, live Editor command patterns, and a strict rule that installs are proposed to the user, never run silently.

## What's inside

One plugin, three skills — the agent loads only what the task needs:

| Skill | Covers |
|---|---|
| `unity-cli-core` | Locating the `unity` binary (the #1 agent failure), JSON/TSV output for scripting, non-interactive flags, auth, full command map |
| `unity-editors` | Unity Hub replacement: installing Unity Editor versions and modules from the command line, licenses, opening and creating projects, CI rules |
| `unity-pipeline` | Unity Editor automation via the Pipeline package: build, test, play mode, scene/asset/prefab commands, async status polling — plus headless `build`/`test`/`run` |

## Install

**Claude Code** (plugin with marketplace):

```
/plugin marketplace add ArkTarusov/unity-cli-plugin
/plugin install unity-cli@unity-cli-plugin
```

**Codex, OpenCode, Cursor, and other Agent Skills harnesses** — the skills are standard `SKILL.md` folders, so use the [skills CLI](https://github.com/vercel-labs/skills):

```
npx skills add ArkTarusov/unity-cli-plugin
```

or copy `skills/*` into your harness's skills directory (`~/.codex/skills/`, `.opencode/skills/`, `~/.agents/skills/`, …). Only the Claude Code plugin wrapper (`.claude-plugin/`) is harness-specific; the skills themselves are not.

## Requirements

Two layers, installed separately:

- **UnityCLI binary** (per machine) — enough for editor installs, modules, licenses, opening projects. [Install guide](https://docs.unity.com/en-us/unity-cli/use-unity-cli).
- **Unity Pipeline package** (per project, `unity pipeline install`) — additionally required for live Editor control; needs Unity 6.0+ and sign-in (`unity auth login`).

The skills instruct the agent to *propose* either install to the user and wait for approval — never to install the CLI or modify the project's package manifest on its own.

## Notes

- UnityCLI is **experimental**; commands and flags change between betas. The skills teach discovery patterns (`unity --help`, `unity command`) over hardcoded surface, and the command reference is a labeled snapshot.
- The skills are plain `SKILL.md` files in the [Agent Skills](https://agentskills.io) format, so they can be reused with other agent harnesses that support it.
- Verified against UnityCLI `1.0.0-beta.3` and Unity Pipeline package `0.4.0-exp.1` on Unity `6000.4`.

## License

MIT
