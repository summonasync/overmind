# Overmind

One mind for your swarm of AI coding agents.

Overmind is a Windows/macOS desktop app that gives AI coding agents — Claude Code, Kimi Code, Codex, Gemini CLI, Antigravity — a shared, persistent context store. Project notes, decisions, gates, guards, and skill-kits live in one local database instead of being re-explained to every agent in every session.

Inspired by [OpenViking](https://github.com/volcengine/OpenViking)'s context-as-a-filesystem idea, but built to be download-and-run: no mandatory external services, no embedding keys required to get started.

## Status

Pre-alpha. The repository currently contains the spec and backlog only — see `BACKLOG/001-overmind-mvp/`. There is no buildable application yet.

## How it works

- **Single .NET host process** — tray application hosting a localhost HTTP API and serving the web UI. No native windows; the browser is the UI.
- **stdio MCP adapter** — a small console app that agents spawn through their standard MCP config. It proxies to the HTTP API and exposes context as MCP resources under a `ctx://` URI scheme (e.g. `ctx://projects/<id>/gates`).
- **SQLite + FTS5** — all data and full-text search, always on, zero dependencies.
- **Qdrant** — the only vector backend, running in local Docker. Optional: without it, search degrades to full-text and the UI says so. Embedding provider is selectable in settings: bundled local ONNX, Ollama, or an OpenAI-compatible endpoint.
- **Skill-kits** — named, versioned sets of skills/gates/guards linked to projects. Activating a kit materializes it into the project's native skill directories (`.claude/skills/`, `.kimi-code/skills/`, …); switching kits re-materializes. The database is the source of truth; files on disk are a managed cache.

## Roadmap

| Priority | Scope |
|----------|-------|
| v1 (MVP) | Multi-project context store, notes/decisions/gates/guards/skills, FTS + optional semantic search, tray app + web UI, stdio MCP adapter, in-project skill-kit materialization |
| P1 | External-folder kit materialization + agent launcher (`claude --add-dir`, `kimi --skills-dir`) |
| P2 | Runtime skill delivery over MCP (`get_skill` tool, `ctx://skills/` resources, bootstrap skill) |
| P2/P3 | Agent initialization flows, project onboarding |

## Repository layout

```
BACKLOG.md                  feature index
BACKLOG/001-overmind-mvp/   MVP feature folder
  feature.md                what and why
  spec.md                   brainstormed design decisions and requirements
```

## License

TBD
