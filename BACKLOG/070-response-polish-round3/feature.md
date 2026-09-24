---
feature: 070
slug: response-polish-round3
status: draft
priority: p1
version: 1
created: 2026-09-24
---

# Feature 070 — Response/doc polish round 3: единый echo-объект, no-op тексты, доки (resources/XML-секции/order), решение archived/delete (OM-15/16/17)

<goal>
Third-round polish: (OM-15) mutation tools return one uniform object {id, version, status, lastLogLine, warnings?} instead of a mix of bare strings; a no-op re-tick says "already ticked, no change" instead of faking "patched"; review field stops leaking onto non-task artifacts; feature updatedAt moves on artifact patches (a live feature stops looking frozen on the board). (OM-16) the server instructions stop pointing at the dead overmind://resources (say list_entries(type=…)); the expected XML sections are listed once in the upsert description (task vs document kinds); order semantics (sort key) stated — three leftovers from round 1. (OM-17) deletion/archive reality: either archived status for features + delete_entry for notes, or the tool descriptions state plainly "deletion is UI-only" BEFORE an agent creates a test feature it cannot remove.
</goal>

<requirements>
- Uniform mutation echo object across append_log / set_current / set_feature_status / validate (set_status already has it).
- No-op tick text distinct from a real change.
- review field scoped to tasks in read_feature.
- Feature updatedAt ticks on artifact mutations.
- Instructions/description fixes: resources pointer, XML-sections list per kind, order semantics.
- OM-17 decision (archive/delete vs documented UI-only) taken in spec with the user.
</requirements>

<non-goals>
- Anything from 067/068/069; batch (044); pointer artifacts (046).
</non-goals>

<refs>
- FEATURELIST/overmind-features-2026-09-24.md OM-15/16/17 (P2).
- 037/059 echo lineage; 032 self-documentation; round-1 README items 13 (docs) still open.
</refs>