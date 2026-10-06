---
feature: 100
slug: tech-debt-revalidation
status: validated
created: 2026-10-06
validated: 2026-10-06
---

# Spec 100 — Tech debt: full structural re-validation of the codebase (god-files, seams, FSD)

<goal>
Produce a whole-solution structural audit (architect-review, hard mode) and convert its findings into concrete, pre-sized follow-up work. This feature delivers the REPORT and the follow-up map only; all code changes live in follow-up features or microbatch ledger items, never here.

Trigger, re-measured 2026-10-06 (grew since feature creation): src/Overmind.Mcp/OvermindPlanningTools.cs is a single class of 1354 lines holding 30+ MCP tools; src/Overmind.Infrastructure/Planning/PlanningMaterializer.cs is at 915; src/Overmind.Infrastructure/Planning/BacklogImporter.cs at 703. The repo rule "Avoid God-Services" (CLAUDE.md) has no enforcement pass behind it — this feature is that pass.
</goal>

<scope>
Audit targets — read-only structural review of:
1. Named god-files (deep dive, explicit verdict per file — split into partials / extract services / keep-as-is with rationale):
   - src/Overmind.Mcp/OvermindPlanningTools.cs (1354) — the 090 decision to keep it single-class for MCP registration is re-justified or explicitly replaced.
   - src/Overmind.Infrastructure/Planning/PlanningMaterializer.cs (915).
   - src/Overmind.Infrastructure/Planning/BacklogImporter.cs (703).
2. Full structural sweep, src/ (projects: Overmind.Core, Overmind.Host, Overmind.Infrastructure, Overmind.Mcp): project dependency graph and direction, service seams, DI shape in Host, EF usage in Infrastructure. OvermindCrewKit/ is the kit distribution folder (no csproj) — out of the C# sweep.
3. Frontend: FSD boundaries in web/src (layers present: app, pages, widgets, features, shared; no entities/ layer — the audit judges violation vs conscious omission); cross-layer import violations.
4. God-service enforcement pass: file/class size outliers and multi-responsibility classes across the solution — "Avoid God-Services" becomes a measured verdict, not a doc line.
5. Duplication and naming drift hotspots.
6. Scope context, present in the report with decisions NOT reopened: landed wave commits 86e8eed, 2fa454c, 04104b9 (feature 125).
</scope>

<anti-scope>
- No refactors and no code changes of any kind inside this feature — every finding becomes a follow-up feature/task or a microbatch ledger entry.
- No MCP contract changes (tool names, payloads) — consumers exist in the wild.
- No re-litigation of closed decisions from prior structural passes without written rationale (baseline: audits/architect-review-2026-09-30-090.md).
- No board mutations and no ledger writes by the audit itself — the deliverable is the report plus a handoff list for the human.
</anti-scope>

<constraints>
- Executor: architect-review sub-agent, HARD mode, read-only toolset (squeezr + Read/Grep/Glob + git log); the only writable path is audits/.
- Report file: audits/architect-review-<run-date>-100.md per the artifact provenance rule (skill-prefix naming, audits/ only) and the established repo pattern.
- Every finding carries severity blocker | warning | suggestion with file:line evidence.
- Every blocker (P0) finding carries a proposed follow-up: a follow-up feature sketch (title + one-paragraph goal + file map) or a numbered microbatch ledger item.
- Tree at rest: the run happens after the 099-agent-claims wave landed (satisfied — wave commits at HEAD as of 2026-10-06).
- Comparison baseline: audits/architect-review-2026-09-30-090.md (light, feature-scoped).
</constraints>

<acceptance-criteria>
- [ ] audits/architect-review-<run-date>-100.md exists with required sections: executive summary, methodology, classified findings, god-file verdicts, follow-up map, coverage checklist.
- [ ] Each of the three named god-files has an explicit verdict (split / extract / keep-as-is) with rationale; the 090 single-class decision for OvermindPlanningTools.cs is re-justified or explicitly replaced.
- [ ] Every named sweep area (src dependency direction, DI shape, EF usage, web FSD, outlier scan) is addressed with findings or an explicit clean verdict.
- [ ] Every P0 finding has a proposed follow-up: feature sketch or microbatch ledger item, pre-sized with the review's file map.
- [ ] Microbatch candidates are listed as numbered entries ready for ledger append.
- [ ] Wave commits 86e8eed, 2fa454c, 04104b9 appear as scope context, and no landed decision from them is reopened.
- [ ] git status after the audit run: only audits/ changed — zero source modifications.
</acceptance-criteria>

<refs>
- 099-agent-claims — in-flight wave this audit had to wait for; landed (wave commits at HEAD).
- audits/architect-review-2026-09-30-090.md — baseline light structural pass; its OvermindPlanningTools single-class rationale is under re-examination.
- CLAUDE.md — "Avoid God-Services" rule and Sub-Agent Rules (model choice for the audit dispatch).
- Feature body trigger numbers (1199/812/703 at creation) — superseded by the 2026-10-06 measurements (1354/915/703) recorded in this spec.
</refs>

<obstacles>
None at draft time. The audit runs in phase C, after the consolidated human validation sitting; if the tree is mid-wave again by then, the run waits for it to land (same at-rest condition as the original non-goal).
</obstacles>

<log>
- 2026-10-06 [agent] — spec draft written by feature-orchestrator (batch mode: spec + plan drafts before one consolidated human validation sitting; audit execution follows validation in phase C).
- 2026-10-06 [agent] — validated with human proof: notification d7458044-6fbf-4908-974e-ecaccfa54a79 replied 2026-10-06 13:05:13 UTC
</log>