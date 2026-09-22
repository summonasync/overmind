---
feature: 043
slug: status-provenance
status: draft
created: 2026-09-22
---

# Result 043 — Status provenance: источник каждого перехода в логе (ui/agent/auto) + предсказуемые и документированные автопереходы

<summary>
Feature 043 shipped status provenance end to end: every status mutation writes a log line carrying its source, and the rules those lines express are stated up front in the onboarding quickstart. Log format is `- YYYY-MM-DD [source] — text` with sources agent/ui/auto/import; legacy lines (no marker) parse backwards-compatibly with Source null.
</summary>

<tasks>
01 — Marker format in PlanningLog (Append/Parse, legacy-tolerant) plus sourced service lines on every status-mutation door: ui update, agent note, import restore, auto transitions on artifact create and on all-tasks-done promotion. All eight surveyed paths stamped; grep gate clean (7 production SetStatus calls, zero unsourced). Review PASS-WITH-NITS (audits/review-043-01.md); both nits (numeric-marker rejection, internal no-log SetStatus overload) fixed in-tree by a parallel micro-round.
02 — Quickstart lifecycle block extended with the three factual rules and the marker legend; materialisation round-trip test (mixed legacy + sourced log lines re-import without corruption or duplication); verbatim pins for all four new lines; this result artifact.
</tasks>

<evidence>
- Gates after 043/02: dotnet build Overmind.sln 0W/0E; dotnet test 1172 passed (Core 197 / Infra 360 / Api 339 / Mcp 276) — baseline 1169 + the parallel fix's numeric-marker case + 2 new tests. Full-run Mcp flakes (PlanningSetStatusDescription_QuotesAllowedStatusesPerKind Task-kind, ResourceContractTests decisions-case) both green on isolated rerun; whole Mcp project 276/276.
- Round-trip: Import_MixedLegacyAndSourcedLogLines_SurviveRoundTripWithoutCorruptionOrDuplication (tests/Overmind.Infrastructure.Tests/BacklogImporterTests.cs) — done feature + done task with mixed log lines import verbatim (legacy parses Source null; agent/auto/ui/import markers map to the enum), re-import creates nothing, updates nothing, appends no re-import notes, bodies byte-identical.
- Quickstart pins: Quickstart_CoversLifecycleEdgeRulesAndMarkerLegend (tests/Overmind.Mcp.Tests/StatusModelContractTests.cs) pins the three rules + legend verbatim; 032 parser constraints still hold (per-kind status listings match PlanningArtifact.AllowedStatuses).
</evidence>

<deviations>
- Pins placed in StatusModelContractTests (032 pattern, direct OnboardQuickstart.Text assertions) instead of OnboardApiTests HTTP level (042 pattern): the rules are status-model documentation and belong in the same contract file as the other quickstart pins.
- No separate onboard lifecycle block exists to mirror: OnboardContracts carries structured materialization/validation blocks (042), but lifecycle text lives only in OnboardQuickstart.Text — single source, no fork created.
</deviations>
