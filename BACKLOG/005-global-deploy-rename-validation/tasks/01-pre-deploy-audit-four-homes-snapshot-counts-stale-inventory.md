---
feature: 005
task: 01
slug: global-deploy-rename-validation
status: done
review: light
created: 2026-09-27
brief: Pre-deploy audit: four homes snapshot (counts, stale inventory)
---

<goal>Verbatim pre-deploy snapshot of the four deployed homes — the before picture for the rename validation.</goal>
<scope>For each home (~/.kimi-code, ~/.claude, ~/.gemini/antigravity, ~/.qwen): ls skills + agents counts; task-orchestrator presence (skill dir, agent file); feature-orchestrator + sprint-planner presence; diff home SKILLFLOW.md vs repo copy. Record every result verbatim in the artifact log.</scope>
<anti-scope>No writes to homes; repo untouched.</anti-scope>
<constraints>Read-only audit; NO git commit.</constraints>
<acceptance-criteria>
- [x] Per-home snapshot recorded: counts, stale inventory (expected: task-orchestrator still present everywhere), SKILLFLOW diff status
</acceptance-criteria>
<refs>Feature body; plan-less tasks-only feature by user decision.</refs>
<obstacles></obstacles>
<log>
- 2026-09-27 [agent] — Pre-deploy audit started: read-only snapshot of the four homes before ./install.sh run.
- 2026-09-27 [agent] — Pre-deploy snapshot (read-only, 2026-09-28):
- ~/.kimi-code: skills=25, agents=22; task-orchestrator: skills/task-orchestrator=yes, agents/task-orchestrator.md=yes; feature-orchestrator=no/no; sprint-planner=no/no; SKILLFLOW.md: DIFFERS from repo (.kimi-code/SKILLFLOW.md — pipeline table rows 0.0/0.1/0.2/Tasks still say task-orchestrator; repo rows say feature-orchestrator + sprint-planner section missing in home copy)
- ~/.claude: skills=25, agents=22; task-orchestrator=yes/yes; feature-orchestrator=no/no; sprint-planner=no/no; SKILLFLOW.md: DIFFERS from repo (.claude/SKILLFLOW.md — same diff shape as kimi)
- ~/.gemini/antigravity: ~/.gemini does not exist at all (ls/stat: "No such file or directory") — fresh home, deploy must create it; nothing stale possible. Repo source .gemini/antigravity: skills=26, agents=23, SKILLFLOW.md present.
- ~/.qwen: skills=25, agents=23 (extra non-crew python-coder.md — untouchable); task-orchestrator=yes/yes (the T3 orphan pair); feature-orchestrator=no/no; sprint-planner=no/no; SKILLFLOW.md: DIFFERS from repo (.qwen/SKILLFLOW.md — same diff shape as kimi)
- Repo install source: all four copies (.kimi-code, .claude, .gemini/antigravity, .qwen) have skills=26, agents=23, SKILLFLOW.md; task-orchestrator skill+agent ABSENT from all repo copies; feature-orchestrator + sprint-planner PRESENT in all four.
- Stale inventory verdict: task-orchestrator still present in all three EXISTING homes (kimi, claude, qwen) — matches the expected before-picture; gemini home absent entirely.
- 2026-09-27 [agent] — Normalized duplicated <log> section (status-move + append_log created two) — single <log> kept, all verbatim results preserved.
- 2026-09-27 [agent] — Reformatted acceptance-criteria to multiline list so AC items parse; log lines untouched.
- 2026-09-27 [agent] — Pre-deploy audit complete: all four homes snapshotted verbatim (counts, task-orchestrator/feature-orchestrator/sprint-planner presence, SKILLFLOW diff status); ~/.gemini found absent entirely — fresh install expected; AC ticked.
- 2026-09-27 [agent] — Orchestrator review passed: before-snapshot verbatim, stale inventory confirmed (task-orchestrator everywhere, old SKILLFLOW), gemini home absence discovered and documented.
</log>