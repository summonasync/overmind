---
feature: 101
task: 01
slug: approval-project-prominence
status: done
review: light
created: 2026-10-03
brief: ProjectNameBadge/ProjectHeader + resolver hook in shared, fallback pinned, tests
---

# Task 01 — Shared primitive: project name resolver + badge/header

<goal>
The one rendering primitive both surface families consume: projectId → prominent project name, with graceful fallback.
</goal>

<scope>
- FSD shared layer (shared/ui or shared/lib per house layout): ProjectNameBadge (compact, for bell rows/cards) + ProjectHeader (large, for content-page tops) + a resolver hook consuming the projects data the app already loads (find the existing projects query/store — reuse, no new fetches unless truly absent; if a fetch is unavoidable, cache it).
- Fallback: unknown/deleted projectId → show the raw id (truncated) or a generic label — never crash, never empty silently (test both).
- Styling: prominent title-position weight/size for the Header variant, badge tone for the Badge variant — consistent with existing chip/badge system.
- Unit tests: known id renders the name, unknown id renders fallback, both variants.
</scope>

<anti-scope>
No surface integration (tasks 02/03), no backend.
</anti-scope>

<constraints>
FSD, RSC-first, no new packages, English UI, no comments by default, no auto-commits.
</constraints>

<acceptance-criteria>
- [x] lint + tsc + build + test green
- [x] Known projectId renders the project name (both variants)
- [x] Unknown projectId renders the fallback (pinned)
- [x] Resolver reuses existing projects data (no duplicate fetch)
</acceptance-criteria>

<refs>
- Spec: 0f88a771-d25a-45cc-ae2c-0b99a2cce8e6 (Shared primitive section).
</refs>

<obstacles>
- Placement deviation from task body: components + resolver hook live in features/projects (components/project-name-badge, components/project-header, lib/use-project-name), NOT shared/ui — shared/ui importing the projects query cache would be a hard FSD direction violation (shared must not depend on features), and duplicating the query in shared breaks the no-duplicate-fetch AC. Spec's wording ("FSD shared or widgets layer") accommodates this; task body's "shared/ui or shared/lib" read as layout guidance. Pure fallback logic IS in shared: shared/lib/project-display.ts (zero deps, node-testable). Consumers (tasks 02/03) import from @/features/projects public API — the same pattern the app already uses for useProjectList.
- Render-level component tests are not pinnable with the house node --test culture (no DOM/RTL runner; components need QueryClientProvider). Pinned the shared resolver (both fallbacks, trim, never-empty) with 6 node tests; on-surface render assertions for badge/header land with tasks 02/03 per the spec's Testing section (bell row / card / toast / page each assert hierarchy).
</obstacles>

<log>
- 2026-10-03 [agent] — dispatched to web-coder (same session as 028/04 — carries the house web patterns), plan validated; while this wave codes, the orchestrator specs 091 with the user in parallel
- 2026-10-03 [agent] — implemented: shared/lib/project-display.ts (pure resolver + PROJECT_NAME_FALLBACK), features/projects: project-name-badge (Badge tone=info, title carries full id), project-header (title-position h1 + optional secondary line), use-project-name hook (reuses projectListQueryOptions cache); exports added to features/projects index; 6 node tests pin known/unknown/blank/blank-name/never-empty/trim. Gates: lint 0 errors (6 pre-existing warnings), tsc 0, build success (route table unchanged incl. /issues), test 58/58 — my own trim test caught the resolver returning an untrimmed stored name, fixed in the resolver. Placement deviation recorded in obstacles (feature-layer components, shared-layer pure fn — FSD direction); render-level surface assertions land with tasks 02/03 per spec Testing section.
- 2026-10-03 [agent] — task 01 complete — 4/4 AC ticked, gates green (lint 0e/6 pre-existing w, tsc 0, build ok, tests 58/58), placement deviation + test-culture caveat in obstacles; ready for review, tasks 02/03 unblocked to consume the primitive
- 2026-10-03 [agent] — nit-fix round (review-101-01 PASS-WITH-NITS): "use client" added to project-name-badge + project-header; badge text-transform pinned to normal-case via cn (verified in built CSS — .normal-case emitted after .capitalize, override holds, «iOS client» renders verbatim); padded-id trim pinned with 2 more tests (padded known → name, padded unknown → truncated id). Gates: lint 0 errors / 6 pre-existing warnings, tsc 0, tests 60/60, build success. Status stays review.
- 2026-10-03 [agent] — 2026-10-04 [agent] — micro fix round (3 nits from review-101-01): "use client" on both components, normal-case override empirically verified against the capitalize base (house cn has no tailwind-merge — specificity/order checked on real CSS), padded-id cases pinned (+2 tests). Gates: lint 0/6, tsc 0, 60/60, build re-run clean. Orchestrator closes done — PASS-WITH-NITS verdict stood.
- 2026-10-03 [agent] — PASS-WITH-NITS (audits/review-101-01.md) + micro fix round applied and verified; AC 4/4. Closing as done — tasks 02/03 unblock for parallel dispatch.
</log>