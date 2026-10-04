---
feature: 114
slug: claims-onboarding-docs
status: inProgress
priority: p0
version: 5
created: 2026-10-04
---

# Feature 114 — Claims onboarding: teach the claim workflow in MCP quickstart + crew skills

<goal>
Agents (and their humans) need practical onboarding for the 099 claim workflow — both in the MCP surface (onboarding quickstart / server instructions) and in the crew skills (overmind-ceremony): when to claim, what the lock means for sub-agents, how unclaim / stale take-over / EOF auto-release behave in practice. The 099 docs wave covered skill/CLAUDE.md/server text, but field feedback (2026-10-04, user): «надо в mcp ребятам рассказать как юзать новые claims» + «прописать в скиллы, как работать с новыми пинами» — the PRACTICE is not landing.
</goal>

<context>
User requests 2026-10-04 (two mid-session messages). Claims replaced pins in 099: planning_claim_feature / planning_unclaim_feature, X-Overmind-Session lock on 12 feature mutations, adapter-session liveness (EOF auto-release), stale take-over. Agents in the field still ask how to use them — the workflow rules (claim on take, release on handoff) need to be spelled out where agents actually read.
</context>

<log>
- 2026-10-04 [auto] — artifact created → inProgress
- 2026-10-04 [agent] — Claude by anthropic claimed the feature
</log>
