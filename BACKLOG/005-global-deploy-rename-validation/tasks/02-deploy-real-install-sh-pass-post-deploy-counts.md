---
feature: 005
task: 02
slug: global-deploy-rename-validation
status: done
review: light
created: 2026-09-27
brief: Deploy: real install.sh pass + post-deploy counts
---

<goal>Real deploy pass: run the crew installer so all four homes get the 003 output.</goal>
<scope>Run ./install.sh from the repo root (NOT --dry-run). Capture the summary output into the log. Then re-list per-home skills/agents counts. Do NOT run install.ps1 (Windows-only human step per feature non-goals).</scope>
<anti-scope>No content edits anywhere; no uninstall; no manual copying outside the installer.</anti-scope>
<constraints>Installer is manifest-driven; user explicitly ordered the deploy (chat 2026-09-28); NO git commit.</constraints>
<acceptance-criteria>
- [x] install.sh exits 0 with captured output
- [x] Post-deploy counts listed per home; mirror-mode homes no longer contain task-orchestrator (its removal there is expected — rsync --delete)
</acceptance-criteria>
<refs>Feature body; SYNC-STRUCTURE Deploy modes.</refs>
<obstacles></obstacles>
<log>
- 2026-09-27 [agent] — Deploy started: real ./install.sh pass from repo root, user-ordered (chat 2026-09-28).
- 2026-09-27 [agent] — Normalized acceptance-criteria to multiline list so AC items parse before ticking.
- 2026-09-27 [agent] — install.sh run (real, NOT --dry-run) from repo root /mnt/Ubuntu-Dev/Projects/AI-Tools/Skills_dotNetCrew:
stdout: "Home /home/johan-boolman/.gemini not found, skipping its entries." / "Home /home/johan-boolman/.gemini/antigravity not found, skipping its entries."
exit code: 0
Post-deploy counts and presence:
- ~/.kimi-code: skills=26, agents=23; task-orchestrator=no/no (removed by rsync --delete, expected); feature-orchestrator=yes/yes; sprint-planner=yes/yes; SKILLFLOW.md IDENTICAL to repo; diff -rq skills+agents vs repo: empty (perfect mirror)
- ~/.claude: skills=26, agents=23; task-orchestrator=no/no (removed by rsync --delete, expected); feature-orchestrator=yes/yes; sprint-planner=yes/yes; SKILLFLOW.md IDENTICAL; diff -rq skills+agents vs repo: empty (perfect mirror)
- ~/.gemini/antigravity: SKIPPED by installer — ~/.gemini parent does not exist; this is installer-designed behavior (install.sh home_ok guard: antigravity tolerates missing antigravity subdir but requires ~/.gemini parent). No home created; nothing deployed there.
- ~/.qwen (merge mode): skills=27, agents=25; task-orchestrator=yes/yes SURVIVED (merge mode never deletes — expected, this is the T3 orphan pair); feature-orchestrator=yes/yes; sprint-planner=yes/yes; SKILLFLOW.md IDENTICAL; merge extras vs repo: skills/task-orchestrator, agents/task-orchestrator.md (T3 targets), agents/python-coder.md (non-crew, untouchable)
- 2026-09-27 [agent] — Deploy complete: install.sh exit 0; kimi+claude perfect mirrors (26/23, task-orchestrator auto-removed); qwen merged to 27/25 with the two expected task-orchestrator orphans left for T3; gemini/antigravity skipped — ~/.gemini absent, installer-designed guard, recorded as open item for the user.
- 2026-09-27 [agent] — Orchestrator review passed: install.sh exit 0, mirror homes perfect (26/23), qwen merged to 26 skills, gemini/antigravity skipped by existing-parent guard (designed) — open item: rerun install.sh when ~/.gemini appears.
</log>