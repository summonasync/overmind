---
feature: 070
slug: response-polish-round3
status: validated
created: 2026-09-26
validated: 2026-09-26
---

# Spec 070 — Response/doc polish round 3: единый echo-объект, no-op тексты, доки (resources/XML-секции/order), решение archived/delete (OM-15/16/17)

<goal>
Third-round polish closing OM-15/16/17. Agents see one uniform echo object from every mutation tool, no-op actions say so, docs point at things that exist, and agents get a deletion door for entries with feature deletion documented as UI-only.
</goal>

<decisions>
- D1 (user, 2026-09-26): OM-17 = delete_entry MCP tool (notes/decisions/gates/guards) + "feature deletion is UI-only" in the descriptions. NO archived feature status (done stays terminal; the standing escape-hatch idea remains unconfirmed).
</decisions>

<requirements>
- R1 (OM-15a, host first): artifact mutation POSTs stop returning empty 200 — /log, /status, /validate, patch/upsert already shaped — return a compact object {id, version, status, lastLogLine} (warnings? where the door produces them). Feature mutations (set_current, set_feature_status, feature upsert path) return the same shape keyed by slug. The MCP adapter then projects ONE uniform echo {id, version, status, lastLogLine, warnings?} across append_log, set_status, set_current, set_feature_status, validate, patch, upsert — replacing the current mix of bare strings and ad-hoc objects (037 lineage).
- R2 (OM-15b): re-ticking an already-ticked AC item is an honest no-op — response text "already ticked — no change", version NOT bumped, no log line. Distinct from a real patch echo.
- R3 (OM-15c): the review field in artifact projections (read_feature artifacts, planning_read_artifact echo) is present only for kind=task; document-kind payloads stop carrying it.
- R4 (OM-15d): feature.UpdatedAt moves when its artifacts mutate (append_log, patch, upsert, validate, set_status on an artifact) — a live feature stops looking frozen on the board. The touch happens in the same SaveChanges, no extra log line.
- R5 (OM-16a): MCP server instructions stop pointing at the dead overmind://resources — replace with the working pointer (list_entries / onboard payload). Closes the duplicate item in 082-п.3 (mark it there at close).
- R6 (OM-16b): planning_upsert_artifact description lists the expected XML sections per kind — task: goal/scope/anti-scope/constraints/acceptance-criteria/refs/obstacles/log (+ optional brief); document: free-form with <log> as the only machine-parsed section.
- R7 (OM-16c): order semantics stated once (in the upsert + upsert_tasks descriptions): task order = 1-based sort key within the feature; drives current_task picking and roster ordering; omitted order appends after the current max.
- R8 (OM-17a): new MCP tool delete_entry(entryId, projectId?) — resolution chain per the 020 pattern; deletes via the existing entries DELETE route. Guards: kit-linked entries (SkillKitEntries) → 409 KitOwnedEntry ("managed by a skill kit — reimport or the UI owns it"); skill-kind entries → 409; notes/decisions/gates/guards delete cleanly. Description states deletion is permanent and tombstones do not apply to entries (029 owns that future).
- R9 (OM-17b): planning_create_feature + planning_upsert_artifact descriptions state "features cannot be deleted or archived via MCP — the UI owns feature deletion".
- R10 (contract pins): uniform echo key-set pins for all seven mutation tools (Canned + McpTests), no-op text pin, review-scoping pin, feature-touch pin (updatedAt moves, no log line), instructions/description pins for R5–R7/R9, delete_entry lifecycle pins (note deleted; kit-linked 409; skill 409).
</requirements>

<non-goals>
- Archived feature status (D1); batch ops (044); pointer artifacts (046); anything 067/068/069 shipped; entry tombstones (029); web UI changes.
</non-goals>

<acceptance-ideas>
- Every mutation tool returns the same key-set (contract test enumerates tools and asserts the projection).
- Re-tick → "already ticked — no change", version unchanged, log unchanged; a REAL tick → normal patch echo.
- read_feature on a feature with a validated spec shows no review field on the spec payload; tasks keep theirs.
- Patch a task on an old feature → feature updatedAt moves on the board.
- MCP instructions contain no "overmind://resources"; upsert description lists per-kind sections; order semantics present.
- delete_entry removes a plain note (gone from list_entries); kit-linked note → 409 KitOwnedEntry; skill entry → 409.
- dotnet build + test green; 068/077 grep gates stay green.
</acceptance-ideas>

<context>
Code facts: 037 slimmed upsert/create_feature/write/update_entry to compact projections and set_status to {id, status, lastLogLine}, but the host returns EMPTY 200 on artifact /status and /log POSTs (PlanningArtifactsApi), so adapters project from request data — append_log/set_current/set_feature_status/validate still return ad-hoc shapes (037 obstacle, now fixable). TickAc on an already-ticked item exits early before ApplyBodyMutation (068 chain) but the response still reads like a patch. Feature.UpdatedAt is touched by feature-level mutations only — artifact mutations leave it stale. MCP ServerInstructions carry the dead overmind://resources pointer (BobHouse B6; same item as 082-п.3). Entries DELETE route exists with no agent-door tool; SkillKitEntries link kit-owned entries (KitReimport deletes kit-owned on reimport).
</context>

<refs>
- FEATURELIST/overmind-features-2026-09-24.md OM-15/16/17.
- 037/059 echo lineage; 032 self-documentation pattern; 020 tool pattern (delete_entry); 082-п.3 (duplicate resources item — 070 closes it, mark at close).
</refs>

<log>
- 2026-09-26 — spec drafted; D1 decided by user in chat (delete_entry + UI-only docs, no archived status).
- 2026-09-26 [agent] — validated with human proof: notification f6b71049-80d6-4c90-8daa-ea8ba7bd6c5d replied 2026-09-26 16:23:22 UTC
</log>