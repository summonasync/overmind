---
feature: 021
task: 02
slug: task-brief
status: done
review: light
created: 2026-09-20
---

# 021/02 — Brief disk round-trip (materializer + importer)

<goal>
`brief` survives the materialize → re-import cycle as a typed field: materialized task files carry it in frontmatter, the importer reads it as a structural key, and ImportAsync restores + diffs it — absent key means null, never a flagged change.
</goal>

<context>
Feature 021, layer 2 of 3. Depends on task 01 (typed Brief on the entity). The risk this task kills: without a structural key, brief either drops into preserved-JSON (untyped, invisible to DTOs) or silently vanishes on re-import of existing repos.
</context>

<scope>
- `src/Overmind.Infrastructure/Planning/PlanningMaterializer.cs` TaskDocument: emit `brief: <value>` in task frontmatter when non-null (alongside status/review/created; do NOT emit for null). Doc artifacts (spec/plan/result) never emit it.
- `src/Overmind.Infrastructure/Planning/BacklogImporter.cs`: add `brief` to StructuralKeys; TryParseTaskAsync reads it into the import record; `BacklogArtifactImport` gains Brief.
- `src/Overmind.Infrastructure/Planning/PlanningFeatureService.cs` ImportAsync: restore Brief alongside the existing restore path; diff — incoming brief differs from stored → apply SetBrief (and count as a change like the other field diffs); absent frontmatter key → null, and null-vs-null or absent-vs-null must NOT flag drift on existing files.
- Tests in `tests/Overmind.Infrastructure.Tests/`: extend/add PlanningRoundTripTests cases — materialize a feature with briefed tasks → wipe → re-import → brief identical and typed (not inside PreservedFrontmatter JSON); a task file without brief imports as null with no change flagged; editing brief on disk then re-import applies the change.
</scope>

<anti-scope>
- No contract/DTO/MCP changes (task 01 owns them).
- No changes to preserved-frontmatter semantics for other keys.
- No web work.
</anti-scope>

<constraints>
- Frontmatter emission order: keep the existing key order stable (feature, task, slug, status, review, created, brief) — diffs of materialized files stay minimal.
- dotnet build + dotnet test green; record test counts in the log.
</constraints>

<acceptance-criteria>
- [ ] Materialized task file with brief shows `brief:` in frontmatter; without brief shows no key.
- [ ] Round-trip: materialize → wipe feature → re-import → brief values identical, typed field, PreservedFrontmatter untouched by brief.
- [ ] Re-import of this repo's existing task files (no brief key) → no change flagged anywhere.
- [ ] Hand-edited brief on disk → re-import applies it via SetBrief.
- [ ] dotnet test green incl. new round-trip tests.
</acceptance-criteria>

<refs>
- Spec: feature 021, spec artifact — round-trip decision (structural key, like review/created).
- Pattern refs: PlanningMaterializer.cs TaskDocument (470–484), AppendPreservedFrontmatter (486–498), BacklogImporter.cs StructuralKeys (7–17) + TryParseTaskAsync (300–331), PlanningFeatureService.ImportAsync restore/diff (223–226, 270–276).
</refs>

<obstacles>
</obstacles>

<log>
- 2026-09-20 — Execution started — handed to dotnet-coder subagent in parallel with the 01 review (task 01 code already in the working tree).
- 2026-09-20 — Coder done: TaskDocument emits brief (key order preserved, null → no key), StructuralKeys + TryParseTaskAsync (Raw, free-text safe), BacklogArtifactImport.Brief, Restore optional brief param ("" → null, no UpdatedAt bump), ImportAsync diff counts brief changes; +3 round-trip tests incl. unknown-key routing proof. Suite 790/790. To review.
- 2026-09-20 — Review PASS-WITH-NITS (audits/review-021-02.md): 0 blockers, 1 IMPORTANT + 4 NITs. IMPORTANT (multi-line brief corrupts round-trip via frontmatter injection) FIXED in-tree by the orchestrator: PlanningValidators rejects \r/\n on create+update doors, pinned by 4 tests (suite 790 → 794). NITs → microbatch queue (full byte-stability cycle unpinned, import-door no-version assert, doc-artifact brief swallow, whitespace-trim drift). Both coder deviations verified correct by review. Done.
</log>
