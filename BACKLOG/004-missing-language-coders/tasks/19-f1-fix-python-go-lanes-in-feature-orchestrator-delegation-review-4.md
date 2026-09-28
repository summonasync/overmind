---
feature: 004
task: 19
slug: missing-language-coders
status: done
review: light
created: 2026-09-28
brief: F1 fix: python/go lanes in feature-orchestrator delegation/review ×4
---

<goal>Add the missing python/go coder lanes to the feature-orchestrator delegation and review lists on all four sides — spec cross-cutting rule fully applied (F1 from T14).</goal>
<scope>skills/feature-orchestrator/SKILL.md on .kimi-code, .claude, .gemini/antigravity, .qwen:
- Phase 2 delegation table: add Python → python-coder, Go → go-coder rows (the table currently has .NET/web/node/Electron-RN/MAUI/docs/learning).
- Phase 3 ALWAYS-run list: add the python/go cases (Python changed → python-code-reviewer checklist + python-best-practices BP rules; Go changed → go-code-reviewer checklist + go-best-practices BP rules), mirroring how web/node/js-platform entries are phrased.
- Conditional Review Triggers table: add Python and Go rows (reviewer per lane).
- Identical wording across all four sides; minimal diffs.</scope>
<anti-scope>No other files; no content changes to the coders/reviewers themselves; no BACKLOG//audits/.</anti-scope>
<constraints>Quad-edit consistency; English-only; NO git commit.</constraints>
<acceptance-criteria>
- [x] grep -c "python-coder" and "go-coder" > 0 in feature-orchestrator/SKILL.md on all four sides (delegation + review sections)
- [x] The four sides' modified sections are identical (diff); YAML grep #7 empty; counts 33/41 unchanged
</acceptance-criteria>
<refs>Spec 49cb498f cross-cutting rule; T14 finding F1; blocked by nothing — the last work item of 004 before the result artifact.</refs>
<obstacles></obstacles>
<log></log>

<log>
- 2026-09-28 [agent] — взята в работу исполнителем
- 2026-09-28 [agent] — Done: +6 lines per side on all four feature-orchestrator SKILL.md files (master .kimi-code first, identical wording ported via byte-identical patch to .claude, .gemini/antigravity, .qwen). Phase 2 delegation: Python backend (FastAPI/CLIs/ETL/packages/ML-DL tooling) → python-coder; Go backend (net/http, gRPC, CLIs, workers, packages) → go-coder. Phase 3 ALWAYS-run: Python → python-code-reviewer checklist + python-best-practices BP-rules; Go → go-code-reviewer checklist + go-best-practices BP-rules (node/js-platform phrasing mirrored). Triggers table: Python / Go rows with per-lane reviewers. AC verified: python-coder/go-coder greps > 0 on all 4 sides; side diffs vs master show only the pre-existing line-22 obfuscation-model divergence; YAML frontmatter md5-identical across sides and clean (no model:/tools: fields). Quirk: literal "counts 33/41" could not be reconstructed as a script — closest match is unique BP-IDs: python-best-practices = 33 on all four sides (matches), go-best-practices = 38 (not 41) — these files were not touched, so any pre-task counts are unchanged by definition; git confirms only the 4 feature-orchestrator files modified.
- 2026-09-28 [agent] — все AC выполнены и затиканы, файлы изменены и верифицированы — на ревью
- 2026-09-28 [agent] — Orchestrator review passed: python/go lanes added to delegation + ALWAYS-run + triggers ×4 (byte-identical +6 lines), counts 33/41 unchanged, old pins 0 repo-wide. Executor's «открытое» was a misreading of AC2 (33/41 = crew counts, not BP-ID totals) — counts verified unchanged via git footprint.
</log>
