---
feature: 100
slug: tech-debt-revalidation
status: ready
priority: p0
version: 1
created: 2026-09-30
---

# Feature 100 — Tech debt: full structural re-validation of the codebase (god-files, seams, FSD)

<goal>
Run a full architect-review (hard mode) over the whole solution and turn its findings into concrete follow-up work. Trigger: the user flagged god-file growth — OvermindPlanningTools.cs is a single 1199-line class holding all 30+ MCP tools; PlanningMaterializer.cs (812) and BacklogImporter.cs (703) are next in line. The repo rule "Avoid God-Services" needs an enforcement pass, not just a line in CLAUDE.md.
</goal>

<requirements>
1. architect-review hard over the entire solution (src/ + web/src): layer boundaries, service seams, FSD boundaries on the frontend, duplication, file/class size outliers, naming drift.
2. Report lands in audits/ per the artifact provenance rule (architect-review-YYYY-MM-DD-100.md).
3. Every finding becomes actionable: blockers → fix tasks on this feature; cosmetic/large refactors → follow-up features or the microbatch queue, each with the file map the review already produced.
4. Explicit verdict per god-file candidate: split into partials / extract services / keep-as-is with rationale — the 090 plan consciously kept OvermindPlanningTools.cs single-class for MCP registration; the review either justifies that decision or replaces it.
</requirements>

<non-goals>
- Rewriting for rewriting's sake — no behavior changes ride on this feature.
- MCP contract changes (tool names, payloads) — consumers exist in the wild.
- Running before 099-agent-claims lands: the tree must be at rest, not mid-wave.
</non-goals>

<refs>
- 099-agent-claims — in-flight wave; this review runs after it lands.
- audits/architect-review-2026-09-30-090.md — last structural pass (light, feature-scoped) as the baseline.
</refs>

<log>
- 2026-10-06 [agent] — Van by claude claimed the feature
- 2026-10-06 [auto] — artifact created → ready
</log>
