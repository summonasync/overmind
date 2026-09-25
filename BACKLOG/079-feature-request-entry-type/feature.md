---
feature: 079
slug: feature-request-entry-type
status: draft
priority: p2
version: 1
created: 2026-09-25
---

# Feature 079 — Feature request entry type: ловить идеи отдельным типом энтрей (не note, не борда-фича)

<goal>
A dedicated entry type "feature request" beside note/decision/gate/guard — a place to catch product ideas the moment they are spoken ("добавить ещё feature Request entry type?"), so they stop evaporating in chat transcripts or hiding inside unrelated feature bodies.
</goal>

<requirements>
- Preliminary, to be sharpened in the Socratic round: new type in the entry-kind enum (Core), surfaced in list_entries/search filters, UI entry editor + read views, type-specific icon/label.
- Semantic difference from note: note = observation/finding, feature request = a ask/proposal for future work, potentially with origin (who asked, when, which session/feature).
- Relationship to planning: a feature request is NOT a board feature — intake only; promotion to a planning feature stays a human/agent ceremony act.
</requirements>

<non-goals>
- To be decided in spec: tombstone interaction (029 is the sibling — both touch the entry-type model), entries materialization interplay (051), auto-promotion to the board.
</non-goals>

<refs>
- Raised by the user in-session 2026-09-25 during 072 planning: "КСТАТИ - добавить к tombstone entry type - ещё feature Request entry type?"
- Siblings: 029-entry-tombstones (entry-type model changes), 051-entries-materialization (entries to disk), 002-kit-library (entry kinds origin).
</refs>