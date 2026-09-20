---
feature: 007
slug: embedded-agent
status: draft
priority: p2
version: 1
created: 2026-09-14
---

# Feature 007 — Embedded agent ("the banger")

<goal>
Overmind ships with its own built-in LLM agent that manages the app itself: the user authenticates it per-provider (platform token / login flow), the agent gets an MCP tool surface over Overmind's own API plus a bootstrap context (existing projects, kits, entries, planning state) — and the user drives the whole app through it, like through a coding agent. "Overmind manages Overmind."
</goal>

<requirements>
Idea-level stub (recorded 2026-09-14, to be fleshed out in a brainstorm before spec):

- Embedded agent runtime inside the app (host-side or sidecar), configurable per LLM provider.
- Provider auth: token / login flow per platform, stored securely (OS keychain — same story as future git credentials).
- The agent receives an MCP tool surface over Overmind itself: manage projects, entries, kits, planning — the app's own API exposed as agent tools.
- Bootstrap context loading: what projects exist, which kits, current planning state — the agent starts knowing the app.
- Chat/command UI in the web app to talk to the agent.
</requirements>

<non-goals>
- To be defined at brainstorm. Open design space: which runtime (own agent loop vs embedding an existing agent SDK), tool scope vs safety (writes gated?), relation to the existing stdio MCP adapter (reuse vs separate surface), cost/quota handling.
</non-goals>

<!--
LIFECYCLE: создаётся при постановке фичи в бэклог ДО старта работы.
Не замораживается гейтом, но изменения после status: ready проходят через пользователя (bump version).
Молчаливые правки запрещены.
Вход для spec gate: на его основе строится spec.md через superpowers:brainstorming.
-->
