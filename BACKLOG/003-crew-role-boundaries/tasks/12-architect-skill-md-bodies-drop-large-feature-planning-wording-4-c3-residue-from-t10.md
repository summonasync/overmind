---
feature: 003
task: 12
slug: crew-role-boundaries
status: done
review: light
created: 2026-09-27
brief: Architect SKILL.md bodies: drop «large feature planning» wording ×4 (C3 residue from T10)
---

<goal>Remove the residual ownership-adjacent wording «large feature planning» from architect SKILL.md bodies on all four sides — the last spec §5 (C3) residue found by the T10 checklist.</goal>
<scope>- skills/dotnet-architect/SKILL.md ×4 (.kimi-code, .claude, .gemini/antigravity, .qwen), line ~13: the sentence «Before any architectural decision or large feature planning, read documentation in strict order:» — reword «large feature planning» to ownership-neutral phrasing (e.g. «large structural work»), keeping the sentence's meaning (when to read docs).
- skills/next-js-architect/SKILL.md ×4, line ~19: same sentence, same rewording.
- Keep the identical wording across all four sides; keep each file's line structure intact.</scope>
<anti-scope>NO agent stub files (dotnet-architect stubs already fixed in T10; next-js-architect stubs are feature 004's rename territory). No other lines, no frontmatter, no BACKLOG/, no audits/.</anti-scope>
<constraints>Quad-edit consistency (same new sentence ×4); English-only; NO git commit.</constraints>
<acceptance-criteria>
- [x] grep -rn "feature planning" on the two SKILL.md files returns 0 hits on all four sides
- [x] The reworded sentence is identical on all four sides; parity 23/26 unchanged; YAML grep #7 still empty
</acceptance-criteria>
<refs>Spec f0fdb479 §5; T10 log (residue origin); blocked-by nothing — runs before the 003 result artifact.</refs>
<obstacles></obstacles>
<log>
- 2026-09-27 [agent] — взята в работу исполнителем
- 2026-09-27 [agent] — normalize: inProgress move duplicated the <log> section (empty original + appended), AC patching 400'd; merged into a single <log> via full upsert, body otherwise unchanged
- 2026-09-27 [agent] — normalize 2: AC section items moved onto their own lines (inline format after the opening tag was not parsed as items — patch kept returning 400), wording unchanged
- 2026-09-27 [agent] — normalize 3: closing </acceptance-criteria> moved to its own line (was glued to the last item) — matching the T10 body format where AC ticking worked
- 2026-09-27 [agent] — DONE: «large feature planning» → «large structural work» in architect SKILL.md bodies ×8 (dotnet-architect:13 ×4 + next-js-architect:19 ×4, Edit-tool exact string replace, one line per file). Verification: grep -rn "feature planning" over the 8 files → 0 hits (exit=1); new sentence byte-identical on all four sides (md5 equal per skill across .kimi-code/.claude/.gemini-antigravity/.qwen: dea79ea7 dotnet-architect, 3d88e2b9 next-js-architect); count parity 23 agents + 26 skills on every side; YAML grep #7 (grep -rnE '^(description|whenToUse): [^"|>-].*: ' over 4 agents dirs) → empty exit=1. Line structure intact, no frontmatter/other-line touches, no git commit. Obstacle: inProgress move duplicated the <log> section and AC patching 400'd ("no acceptance-criteria items") — root cause was AC item format, fixed by full upsert v4 (items on own lines + closing tag on its own line, matching T10's working format); wording of sections unchanged.
- 2026-09-27 [agent] — execution complete: 8 files edited, all ACs verified and ticked (grep 0 hits, sentence identical ×4, parity 23/26, YAML grep #7 empty) — submitted for review
- 2026-09-27 [agent] — Orchestrator review passed: 0 residue hits across 8 files, reworded sentence byte-identical on all four sides (sort -u = 1), parity 23/26 unchanged. Last task of 003 — feature ready for the result artifact.
</log>