---
feature: 116
slug: crewkit-in-repo
status: review
priority: p0
version: 1
created: 2026-10-04
---

# Feature 116 — Crew kit in-repo: pack from repo, sync repo → home

<goal>
Crew kit (src/OvermindCrewKit, embedded clone of dotNET-Coding-Agents-Crew) becomes the source of truth Overmind releases stage from: pack.sh bundles the kit from the repo working tree, not from the deployed home; the sync flow is repo → home (install.sh), never home → bundle.
</goal>

<context>
User moved the crew kit in-repo 2026-10-04 («теперь я перенёс сюда crewKit... он должен быть базовым китом overmind'а — любые изменения в нашем MCP отлетают в него, и после релиза он раскатывается»). Today pack.sh has DEFAULT_CREW_KIT=/home/johan-boolman/.claude — the bundle stages whatever happens to be deployed in home, which can drift from the repo.
</context>

<log>
- 2026-10-04 [auto] — artifact created → inProgress
- 2026-10-04 [agent] — Van by claude claimed the feature
- 2026-10-04 [auto] — all tasks done + spec + plan validated → review
- 2026-10-04 [agent] — agent by unknown released the claim
</log>
