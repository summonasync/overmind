---
name: augur-method
description: |
  Codified Native.AI estimation method (AM-IDs) consumed by augur-estimator during estimation and
  by augur-challenger as the review checklist. Single source of the inventory schema, bucket
  taxonomy, default AI coefficients, overheads, roll-up formulas, scenarios, BA/QA delta rules,
  xlsx layout and the calibration log. Hours and FTE only — money is out of scope.
type: prompt
whenToUse: |
  During any augur estimate (estimator loads it with the frozen inventory) and any estimate
  challenge (challenger cites violations by AM-ID). Also when recalibrating defaults after a
  delivered project.
---

# Augur Method — Native.AI Estimation Rules

Each rule has a stable AM-ID. The challenger cites violations as `AM-NN (severity)`.
Severity: **[B]** BLOCKER, **[W]** WARNING, **[S]** SUGGESTION.
Status of defaults: **uncalibrated** until the Calibration section has at least one entry.

## Inventory schema
Columns, in this order, in the research report and in the estimate md `## Per-feature estimate`
table (which appends the computed columns of AM-83 after them). The xlsx `Features` sheet (AM-83)
is narrower: it drops `description` and `assumptions`:
`id | name | module | description | source | size | bucket | k_override | confidence | assumptions | phase`

- **AM-01 [B]** `id` is `F-NNN`, unique, never reused after a re-run (gaps are fine).
- **AM-02 [B]** `source` cites file + page/section (`rfp.md §2.2`, `brief.pdf p.4`). A row with no
  source must carry an explicit assumption text instead; a row with neither is invalid.
- **AM-03 [B]** Nothing outside the inventory is estimated. The estimator never adds rows; missing
  work goes back to the researcher through the orchestrator.
- **AM-04 [B]** NFRs, integrations, data migration, DevOps/CI, security/compliance, observability
  and test-automation setup are their own rows, never folded into feature rows.
- **AM-05 [W]** `size` ∈ {S, M, L, XL}; `confidence` ∈ {high, medium, low}. `low` confidence rows
  are listed in the estimate's Assumptions & Risks block.
- **AM-06 [W]** `bucket` is exactly one of the bucket keys below. A feature spanning buckets is
  split into rows with new ids (`F-012a` / `F-012b` suffixes are NOT allowed).
- **AM-07 [B]** `phase` is filled by the architect after GATE 2 as `P<n>` (`P1`, `P2`, …) and is
  never empty at estimate time.
- **AM-08 [S]** `description` is one line; detail lives in the research report body.
- **AM-09 [W]** Inputs that could not be read (scans, images, locked files, extraction errors) are
  listed under "Unprocessed inputs" in the research report; the estimate repeats that list verbatim.

## Buckets and default AI coefficients
AI-native hours = classic hours × k. Defaults below are starting points.

| Bucket key | Bucket | k | Kind |
|---|---|---|---|
| `crud` | CRUD / scaffolding / boilerplate | 0.35 | dev |
| `ui-std` | Standard UI screens | 0.45 | dev |
| `ui-custom` | Custom / complex UX | 0.70 | dev |
| `integration` | Third-party integrations | 0.65 | dev |
| `domain` | Complex domain logic / algorithms | 0.80 | dev |
| `migration` | Data migration / legacy adaptation | 0.75 | dev |
| `infra` | Infrastructure / DevOps / CI | 0.60 | dev |
| `security` | Security / compliance | 0.90 | dev |
| `ba-workshop` | BA: workshops, elicitation with the client | 0.80 | role |
| `ba-docs` | BA: documentation, user stories, acceptance criteria | 0.40 | role |
| `qa-design` | QA: test design | 0.45 | role |
| `qa-auto` | QA: test automation | 0.40 | role |
| `qa-manual` | QA: manual / exploratory | 0.80 | role |
| `qa-regression` | QA: regression | 0.30 | role |
| `pm` | PM / coordination | 0.90 | role |

- **AM-10 [B]** Only the keys above are valid bucket values.
- **AM-11 [W]** Dev buckets are assigned per inventory row. Role buckets are derived per phase
  from the phase's classic dev hours (AM-33); an inventory row MAY also carry a role bucket when
  the work is a discrete deliverable (test-automation framework setup, UAT support, a workshop
  series) — such explicit rows are added on top of the derived hours (AM-34).
- **AM-12 [W]** DevOps work is `infra` rows and counts as dev hours; there is no separate DevOps
  role in the roll-up (a deliberate simplification of the design spec — note it when a client
  asks for a DevOps FTE line).
- **AM-20 [B]** Every k used in an estimate is printed in the Parameters block (AM-80); an estimate
  with hidden coefficients is invalid.
- **AM-21 [B]** Bucket k overrides (Parameters) and per-row `k_override` values are allowed only
  inside `0.10 ≤ k ≤ 1.00`. Out-of-range values are rejected with the key or row id named —
  never clamped silently.
- **AM-22 [B]** Size-table cells (AM-30) must be > 0; a zero or negative override is rejected.
- **AM-23 [W]** Lowering a k below its default (bucket override or `k_override`) needs a one-line
  justification in `overrides_justification` (Parameters) or in the row's `assumptions`.
- **AM-24 [S]** `k_override: 1.00` is the honest value for a row AI does not accelerate (hardware,
  vendor waiting time, legal review, UAT, sign-off); use it rather than dropping the row.
- **AM-25 [B]** `capacity_hours_per_fte_month`, `parallelism` and every `dev_fte_per_phase` value
  must be > 0; `ba_split` and `qa_split` must each sum to 1.00; an unknown key anywhere in the
  Parameters block is rejected by name (a typo like `ui_std` must not be silently ignored).

## Classic hours by size and bucket

| Bucket key | S | M | L | XL |
|---|---|---|---|---|
| `crud` | 8 | 16 | 32 | 56 |
| `ui-std` | 8 | 16 | 32 | 64 |
| `ui-custom` | 16 | 32 | 64 | 120 |
| `integration` | 16 | 32 | 64 | 120 |
| `domain` | 24 | 48 | 96 | 160 |
| `migration` | 16 | 40 | 80 | 160 |
| `infra` | 8 | 24 | 48 | 96 |
| `security` | 16 | 32 | 64 | 120 |
| `ba-workshop` | 4 | 8 | 16 | 32 |
| `ba-docs` | 4 | 8 | 16 | 32 |
| `qa-design` | 4 | 8 | 16 | 32 |
| `qa-auto` | 8 | 16 | 32 | 64 |
| `qa-manual` | 4 | 8 | 16 | 32 |
| `qa-regression` | 4 | 8 | 16 | 32 |
| `pm` | 4 | 8 | 16 | 32 |

- **AM-30 [B]** Classic hours per row = table[bucket][size], for every bucket (dev and role);
  overrides go in Parameters `size_hours`.
- **AM-31 [W]** A row that would exceed XL is split into several rows by the researcher, not
  estimated as "XXL".
- **AM-32 [W]** Size is judged on the classic effort, never on the AI-assisted effort; k does the
  AI part.
- **AM-33 [B]** Derived role hours per phase (classic), as ratios of the phase's classic DEV hours
  (dev-bucket rows only): BA 15% (split `ba-workshop` 40% / `ba-docs` 60%), QA 30% (split
  `qa-design` 25% / `qa-auto` 35% / `qa-manual` 25% / `qa-regression` 15%), PM 12%. All ratios
  and splits are Parameters. A phase with zero dev hours has zero derived hours.
- **AM-34 [S]** Explicit role-bucket rows in the inventory are added on top of the derived hours,
  not instead of them, in both columns.
- **AM-35 [W]** Spikes listed by the architect (`## Spikes needed`) are costed as explicit rows the
  researcher adds on the orchestrator's request: bucket `domain` or `integration` by nature, size S
  or M, `k_override: 1.00` (a spike is exploration, AI does not shorten it). A spike with no row is
  reported in Assumptions & Risks as uncosted.

## AI overheads (added on top of AI-native hours)
- **AM-40 [B]** AI output review per phase = `review_overhead` (default 10%) × the phase's AI dev
  base, where AI dev base = Σ over dev-bucket rows of classic × k_eff (AM-56) — BEFORE the
  overhead itself, setup, and role hours are added.
- **AM-41 [B]** AI tooling and project-context setup = `ai_setup_hours` (default 24) once per
  project, booked in `P1`, not scaled by scenario deltas.
- **AM-42 [W]** Overheads are shown as their own lines in Phases and Summary, never blended into k.
- **AM-43 [S]** Projects with an existing AI-ready codebase (agent instructions, test harness) may
  set `ai_setup_hours` to 8 with a justification.
- **AM-44 [W]** Overheads apply to the AI-native column only; the classic column has none.

## Roll-up, team, timeline and scenarios
Parameters: `capacity_hours_per_fte_month` (default 160), `parallelism` (default 0.80),
`dev_fte_per_phase` (architect's proposal, per phase), scenario deltas and buffer below.

- **AM-50 [B]** Per row: `classic_h` = AM-30; `k_row` = `k_override` if set, else the bucket k;
  `k_eff(s)` = min(1.00, k_row + delta(s)) with delta = 0 / `scenario_delta.realistic` /
  `scenario_delta.pessimistic`; `ai_h(s)` = classic_h × k_eff(s). The delta applies to every
  bucket, dev and role, and to `k_override` rows alike.
- **AM-51 [B]** Per phase and scenario s:
  `D_c` = Σ classic_h of dev rows; `D_a(s)` = Σ ai_h(s) of dev rows (the AI dev base);
  `R(s)` = review_overhead × D_a(s); `S` = ai_setup_hours in P1 else 0;
  derived classic role hours from `D_c` by AM-33, their AI hours = each sub-bucket's classic hours ×
  k_eff(bucket, s); explicit role rows per AM-34;
  `T_c(s)` = (D_c + derived classic + explicit classic) × buffer(s);
  `T_a(s)` = (D_a + R + S + derived AI + explicit AI) × buffer(s);
  buffer = 1.00 / 1.00 / `risk_buffer_pessimistic` (default 1.15) — the pessimistic buffer multiplies
  every hour of both columns, overheads and setup included.
- **AM-52 [B]** A row with an empty `phase`, or a phase id not listed in `## Delivery phases`,
  aborts the estimate; the estimator lists the orphan ids and stops (no invented phase).
- **AM-53 [B]** Phase months per column and scenario = round-up-to-0.5( load / (fte_<phase> ×
  capacity × parallelism) ), with load = `D_c × buffer` for the classic column and
  `(D_a + R + S) × buffer` for the AI column. A phase with zero dev rows uses its total column
  hours (`T_c` / `T_a`) as the load instead, so months are never zero for a non-empty phase.
- **AM-54 [B]** FTE per non-dev role per phase = round-up-to-0.25( role hours of that column and
  scenario / (that column's months × capacity) ). Dev FTE = `fte_<phase>` as given (an input, not
  a result).
- **AM-55 [W]** Phases are sequential by default; the architect may mark phases parallel, and the
  Summary then reports both the sum and the critical path.
- **AM-56 [B]** Three scenarios: optimistic (delta 0, buffer 1.00), realistic (delta 0.10, buffer
  1.00), pessimistic (delta 0.20, buffer 1.15); order of operations is exactly AM-50 → AM-51 →
  AM-53 → AM-54. The classic column changes only through the pessimistic buffer.
- **AM-57 [B]** The realistic scenario is the headline in Summary and in the summary report; the
  other two are shown next to it, never hidden.
- **AM-58 [B]** Totals are reported in hours, person-months (hours / capacity) and FTE —
  never a currency figure, never a rate, never a cost, never a budget, in any augur artifact or chat line.
- **AM-59 [W]** Every rounded figure keeps its unrounded source in the xlsx (a hidden column is fine).

## Worked example (the challenger recomputes this; an estimator that cannot reproduce it is wrong)
Defaults, `dev_fte_per_phase: { P1: 2, P2: 1 }`. Inventory:

| id | size | bucket | k_override | phase | classic_h |
|---|---|---|---|---|---|
| F-001 | M | `crud` | — | P1 | 16 |
| F-002 | L | `integration` | — | P1 | 64 |
| F-003 | S | `qa-auto` (explicit: automation framework setup) | — | P2 | 8 |

P1, optimistic: D_c = 80; D_a = 16×0.35 + 64×0.65 = 47.20; R = 4.72; S = 24.
Derived classic: BA 12 (workshop 4.8, docs 7.2), QA 24 (design 6, auto 8.4, manual 6, regression 3.6), PM 9.6.
Derived AI: BA 4.8×0.80 + 7.2×0.40 = 6.72; QA 6×0.45 + 8.4×0.40 + 6×0.80 + 3.6×0.30 = 11.94; PM 9.6×0.90 = 8.64.
T_c = 80 + 12 + 24 + 9.6 = 125.60; T_a = 47.20 + 4.72 + 24 + 6.72 + 11.94 + 8.64 = 103.22.
Months classic = 80 / (2×160×0.8) = 0.31 → 0.5; months AI = (47.20+4.72+24) / 256 = 0.30 → 0.5.
BA FTE classic = 12 / (0.5×160) = 0.15 → 0.25; QA 24/80 = 0.30 → 0.50; PM 9.6/80 = 0.12 → 0.25.

P1, realistic (every k + 0.10): D_a = 16×0.45 + 64×0.75 = 55.20; R = 5.52;
BA AI = 4.8×0.90 + 7.2×0.50 = 7.92; QA AI = 6×0.55 + 8.4×0.50 + 6×0.90 + 3.6×0.40 = 14.34; PM AI = 9.6×1.00 = 9.60.
T_c = 125.60; T_a = 55.20 + 5.52 + 24 + 7.92 + 14.34 + 9.60 = 116.58.

P1, pessimistic (every k + 0.20, cap 1.00, then ×1.15): D_a = 16×0.55 + 64×0.85 = 63.20; R = 6.32;
BA AI = 4.8×1.00 + 7.2×0.60 = 9.12; QA AI = 6×0.65 + 8.4×0.60 + 6×1.00 + 3.6×0.50 = 16.74; PM AI = 9.60.
T_a = (63.20 + 6.32 + 24 + 9.12 + 16.74 + 9.60) × 1.15 = 128.98 × 1.15 = 148.33; T_c = 125.60 × 1.15 = 144.44.

P2 (zero dev rows → no derived roles, no review overhead, no setup): F-003 classic 8;
AI = 8×0.40 = 3.20 (opt) / 8×0.50 = 4.00 (real) / 8×0.60×1.15 = 5.52 (pess).
Months: load = total column hours (AM-53 zero-dev rule): classic 8/(1×160×0.8) = 0.06 → 0.5;
AI 3.20/128 → 0.5. QA FTE classic = 8/(0.5×160) = 0.10 → 0.25.

Project realistic headline: classic 125.60 + 8 = 133.60 h; AI-native 116.58 + 4.00 = 120.58 h;
person-months = 133.60/160 = 0.84 vs 120.58/160 = 0.75.

## BA / QA delta
- **AM-60 [B]** A dedicated block (md section + xlsx sheet `BA-QA Delta`) reports classic vs
  AI-native hours for BA and QA per phase and in total, with savings itemised per bucket (derived
  and explicit rows together, per bucket).
- **AM-61 [B]** Explicit rows for UAT, stakeholder sign-off, production acceptance and client
  workshops beyond the derived `ba-workshop` share are not AI-reducible: they carry
  `k_override: 1.00` and are listed as "not reducible" in the block.
- **AM-62 [W]** The delta is expressed in hours and FTE; a percentage is allowed next to the hours.
- **AM-63 [W]** The block names WHAT the AI does instead of the saved hours (generated test cases
  from acceptance criteria, user stories drafted from the inventory, regression suites generated
  from the automation layer) — a saving with no mechanism is a WARNING.
- **AM-64 [S]** When the client has its own QA team, the block separates vendor QA from client QA.
- **AM-65 [W]** Savings above 60% on `ba-workshop` or `qa-manual` are flagged for justification.
- **AM-66 [S]** The block closes with the residual human roles per phase (who still signs off).

## Calibration
- **AM-70 [W]** After a delivered project, actual hours per bucket are compared to the estimate and
  a dated entry is appended to the log below — in the **repo copy** of this file
  (`augur-crew/skills/augur-method/SKILL.md`), then re-installed with `install.ps1`. The installed
  copy under `~/.qwen` is overwritten on every install, so an entry made only there is lost.
  k and size-table defaults change only through such an entry.
- **AM-71 [W]** Until the log has an entry, every estimate states "defaults uncalibrated" in its
  Assumptions & Risks block.
- **AM-72 [S]** A calibration entry lists: project, date, bucket, estimated k, observed k, decision.

### Calibration log
| Date | Project | Bucket | Estimated k | Observed k | Decision |
|---|---|---|---|---|---|
| — | — | — | — | — | no calibration yet |

## Parameters block (top of every estimate md)

```yaml
augur:
  client: acme
  date: 2026-10-15
  capacity_hours_per_fte_month: 160
  parallelism: 0.80
  review_overhead: 0.10
  ai_setup_hours: 24
  role_ratios: { ba: 0.15, qa: 0.30, pm: 0.12 }
  ba_split: { ba-workshop: 0.40, ba-docs: 0.60 }
  qa_split: { qa-design: 0.25, qa-auto: 0.35, qa-manual: 0.25, qa-regression: 0.15 }
  scenario_delta: { realistic: 0.10, pessimistic: 0.20 }
  risk_buffer_pessimistic: 1.15
  k: { crud: 0.35, ui-std: 0.45, ui-custom: 0.70, integration: 0.65, domain: 0.80, migration: 0.75, infra: 0.60, security: 0.90, ba-workshop: 0.80, ba-docs: 0.40, qa-design: 0.45, qa-auto: 0.40, qa-manual: 0.80, qa-regression: 0.30, pm: 0.90 }
  size_hours: { crud: [8,16,32,56], ui-std: [8,16,32,64], ui-custom: [16,32,64,120], integration: [16,32,64,120], domain: [24,48,96,160], migration: [16,40,80,160], infra: [8,24,48,96], security: [16,32,64,120], ba-workshop: [4,8,16,32], ba-docs: [4,8,16,32], qa-design: [4,8,16,32], qa-auto: [8,16,32,64], qa-manual: [4,8,16,32], qa-regression: [4,8,16,32], pm: [4,8,16,32] }
  dev_fte_per_phase: { P1: 3, P2: 3, P3: 2 }
  overrides_justification: []
```

- **AM-80 [B]** The block above is complete and printed in full in every estimate; the xlsx
  `Parameters` sheet mirrors it through the named cells of AM-82, one cell per scalar value.

## xlsx layout
- **AM-81 [B]** Sheets, in order: `Summary`, `Parameters`, `Features`, `Phases`, `Team`,
  `BA-QA Delta`, `Assumptions & Risks`.
- **AM-82 [B]** `Parameters` is the only sheet meant for hand edits; every number elsewhere is a
  formula referencing it. Named cells (hyphens in bucket keys become underscores):
  `capacity` ← capacity_hours_per_fte_month; `parallelism`; `review_overhead`; `ai_setup_hours`;
  `ratio_ba` / `ratio_qa` / `ratio_pm` ← role_ratios; `split_ba_workshop` / `split_ba_docs` ←
  ba_split; `split_qa_design` / `split_qa_auto` / `split_qa_manual` / `split_qa_regression` ←
  qa_split; `delta_realistic` / `delta_pessimistic` ← scenario_delta; `risk_buffer` ←
  risk_buffer_pessimistic; `k_<bucket>` (15 cells) ← k; `size_<bucket>_<S|M|L|XL>` (60 cells) ←
  size_hours; `fte_<phase>` (one per phase, e.g. `fte_P1`) ← dev_fte_per_phase.
- **AM-83 [B]** `Features` columns: `id | name | module | source | size | bucket | k_override |
  confidence | phase | classic_h | k_row | ai_h_opt | ai_h_real | ai_h_pess` where `classic_h`
  looks up `size_<bucket>_<size>`, `k_row = IF(k_override="", k_<bucket>, k_override)`, and each
  `ai_h_*` = `classic_h * MIN(1, k_row + delta)` (`delta` = 0 / `delta_realistic` /
  `delta_pessimistic`; the pessimistic buffer is applied on `Phases`, not here).
- **AM-84 [B]** `Phases` rows per phase, one column group per scenario: D_c, D_a, R, S, derived
  BA/QA/PM classic and AI, explicit role rows classic and AI, T_c, T_a, months classic, months AI
  (AM-51, AM-53), all as formulas over `Features` and `Parameters`.
- **AM-85 [B]** `Team`: FTE per role per phase per scenario (AM-54), dev FTE = `fte_<phase>`.
- **AM-86 [B]** `BA-QA Delta`: per phase and total, classic vs AI per BA/QA bucket, saving h and %.
- **AM-87 [W]** `Summary`: headline = realistic; optimistic and pessimistic next to it; person-months.
- **AM-88 [W]** `Assumptions & Risks`: every `low` confidence row, every assumption, every
  unprocessed input, the spikes the architect listed, the calibration status (AM-71).
- **AM-89 [B]** Workbook is generated with the `xlsx` skill (openpyxl writes the formulas; the
  skill's recalc step — LibreOffice headless — fills the cached values), then scanned for
  `#REF!` / `#NAME?` / `#DIV/0!` / `#VALUE!` before the file is reported as done; no pasted values
  where a formula is prescribed. Without a recalculation the md totals cannot be copied from the
  workbook (openpyxl `data_only=True` returns empty cells) — the estimator must say so instead of
  computing the totals by hand.

---

## Output Contract (MANDATORY)
This is a reference skill: it is loaded, not run. When invoked directly (calibration request),
end with: 1. **Done** — the calibration entry added to the repo copy. 2. **Next step** —
`install.ps1`, then re-run `augur-estimator` on the open pre-sales project with the new
defaults. 3. **Alternatives** — leave defaults, record the observation only. Suggest, never
auto-proceed.
