---
feature: 075
slug: materialization-git-hygiene
status: draft
priority: p1
version: 1
created: 2026-09-24
---

# Feature 075 — Materialization × git hygiene: untracked BACKLOG/ by default + explicit export/snapshot

<goal>
Materialized BACKLOG/ stops polluting the git working tree: one ceremony session (spec+plan+6 tasks, zero code touches) produced 77 git-status entries including 28 modified tracked files — planning noise drowns code diffs. Decide and implement the hygiene model: BACKLOG/ untracked by default (.gitignore) with an explicit export/snapshot command when a human wants to commit planning alongside code.
</goal>

<requirements>
- A strategy decision (spec required): gitignore-by-default vs tracked-by-default — breaking for existing repos, must be explicit.
- Explicit export/snapshot path for "commit the planning with the code" use cases.
- Cyrillic task filenames render as octal ladders in git output (\320\276…) — unreadable and unpatchable; same human-readability axis as microbatch #51 (\uXXXX in preserved frontmatter).
</requirements>

<non-goals>
- Removing materialization altogether; changing mirror content format (049 owns the index retirement); importer behavior.
</non-goals>

<refs>
Source: note «Полевые заметки агента (BobHouse 020, 2026-09-24)», B2 (entry c7f34f40-3cea-49ec-8e23-bb1d8fe8e9c9). Related: 049 (draft), microbatch #51.</refs>