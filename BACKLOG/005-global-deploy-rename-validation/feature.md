---
feature: 005
slug: global-deploy-rename-validation
status: done
priority: p1
version: 1
created: 2026-09-27
depends_on: 003-crew-role-boundaries
---

# Feature 005 — Global deploy of 003 + stale-rename validation across four tool homes

## Goal

Deploy the reworked crew (003 output) to the four global tool homes and PROVE the rename left no stale artifacts anywhere. Sits between 003 and 004: 004's implementation must start from deployed, verified homes.

## Scope

1. **Precondition (human):** user commits 003 — deploy runs from a committed tree, not a dirty working tree.
2. **Deploy:** run the crew installers (install.sh on this machine; install.ps1 documented for Windows) — manifest-driven mirror/merge into ~/.kimi-code, ~/.claude, ~/.gemini + ~/.gemini/antigravity, ~/.qwen. Dry runs were already validated in 003/T10; this is the real pass, executed on the user's explicit command.
3. **Stale-rename validation (the core of this feature):**
   - `task-orchestrator` absent from ALL four homes (dirs, agent files, SKILLFLOW.md copies, contract files, SOCIAL_DICTIONARY if it referenced it): grep + path checks.
   - Merge-mode orphan sweep (known limitation, SYNC-STRUCTURE «Deploy modes»): ~/.qwen/skills/task-orchestrator and ~/.qwen/agents/task-orchestrator.md are EXPECTED orphans after rename — detect and remove them (crew-owned content; removal completes the rename, user-approved scope).
   - Presence checks: feature-orchestrator + sprint-planner (skill dir + agent file) in all four homes; per-home counts match repo (23 agents / 26 skills).
   - Content freshness: SKILLFLOW.md / contract files in homes match the repo copies (diff).
4. **Report:** verbatim check results; any orphan/debris found → removed with paths listed; failures → routed back before 004 starts.

## Non-goals

- No skill content changes (deploy + validation only).
- No Windows machine execution here — install.ps1 pass is documented for the user to run on Windows separately.
- No changes to runtime settings/hooks (settings.json stays human-managed).

## Decisions

- Runs as a separate feature between 003 and 004 (user decision 2026-09-28); 004 depends on this one, not directly on 003.
- Orphan removal in merge-mode homes is IN scope (it is the completion of the 003 rename, not a new change).

<removed 2026-09-28 — deploy does not include a Windows pass from this machine; install.ps1 validation stays a documented human step, revisit if a Windows agent host appears.>

## Refs

- SYNC-STRUCTURE.md «Deploy modes»: mirror = rsync -a --delete / robocopy /MIR (self-cleaning for renames); merge = children-only (orphans on delete — the exact risk this feature validates).
- Blocked by 003 (needs the committed rename); blocks 004.

<log>
- 2026-09-27 [auto] — artifact created → inProgress
- 2026-09-27 [agent] — User ordered execution in chat («коммит, пуш, поехали дальше 005», 2026-09-28). All 3 tasks done via gated pipeline; my independent home sweep confirms: kimi 26/23, claude 26/23, qwen 26/24 (+1 foreign), task-orchestrator absent everywhere, presence 6/6, orphans removed exactly as scoped. Open item documented: ~/.gemini absent on this machine — antigravity side deploys on next install.sh run once the home exists.
</log>
