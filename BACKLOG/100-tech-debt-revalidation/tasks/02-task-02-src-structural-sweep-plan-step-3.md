---
feature: 100
task: 02
slug: tech-debt-revalidation
status: review
review: light
created: 2026-10-06
depends_on: 01
brief: src sweep: dependency direction, Mcp/Infrastructure/Host seams, Host DI shape, EF usage, new outlier classes; architect-review on opus, read-only, only allowed write = audits/architect-review-2026-10-06-100.md
---

# Task 02 — src structural sweep (plan step 3)

<goal>
Structural sweep of the backend: dependency direction, service seams, DI shape, EF usage.
</goal>

<scope>
- Project dependency graph and direction across src/ projects; seam quality between Mcp / Infrastructure / Host.
- Host DI registration shape (god-service signals, lifetime mistakes as structural smell).
- Infrastructure EF usage patterns (context ownership, migration assembly placement).
- New god-file / multi-responsibility outliers beyond the three named ones.
</scope>

<anti-scope>
No source edits, no board mutations — findings feed task 04 classification only.
</anti-scope>

<constraints>
Executor: architect-review on opus. Read-only except one file: audits/architect-review-2026-10-06-100.md. Runs CONCURRENTLY with the sprint's coding lanes — safe because read-only.
</constraints>

<acceptance-criteria>
- [ ] Dependency-direction verdict per src/ project boundary — clean or violation with file:line.
- [ ] DI shape and EF usage addressed with findings or an explicit clean verdict.
- [ ] Any new size/responsibility outliers listed with line counts.
</acceptance-criteria>

<refs>
- spec 2086fff7-a874-46b0-8169-a3411449a677, plan f40abff9-612c-4d0c-9500-a91ac2efed9c (this feature) — procedure step 3.
</refs>

<obstacles>
None at breakdown time.
</obstacles>

<log>
- 2026-10-06 [agent] — task created by feature-orchestrator (task breakdown after consolidated validation sitting).
- 2026-10-06 [agent] — audit task 02 → review: §7 src sweep appended (dependency direction clean; AgentPlanningApi.Current state-machine in HTTP handler = structural candidate; 4-class PlanningTools EXTRACT validated with 3 refinements; KitImporter/KitScanner KEEP; EF clean; pointer list of 10 items for task 04 in §7.6). 3/3 AC covered. Report audits/architect-review-2026-10-06-100.md:214-305.
</log>