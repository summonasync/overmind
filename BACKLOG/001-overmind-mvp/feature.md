---
feature: 001
slug: overmind-mvp
status: done
version: 2
created: 2026-09-06
---

# Feature 001 — Overmind MVP

# Feature 001 — Overmind MVP

<goal>
Overmind is a win/mac desktop app (.NET) that gives AI coding agents (Claude Code, Kimi Code, Codex, Gemini CLI, Antigravity) a shared, persistent context store — without OpenViking-level setup pain. One local app holds projects, notes/decisions, gates, guards, and skills as a single source of truth; agents connect over a local HTTP API via a thin stdio MCP adapter, and skill-kits are materialized into projects so agents pick them up natively.
</goal>

<requirements>
- A user can create multiple projects and keep their context isolated per project.
- A user can store, edit, tag, and full-text-search context entries: notes/decisions, gates, guards, skills.
- An agent can read project context through a standard MCP connection (stdio) with zero manual file copying.
- A user can manage skill-kits (CRUD, enable/switch per project) and have the active kit materialized into the project's native skill directories (.claude/.kimi-code/etc.).
- The app runs as a tray application with a browser-based UI; setup is "download and run" — no mandatory external services.
- Vector (semantic) search works when a local Qdrant (Docker) is configured; without it the app degrades to FTS and says so honestly.
- Embedding provider is selectable in the UI (local ONNX / Ollama / OpenAI-compatible endpoint).
</requirements>

<non-goals>
- External-folder skill materialization and the agent launcher (P1, deferred).
- Runtime delivery of skills/gates/guards over MCP tools/resources (P2, deferred).
- Cloud sync, multi-user/team features, auth beyond localhost.
- Linux support.
- Vector backends other than Qdrant.
</non-goals>

<!--
LIFECYCLE: создаётся при постановке фичи в бэклог ДО старта работы.
Не замораживается гейтом, но изменения после status: ready проходят через пользователя (bump version).
Молчаливые правки запрещены.
Вход для spec gate: на его основе строится spec.md через superpowers:brainstorming.
-->
