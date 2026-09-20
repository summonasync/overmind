---
feature: 034
slug: feature-priorities
status: validated
created: 2026-09-20
validated: 2026-09-20
---

# Spec 034 — Feature priorities: прио как данные (поле + сортировка/фильтр борды + MCP + round-trip)

<decisions>
- FeaturePriority enum: P0-Critical, P1-High, P2-Normal, P3-Low. Integer-persisted by EF convention (additive column, single migration defaulting ALL existing features to P2-Normal); serialized lowercase ("p0".."p3") matching the status conventions.
- No "no priority" state: P2 is the default for every create path that omits it; the board sort is always total — (priority asc, updatedAt desc). User decision 2026-09-20 (enum over int/drag-order; P2-for-all over nullable).
- Both doors on CREATE: human door feature create/update accepts priority; agent door planning_create_feature gains an optional priority param. UPDATES through the agent door are a deliberate non-goal for v1 — there is no agent feature-update tool yet (same gap class as 023/08 pre-fix); the human door PUT + web editor cover changes. Revisit when/if an agent update-feature tool lands.
- Validation: unknown priority value → typed 400 (mirror the status-validation patterns).
- Web: priority chip on the board card + workspace header (tone: p0 danger, p1 warning, p2 neutral, p3 muted — status-tone precedent); default board sort becomes (priority, updatedAt desc); priority filter joins the filter bar; feature editor gains a priority select.
- Materializer/importer: frontmatter `priority: p1` round-trip; unknown value on import → default P2 + a report line (mirror the status-import downgrade-with-report behavior).
- current-pin stays orthogonal: pin = «working now», priority = queue order; pinning does not touch priority and vice versa.
</decisions>

<requirements>
Backend:
- PlanningFeature entity: Priority property (private set, factory default P2); EF migration adds the column with P2 for all rows; no other entity changes.
- Feature create/update (human door): accept + validate priority; response carries it. Agent planning_create_feature: optional priority param, same validation.
- Board list API: returns features sorted (priority asc, updatedAt desc); the response shape gains priority.
- Tests: enum round-trip, default-on-create (both doors), typed 400 on unknown, migration default (existing rows → P2), board sort order, materializer frontmatter round-trip, importer downgrade-with-report.
Web:
- types + status-tone-style priority tones; chip on board card + workspace header; default sort (priority, updatedAt); priority filter in the filter bar; editor select; static export + strict TS.
Verification:
- dotnet build/test green; web lint/tsc/build green; live dogfood: set the real queue priorities on the board (033=P1, 032=P1, 026=P2 …) and verify sort/chips in the UI.
</requirements>

<anti-scope>
- No drag-and-drop ordering (enum chosen deliberately); no agent-door priority UPDATES (non-goal above); no cross-feature automation driven by priority (it is display/sort/filter data only); no changes to current-pin semantics.
</anti-scope>

<acceptance-ideas>
- Create feature with priority p1 via MCP → board shows the chip, sorts it above P2s.
- PUT priority p0 via human door → card jumps to top (above same-priority? above lower priorities), chip turns danger.
- Import a feature.md with `priority: p9` → P2 + report line; materialize round-trips `priority: p0` byte-stable.
- Filter by P1 → only P1 cards; hide-done filter unaffected.
</acceptance-ideas>
