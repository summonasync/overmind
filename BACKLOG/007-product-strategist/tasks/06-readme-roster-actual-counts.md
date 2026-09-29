---
feature: 007
task: 06
slug: product-strategist
status: done
review: light
created: 2026-09-29
depends_on: 01
brief: README roster + actual counts
---

<goal>Fix the README crew roster: mention product-strategist, correct stale agent/skill counts.</goal>
<scope>
- `README.md` only.
- Roster sentence: add the product strategist role; recompute counts from the actual trees at execution time: `ls .claude/agents/*.md | wc -l` and `ls -d .claude/skills/*/ | wc -l`.
- Layout tree comments («N agent definitions», «N skill directories») updated to the same actual counts everywhere they appear.
</scope>
<anti-scope>No content rewrite beyond roster + counts; no other files.</anti-scope>
<constraints>Counts must be computed, not guessed; keep README language/style as-is.</constraints>
<acceptance-criteria>
- [x] README states counts equal to actual directory counts (verified by the two commands above)
- [x] Roster names product-strategist
- [x] All stale count mentions in the layout tree are updated
</acceptance-criteria>
<refs>Task 01 (role name); spec 364635bb (stale-counts finding: README said 22/24, actual 34/42 before this feature).</refs>
<obstacles>None.</obstacles>

<log>
- 2026-09-29 [agent] — computing actual counts across four trees, then roster + count fixes
- 2026-09-29 [agent] — DRIFT FOUND (out of scope, reported): .qwen/ is missing agents/git-commit-ai-detector.md AND skills/git-commit-ai-detector/ — present on claude/kimi/gemini (35/43 there, 34/42 on qwen). Pre-existing, not introduced by 007 (product-strategist files present on all four sides). SYNC-STRUCTURE.md mapping lists it for all four sides. README got honest per-tree numbers until fixed. Follow-up proposal: mirror to .qwen/, bump numbers, add a four-side presence grep to SYNC-STRUCTURE checklist.
- 2026-09-29 [agent] — README counts fixed to actual (claude/kimi/gemini 35/43, qwen honest 34/42), roster names product-strategist; qwen git-commit-ai-detector drift logged as out-of-scope finding
</log>
