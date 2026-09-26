---
feature: 070
task: 04
slug: response-polish-round3
status: done
review: light
created: 2026-09-26
brief: delete_entry MCP tool with kit/skill guards; permanence documented
---

# 070/04 — delete_entry MCP tool

<goal>
Spec R8: agents get a deletion door for entries; feature deletion documented as UI-only (task 03 owns the text).
</goal>

<scope>
- New MCP tool delete_entry(entryId, projectId?) — 020 resolution chain (explicit → OVERMIND_PROJECT_ID → onboard cwd), Guarded idiom; DELETE via the existing entries route.
- Guards: kit-linked entry (SkillKitEntries link) → 409 KitOwnedEntry with text "managed by a skill kit — the kit reimport or the UI owns it"; skill-kind entry → 409. notes/decisions/gates/guards delete cleanly.
- Description: deletion is permanent; entry tombstones do not exist (029 owns that future).
- Contract pins: HandshakeContractTests tool count +1; note deleted (gone from list_entries); kit-linked → 409 verbatim; skill → 409; description text pinned; resolution chain branches per the 020 test pattern.
</scope>

<anti-scope>
- Entry tombstones (029), backend model changes, other tools.
</anti-scope>

<constraints>
- .NET 10 / C# 14; no comments; English-only; no git commands; dotnet build 0 warnings + dotnet test green; 068/077 grep gates green.
</constraints>

<acceptance-criteria>
- [x] delete_entry removes a plain note (list_entries no longer returns it) (pin)
- [x] Kit-linked entry → 409 KitOwnedEntry verbatim (pin)
- [x] Skill-kind entry → 409 (pin)
- [x] Tool count contract updated; description pinned (permanence + no tombstones)
- [x] dotnet build 0 warnings + dotnet test green
</acceptance-criteria>

<refs>
- Spec cda4c5c7 R8/D1; 020 tool pattern (OvermindNotificationTools); KitReimport kit-owned semantics.
</refs>

<obstacles />

<log />

<log>
- 2026-09-26 [agent] — 070/04 picked up: delete_entry MCP tool over the existing entries DELETE route; kit-linked/skill guards; permanence description; contract pins
- 2026-09-26 [agent] — Coded (dotnet-coder): delete_entry shipped in OvermindTools (the entries-CRUD surface, update_entry client-side-scope precedent). Shape: delete_entry(entryId, projectId?) with the 020 resolution chain (explicit → OVERMIND_PROJECT_ID → onboard cwd, per-branch pins incl. the env-override-skips-onboard pattern). Guard design decision: the host DELETE /api/entries/{id} deletes unconditionally — no kit/skill distinction to pass through — and EntryResponse carries no kit-link data, but GET /api/kits exposes entryIds per kit, so the guards live CLIENT-SIDE in the adapter exactly as the task prescription anticipated: resolved-project ownership check (entry from another project refused before any deletion), skill-kind → SkillEntryDeleteRefused, kit membership via /api/kits → "KitOwnedEntry: entry '<id>' is managed by a skill kit '<name>' — the kit reimport or the UI owns it" (verbatim fragments pinned). No host changes, no enforcement duplicated (host had none), no DELETE call fires on any refusal path (asserted). Success echo: {id, type, deleted:true}. Description pins permanence ("permanent and immediate", "no tombstones", "no undo") + both refusal texts + the env override. Contract pins: Handshake 31→32 tools with delete_entry in the sorted list; clean-delete round-trip pin includes the gone-from-list_entries assertion via a mutable stub listing; 7 new tests total. Gates: 068 frozen-sweep and 077 proof-line green. Build 0 warnings; tests 1525/1525 (Core 215 / Infra 496 / Api 447 / Mcp 367, +7).
- 2026-09-26 [agent] — 070/04 done by dotnet-coder: delete_entry(entryId, projectId?) with the 020 resolution chain, client-side kit-linked (KitOwnedEntry verbatim) and skill-kind guards over the unconditional host DELETE route, permanence/no-tombstones description, 32-tool handshake contract. Evidence: build 0 warnings, tests 1525/1525 (Core 215 / Infra 496 / Api 447 / Mcp 367), 068/077 gates green. All 5 AC ticked; ready for light review — after this the feature goes to dogfood + closing review.
- 2026-09-26 [agent] — Fix round W1/W2 (review-070-04, applied): W1 — the gone-from-list pin now asserts ShouldNotContain("Note One") (the stub serves "Note One"; the old "Entry One" assertion was vacuous). W2 — KitsJson/KitsWithoutEntryJson reshaped to the live KitResponse wire shape (id, name, version, description, entryIds, sourceKind, sourcePath, importedAtUtc) replacing the invented origin key. Build 0 warnings; tests 1525/1525 (Core 215 / Infra 496 / Api 447 / Mcp 367).
</log>
