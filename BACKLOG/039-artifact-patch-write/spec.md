---
feature: 039
slug: artifact-patch-write
status: validated
created: 2026-09-22
validated: 2026-09-22
---

# Spec 039 — Artifact patch write: append `<log>` / replace XML-секции / tick AC — без перезаливки тела

<decisions>
- **D1 — Patch is a separate operation, not a mode of `planning_upsert_artifact` (agent).** New agent endpoint `POST /api/agent/planning/artifacts/{id}/patch` + new MCP tool `planning_patch_artifact`. Upsert keeps its single contract ("full body, create or replace"); overloading it with a patch mode puts two contracts in one tool and field agents already know upsert as the full-body call. Rejected: extending upsert with optional section params — ambiguity when both `body` and `section` are present, and discovery does not reveal the mode.
- **D2 — Two operations: `replace_section` and `tick_ac` (agent).** Append-to-log already exists end-to-end (`planning_append_log` → `AppendLogAsync` → `PlanningLog.Append` with an anchored `<log>` parser) and is NOT duplicated here; the field complaint about append was adoption, not absence — closed by D9/R7. One tool carries both operations, dispatched by parameters (`section`+`content` → replace; `acIndex`/`acText` → tick). Rejected: two narrow tools — discovery noise for two rare, homogeneous operations.
- **D3 — A general section parser generalises `PlanningLog` (agent).** The anchored-pair logic (last `<name>...</name>` pair on its own lines, `FindLastAnchoredPair`) moves from `PlanningLog` into a shared `PlanningSections` helper: `Find(name)`, `Replace(name, content)`, `List()`. `<log>` remains a special case of it. No JSON-Patch/RFC-6902 — the field asked for three concrete operations, not a generic protocol.
- **D4 — Every patch snapshots the body like `UpdateBody` does (agent).** `PlanningArtifactVersion.Create(Id, Version, Body)` + `Version++`, so version history stays complete for the upcoming diff feature (045). `AppendLog` (entity `:203`, service `:162`) currently does NOT snapshot — it is brought under the same discipline in this feature. This is the only behavioural change to an existing tool and is called out in its log.
- **D5 — Frozen semantics inherit `UpdateBody` (agent).** A patch on a `validated` artifact requires a `logNote` and demotes it back to `draft` — identical to a full-body update. No logNote required when not frozen.
- **D6 — Strict errors, idempotent tick (agent).** Replacing a section that does not exist is a 400 (creating sections is the full-body upsert's job, not a patch side effect — the field explicitly punished silent creation). Ticking a not-found AC item is a 400 that lists the items it can see. Ticking an already-ticked item is a successful no-op — a retried call after a client timeout must not punish the agent. Marked `Idempotent = true` on the tick path.
- **D7 — Echo follows the 037 slimming contract (agent).** One compact line: `{artifactId, kind, section, version, bodyChars}` — no body, no feature payload.
- **D8 — Materialisation unchanged (agent).** A patch is an ordinary mutation: `SyncMaterializationAsync` after save, clobber-guard (033) semantics untouched.
- **D9 — Cost-transparent write surface (user, gate feedback).** The write tools form a three-tier cost model and their descriptions must state the tier explicitly, so tool discovery answers "what do I use": `planning_append_log` — "cheapest write: one dated line, body is not resent"; `planning_patch_artifact` — "middle: replaces one XML section, rest of the body untouched"; `planning_upsert_artifact` — "most expensive write: resends the full body". Descriptions of the two existing tools are updated in this feature. The read side gets the symmetric treatment in 040's spec (cheap partial / expensive full). Rationale: the DMT field report proved agents cannot pick correctly today — the append tool existed and was still complained about as missing.
</decisions>

<open-questions>
- Should `replace_section` on `<log>` be allowed (competing with `planning_append_log`)? Recommendation: reject with 400 — the log is append-only by design; rewriting history belongs to a full-body update with a logNote.
- Web-UI access to patch operations? Out of scope for the first iteration (the UI editor writes whole bodies); revisit if field feedback asks.
</open-questions>

<requirements>
- R1 — `POST /api/agent/planning/artifacts/{id}/patch` accepts `{ op: "replace_section", section, content, logNote? }` and `{ op: "tick_ac", acIndex? , acText?, logNote? }`; validation errors return 400 with a diagnostic that names what was searched.
- R2 — MCP tool `planning_patch_artifact` mirrors R1, `Destructive = false`, tick idempotent, echo per D7.
- R3 — `PlanningSections` parser (D3) with unit coverage: single section, missing section, multiple same-name sections (last anchored pair wins), content spanning blank lines, CRLF tolerance.
- R4 — AC parsing reads `<acceptance-criteria>` as a list; tick marks the matched item `[ ]`→`[x]` preserving indentation and suffix text.
- R5 — Version snapshot on every patch and on `append_log` (D4); frozen artifacts demote per D5.
- R6 — Tests: parser suite; patch round-trip through materialisation (disk file reflects the patched section); tick idempotency; frozen demotion requiring logNote; echo compactness pin (no body in response).
- R7 — Agent discoverability (extended per D9): `SessionBootstrapPrompt` carries the pick-by-cost routing line — "line → `planning_append_log`; one section → `planning_patch_artifact`; whole body → `planning_upsert_artifact`" — and the three tool descriptions state their cost tier verbatim.
</requirements>

<non-goals>
- Append to `<log>` — already shipped (`planning_append_log`); not duplicated.
- Batch patches across artifacts — second wave (044).
- Partial reads (`sections` param, read-by-id) — 040 (its spec carries the read-side cost symmetry).
- Version diff — 045.
- Generic JSON-Patch protocol — three concrete operations only.
- Web-UI patch surface.
- Renaming existing tools — cost tiers go into descriptions, not into new names.
</non-goals>

<context>
Field sources: FEATURELIST/04 (4–5x token cost, log-append as the most frequent op), 01 (edit-one-line ≈7K vs ≈200 tokens), 03, 08. Code facts this spec relies on: `PlanningArtifact.UpdateBody` (snapshot + freeze semantics, `PlanningArtifact.cs:131-158`), `PlanningLog.Append` anchored parser (already inserts before `</log>`), `planning_append_log` MCP tool (`OvermindPlanningTools.cs:211`, compact echo already), `AppendLogAsync` service (`PlanningArtifactService.cs:162`). D9 originates from the user's gate review: "if both exist, it is unclear WHAT to use in MCP — make the read/write split explicit, cheap append vs expensive replace".
</context>

<log>
- 2026-09-22 — User gate feedback: cost-transparent MCP surface — write tools state their price in descriptions (D9), bootstrap carries a pick-by-cost routing line (R7 extended); read-side symmetry noted for 040.
</log>
