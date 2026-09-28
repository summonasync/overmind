---
feature: 004
slug: missing-language-coders
status: inProgress
priority: p1
version: 1
created: 2026-09-27
depends_on: 005-global-deploy-rename-validation
---

# Feature 004 — Missing language coders: js-ts frontend/backend split, python, go, solution-architect

## Goal

Expand the crew beyond the .NET/JS-web pair: split the js-ts domain by frontend/backend, add Python and Go implementers with matching reviewers and best-practices rule libraries, and introduce a solution-architect owning solution-level architecture and technology selection. One feature, phased tasks inside: Phase A (js-ts split) → Phase B (python trio) → Phase C (go trio) → Phase D (solution-architect). Every addition lands ×4 runtimes per SYNC-STRUCTURE.md.

## Decided (user-approved 2026-09-27/28)

- Structure: ONE feature, phased tasks A→D, single spec/plan gate (user choice).
- python-coder + python-code-reviewer + python-best-practices (rule library WITHOUT an agent — mirrors dotnet-best-practices).
- go-coder + go-code-reviewer + go-best-practices (same shape).
- Reviewers are always separate agents (Coder → Reviewer independence, fixed in 003 — do not regress).
- solution-architect: owns initial solution architecture, technology selection, large additions/rewrites of services; stack architects (dotnet / js-ts-*) remain consulted-by Phase-1 specialists; owns the solution-level ADR ritual (TECHNICAL-SOLUTIONS.md).
- Phasing rationale: the crew roughly doubles (~23→~33 agents, ~26→~36 skills ×4 runtimes); phases keep each review gate small and each phase independently shippable.

## OPEN QUESTION — Socratic session pending (do NOT draft the Phase A spec before it resolves)

The js-ts domain split. User's working hypothesis (2026-09-28): ONE js-ts-coder cannot span the domain — frontend (many frameworks, many architectures) and backend (the Node world is even bigger) are different masters. Working candidate: `js-ts-frontend-coder` + `js-ts-backend-coder`. The Socratic session must resolve: naming, whether frontend/backend split applies to architect/reviewer pairs too, where the current Next.js-specific rules land (FSD/RSC/BFF/React Query — stay as a section? move? stay with the frontend coder?), and how Phase A tasks decompose under the chosen split.

<removed 2026-09-28 — node.js-master superseded: user and reviewer agreed Node is a runtime, not a language; the real need became the js-ts frontend/backend split above. Do not resurrect node.js-master as a separate agent.>

## Non-goals

- No MAUI/dotnet changes beyond solution-architect routing implications.
- No runtime enforcement changes (hooks/settings).
- No best-practice catalogs at full dotnet-best-practices depth on day one — each language library starts with its top enforceable rules and grows.

## Refs

- Built on 003-crew-role-boundaries (role model: feature-orchestrator / single-task coders / separate reviewers / stack architects as consultants). Phase A renames files 003 is currently touching → hard dependency, materialized on the board.
- Audit origin: conversation 2026-09-27 (003 collision audit) + user proposal 2026-09-28.

<log>
- 2026-09-27 [auto] — artifact created → inProgress
</log>
