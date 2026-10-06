---
feature: 100
task: 01
slug: tech-debt-revalidation
status: review
review: light
created: 2026-10-06
brief: Orientation (repo_map, index freshness, csproj graph) + deep dive of the 3 named god-files with per-file split/extract/keep verdicts; architect-review on opus, read-only, only allowed write = audits/architect-review-2026-10-06-100.md
---

# Task 01 — Orientation + god-file deep dive (plan steps 1-2)

<goal>
Orient the audit workspace and produce draft per-file verdicts for the three named god-files.
</goal>

<scope>
- repo_map over the workspace, index_status freshness check (a stale index is noted for the methodology section), csproj dependency graph across Overmind.Core / Overmind.Host / Overmind.Infrastructure / Overmind.Mcp.
- Deep dive per named file: src/Overmind.Mcp/OvermindPlanningTools.cs (1354), src/Overmind.Infrastructure/Planning/PlanningMaterializer.cs (915), src/Overmind.Infrastructure/Planning/BacklogImporter.cs (703) — responsibility inventory by method groups, coupling via find_references, internal duplication, extraction candidates.
- Draft verdict per file: split-partials / extract-services / keep-as-is, including re-justification or explicit written replacement of the 090 single-class rationale for OvermindPlanningTools.cs.
</scope>

<anti-scope>
No source edits, no board mutations, no MCP contract proposals — verdicts are draft input for task 04.
</anti-scope>

<constraints>
Executor: architect-review on opus. Read-only except one file: audits/architect-review-2026-10-06-100.md (may be drafted progressively). Runs CONCURRENTLY with the sprint's coding lanes — safe because read-only.
</constraints>

<acceptance-criteria>
- [ ] Index freshness noted; csproj dependency graph captured.
- [ ] Each of the three god-files has a draft verdict with method-group inventory and extraction candidates.
- [ ] The 090 single-class decision is re-justified or explicitly replaced in writing.
</acceptance-criteria>

<refs>
- spec 2086fff7-a874-46b0-8169-a3411449a677, plan f40abff9-612c-4d0c-9500-a91ac2efed9c (this feature) — procedure steps 1-2.
- audits/architect-review-2026-09-30-090.md — baseline under re-examination.
</refs>

<obstacles>
None at breakdown time.
</obstacles>

<log>
- 2026-10-06 [agent] — task created by feature-orchestrator (task breakdown after consolidated validation sitting).
- 2026-10-06 [agent] — audit task 01 complete — report §2 orientation + §3.1 csproj graph + §4.1-4.3 god-file inventories/verdicts (PlanningTools EXTRACT 4-class seam; Materializer EXTRACT-partial + hash-dedup S-finding ×4; BacklogImporter KEEP) + §4.4 decision-090 OVERTURN with superseding-ADR wording. All 3 AC covered; granular ticks to be applied before the final sitting. Report: audits/architect-review-2026-10-06-100.md (210 lines).
- 2026-10-06 [agent] — audit task 01 → review: orientation + god-file deep dive landed in audits/architect-review-2026-10-06-100.md; 3/3 AC covered (ticks pending before final sitting). Executor had no MCP door — statuses moved by main session per contract.
</log>