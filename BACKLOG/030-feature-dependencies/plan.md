---
feature: 030
slug: feature-dependencies
status: validated
created: 2026-09-24
validated: 2026-09-24
---

# Plan 030 — Feature dependencies: structured blockedBy/blocks (не текстом в body)

<goal>
Decompose 030-feature-dependencies into small single-concern tasks with exact file paths and verifiable acceptance criteria, ordered by dependency: data → service → API → (MCP enforcement, UI, materialization) → result.
</goal>

<context>
Spec 7badf229 validated 2026-09-24. Architecture facts: MCP tools are thin clients over Host HTTP API (Overmind.Mcp/OvermindPlanningTools.cs → Overmind.ApiApiClient → Host/Api); domain logic lives in Infrastructure/Planning; entities in Core/Entities; web UI is Next.js under web/src; tests live in tests/Overmind.{Core,Api,Infrastructure,Mcp}.Tests. Frontmatter keys are registry-driven (FrontmatterKeyRegistry.cs) — materialization round-trip goes through that registry.
</context>

<scope>
Six tasks, created as separate task artifacts after plan validation:

1. Domain + persistence — PlanningFeature gets stored blockedBy references (slug-only, same project; blocks derived). EF mapping + migration. Files: src/Overmind.Core/Entities/PlanningFeature.cs, src/Overmind.Infrastructure/Persistence (mapping), src/Overmind.Infrastructure/Migrations (new migration). AC: migration applies cleanly on an existing DB; dotnet build green.

2. Dependency service + cycle detection — add/remove/list operations; cycle rejection (direct and transitive, error carries the chain); closed-when-blocker-is-done-or-review rule lives here as a query. Files: src/Overmind.Infrastructure/Planning/PlanningFeatureService.cs (+ FeatureDependencyService if the class grows past a seam). AC: Infrastructure.Tests cover direct cycle, transitive cycle, chain in error, both-directions list, block lift on done/review, re-block on draft/ready/inProgress.

3. Host API surface — endpoints for add/remove/list dependencies; feature list/detail DTOs carry blockedBy/blocked-state for cards and the board filter. Files: src/Overmind.Host/Api (planning endpoints). AC: Api.Tests cover the three endpoints + blocked flag in DTO; build green.

4. MCP tools + enforcement — add_feature_dependency / remove_feature_dependency / list_feature_dependencies wired in OvermindPlanningTools.cs; planning_current_task returns a stop state listing unclosed blockers (feature stays readable); planning_set_current refuses a blocked feature with diagnostics. Enforcement logic on the host side (current-task resolver), MCP renders. Files: src/Overmind.Mcp/OvermindPlanningTools.cs, src/Overmind.Infrastructure/Planning (current-task resolver + set-current guard), src/Overmind.Host/Api (if enforcement needs an endpoint). AC: Mcp.Tests cover stop state shape + set_current refusal; manual check via MCP round-trip documented in task log.

5. Web UI — Dependencies panel in the feature workspace (add/remove, chips "waits for N"); dashboard card shows blocked chips only; board filter "blocked". Files: web/src (features/planning/*). AC: npm run lint && npx tsc --noEmit green; npm run build green; panel and filter verified manually.

6. Materialization round-trip — dependency links in feature.md frontmatter via FrontmatterKeyRegistry; PlanningMaterializer writes, BacklogImporter reads back. Files: src/Overmind.Infrastructure/Planning/FrontmatterKeyRegistry.cs, PlanningMaterializer.cs, BacklogFeatureImport.cs. AC: Infrastructure.Tests prove export→import preserves links verbatim.

Task 0 (parallel, small): result artifact at the end — orchestrator duty, not a dispatch task.
</scope>

<anti-scope>
Cross-project dependencies; enforcement on set_feature_status; editing deps from the dashboard card; queue ordering (072); archived interplay (OM-17) — per spec anti-scope.
</anti-scope>

<constraints>
Task refs are explicit: 2 blockedBy 1; 3 blockedBy 2; 4 blockedBy 3; 5 blockedBy 3; 6 blockedBy 1. Tasks 4, 5, 6 may run in parallel once their refs are done (disjoint file sets). Provenance markers on all mutations; no comments in code; English-only messages; dotnet build after each backend task, lint+tsc after the web task.
</constraints>

<acceptance-criteria>
- [ ] All six task artifacts exist with XML sections, briefs, review level, and refs encoding the dependency order above
- [ ] Every task AC names a verification command or observable behavior (build/test/lint/manual-documented)
- [ ] No task spans more than one architectural layer
</acceptance-criteria>

<refs>
spec 7badf229 (validated 2026-09-24); 044-batch-planning-ops (its planning_upsert_tasks will be used to create these tasks if shipped in time — dogfood); 028 (anti-scope), 072 (future consumer)
</refs>

<obstacles></obstacles>

<log></log>