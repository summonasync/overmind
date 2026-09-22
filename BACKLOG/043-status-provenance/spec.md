---
feature: 043
slug: status-provenance
status: validated
created: 2026-09-22
validated: 2026-09-22
---

# Spec 043 — Status provenance: источник каждого перехода в логе (ui/agent/auto) + предсказуемые и документированные автопереходы

<decisions>
- **D1 — Every status mutation writes a log line carrying its source; the source is a marker in the line (agent).** Format: `- YYYY-MM-DD [source] — text` with source ∈ {agent, ui, auto, import}. The log stays where it lives today — the `<log>` section of the body (feature and artifact alike); no new table, no new entity. `PlanningLog.Append`/`Parse` learn the marker backwards-compatibly: lines without a marker are legacy and read as source unknown. Agent lines carry the user's logNote; ui/auto/import lines are service lines with no free text.
- **D2 — Door-blind doors no longer exist: every path stamps its source (agent).** Known mutation sites from the code survey: feature — UI update (`PlanningFeatureService.cs:87`, today silent), agent SetStatusAsync (:129, has logNote), importer restore (:238), artifact-driven auto-transitions (`TransitionOnArtifactCreate` :245, `TryPromoteToReviewAsync` :254); artifacts — agent SetStatus, the human-door status path (verify in task whether it takes a note today), importer restore (`:321`). UI lines read `status moved to '<status>' via ui`; auto lines carry the reason (`artifact created → inProgress`, `all tasks done + spec + plan → review`); import lines read `status restored from imported file`.
- **D3 — The documented lifecycle becomes the factual one, including the three previously silent rules (agent).** Quickstart/onboard lifecycle block gains: a done feature receiving a new artifact stays done (no transition fires — by design, done is terminal); a feature without a plan does not auto-promote even at 100% tasks done (spec AND plan required — the 037/038 quirk, now named); import restores statuses verbatim from files — imported done features were not "auto-closed" (this explains the ai-certification-task observation). These are facts of the code, verified in this spec's survey — documentation debt, not behaviour bugs.
- **D4 — No transition matrix changes (agent).** The survey found no divergence between implemented transitions and their intended semantics; 043 adds observability and documentation, not new rules. If execution uncovers a real divergence, it is fixed only with a spec amendment (logNote), never silently.
</decisions>

<open-questions>
- Marker placement `[source]` after the date vs at line end? Recommendation: after the date — lexicographically stable, greppable, reads naturally in materialised files.
- Should the human-door artifact status path (if it currently demands a note) be relaxed to optional-with-service-line? Recommendation: no — if it already demands a note, that door already provenance-carries; only silent paths gain service lines.
</open-questions>

<requirements>
- R1 — PlanningLog marker support (append + parse, legacy-tolerant) with unit coverage for both formats.
- R2 — All surveyed mutation paths write sourced lines per D2 (feature UI/agent/import/auto; artifact agent/human/import); no silent status change remains anywhere in the codebase (grep-verifiable: every `SetStatus` call on feature/artifact is preceded by a sourced line or carries a note).
- R3 — Service-line texts per D2 land in English and are pinned.
- R4 — Lifecycle documentation per D3 lands in the quickstart (and the onboard lifecycle block if separate), pinned.
- R5 — Materialisation round-trips the markers (import of a materialised file with sourced lines does not corrupt or duplicate them).
- R6 — Tests: parse legacy + new; each door's service line; auto-transition reasons; import restore line; quickstart pins.
</requirements>

<non-goals>
- Changing any transition rule or the matrix (D4).
- A separate audit table / query API for history — the `<log>` lines are the record.
- Web UI rendering of sources (the lines show up in existing log views as text).
- 045-style diffs.
</non-goals>

<context>
Field sources: FEATURELIST/06 — the DMT agent saw two features in done it had not set and could not distinguish user action from auto-transition from its own mistake; UI moves leave no trace while agent moves demand logNote (the asymmetry). FEATURELIST/08 — "four features landed in done on their own while three sat in inProgress with 100% of tasks done; could not derive the rule". Code survey (this spec): PlanningFeature.SetStatus has a no-log overload (:95-99) used by the UI path and the importer; auto-transitions live at PlanningArtifactService.cs:245-272; the importer restores file statuses verbatim (PlanningFeatureService.cs:238/:321). The three "unpredictable" observations map to: import restore, the spec+plan promotion requirement, and terminal done ignoring artifact creation — all by design, all previously undocumented.
</context>