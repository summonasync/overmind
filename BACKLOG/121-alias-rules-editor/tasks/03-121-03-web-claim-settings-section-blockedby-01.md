---
feature: 121
task: 03
slug: alias-rules-editor
status: done
review: light
created: 2026-10-06
depends_on: 01
brief: Web: claim settings section on project detail — textarea max 2000, types/api/queries/hook wiring, mount after workflow settings, node:test component test
---

# 121/03 — Web: claim settings section (blockedBy 01)

<goal>
The user edits and saves the naming prompt in project settings: a textarea (maxLength 2000) on the project detail, read+mutation wiring per FSD, the persisted value survives reload.
</goal>

<context>
Depends on task 01's endpoint contract (blockedBy order 1). Mount point and patterns verified: projects-page.tsx renders the settings sections at :102-110; workflow-settings.tsx is the form pattern. Spec dc030eba-1f10-4bf2-a68f-f9fea8534b97, plan ee02bacf-1651-4e60-9b16-a5f672647fa8 (slice 03).
</context>

<scope>
- web/src/features/projects/types.ts: ClaimSettingsResponse { claimStaleThresholdMinutes: number; agentNamingRules: string | null }.
- web/src/features/projects/api.ts: getClaimSettings(projectId), updateClaimSettings(projectId, body).
- web/src/features/projects/queries.ts: projectKeys.claimSettings(projectId) + claimSettingsQueryOptions (mirror planningKeys.workflow :18/:45).
- web/src/features/projects/lib/use-claim-settings.ts (new): read hook + useClaimSettingsMutation (invalidate on success) — mirror the workflow hooks in use-planning.ts.
- web/src/features/projects/components/claim-settings-section.tsx (new): textarea (maxLength 2000), synced-state pattern from workflow-settings.tsx, save Button, ErrorText; hint that agents read this text in the onboard agentIdentity block.
- web/src/pages/projects-page/ui/projects-page.tsx: mount <ClaimSettingsSection projectId={detail.id} /> after PlanningWorkflowSettings (:106).
- web/tests/claim-settings-section.test.mts (new, mirror feature-number-settings.test.mts): renders the saved value; save posts the entered text.
</scope>

<anti-scope>
- No threshold input in the UI (API-only; the form sends the GET value back untouched).
- No global settings page changes; no FSD re-arrangement; no direct fetch from client components (features/projects api layer only).
</anti-scope>

<constraints>
- React Query v5; no useEffect data fetching; 'use client' only where hooks demand; RSC-first.
- Export the section through the features/projects barrel if the page imports from it (match the existing import style at projects-page.tsx:12-16).
</constraints>

<acceptance-criteria>
- [x] Section renders the persisted rules text; save posts it; invalidation refetches; reload shows the saved value.
- [x] npm run lint && npx tsc --noEmit green; npm test green; npm run build green (in web/).
</acceptance-criteria>

<refs>
- Spec: dc030eba-1f10-4bf2-a68f-f9fea8534b97; Plan: ee02bacf-1651-4e60-9b16-a5f672647fa8 (slice 03).
- blockedBy: task 01 (order 1) — endpoint contract.
- web/src/features/planning/components/workflow-settings.tsx; web/src/features/planning/queries.ts:18/:45; web/src/pages/projects-page/ui/projects-page.tsx:102-110; web/tests/feature-number-settings.test.mts.
</refs>

<obstacles>
- Test runner is node --test "tests/**/*.test.mts" (npm test) — plain node:test, no vitest; match the .test.mts style.
</obstacles>

<log>
- 2026-10-06 [agent] — task created from validated plan slice 03 (batch breakdown; review: light; blockedBy order 1).
- 2026-10-06 [agent] — 121/03 coding round started
- 2026-10-06 [agent] — coding round complete — light review gate. Commit 1c2aeed: types/api/queries/claimSettingsQueryOptions, use-claim-settings read+mutation hooks, claim-settings-section (textarea maxLength 2000, synced-state, threshold echoed untouched), barrel exports, mounted after Planning workflow on projects-page; 6 pin tests in web/tests/claim-settings-section.test.mts. Gates green: lint 0 errors (6 pre-existing warnings in unrelated files), tsc clean, 263/263 node:test, next build OK.
- 2026-10-06 [agent] — light review APPROVED — 0 blockers, 1 microbatch candidate (mount pin asserts presence, not position after Planning workflow — position verified by eye; ledger #266). FSD/BFF clean, endpoint contract matches 392568f 1:1, synced-state mirrors workflow-settings, spot-checks 263/263 + lint + tsc. Commit 1c2aeed.
</log>