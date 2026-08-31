# Task 001/03 Report — adapt-claude-skills-agents

## Status: DONE_WITH_CONCERNS

## What was done
1. `mkdir -p .claude/skills .claude/agents`
2. `cp -r .kimi-code/skills/* .claude/skills/` (20 skill dirs), `cp .kimi-code/agents/*.md .claude/agents/` (18), `cp .kimi-code/SKILLFLOW.md .claude/SKILLFLOW.md`
3. Mechanical sed pass (`sed -i.bak` over all `.claude/**/*.md`, `.bak` deleted after) applying replacement-table rules 1–13, 15, 16 in table order:

```
sed -i.bak \
 -e 's|~/.kimi-code/plugins/managed/superpowers|superpowers plugin for Claude Code|g' \
 -e 's|~/.kimi-code/skills|~/.claude/skills|g' \
 -e 's|~/.kimi-code/|~/.claude/|g' \
 -e 's|kimi-code/kimi-for-coding-highspeed|haiku|g' \
 -e 's|kimi-code/kimi-for-coding|sonnet|g' \
 -e 's|kimi-code/kimi-k3|opus|g' \
 -e 's|kimi-code/k3-256k|opus|g' \
 -e 's|kimi-code/k3|opus|g' \
 -e 's|Kimi K2\.7|Claude Sonnet|g' \
 -e 's|Kimi K3|Claude Opus|g' \
 -e 's|K2\.7|Sonnet|g' \
 -e 's|\bK3\b|Opus|g' \
 -e 's|Kimi Code CLI|Claude Code|g' \
 -e 's|AgentSwarm|multiple Task tool calls in one message|g' \
 -e 's|Agent tool|Task tool|g' \
 -e 's|`Agent`|`Task`|g'
```

4. Manual rule-14 / context pass — `grep -rni 'kimi' .claude/` had exactly one hit after sed, fixed:
   - `.claude/skills/coding-challenge-orchestrator/SKILL.md:65` — stop-list `.kimi/` → `.claude/`
     (the AI-leakage stop-list for delivered challenge code; on the Claude side the marker dir is `.claude/`).
   - No `config.toml` secondary_model pool blocks existed in the copied content — nothing to reformulate.
5. Agent frontmatter rework (Python script, per file): merged `whenToUse` into `description` as
   `"... . Use when <whenToUse, first letter lowercased>"`, added `model:` per model-mapping
   (opus for the 7 listed, sonnet for the other 11), kept `tools:` lists where present,
   dropped the non-Claude keys `whenToUse:` and `subagents:`.

## Acceptance criteria — verification output (verbatim)

### crit1: counts
```
$ ls -d .claude/skills/*/ | wc -l
20
$ ls .claude/agents/*.md | wc -l
18
```
PASS

### crit2: SKILLFLOW clean
```
$ ls .claude/SKILLFLOW.md
.claude/SKILLFLOW.md
$ grep -ni 'k2\.7\|\bk3\b\|kimi' .claude/SKILLFLOW.md
grep exit: 1
```
PASS (empty)

### crit3: no kimi/K2.7/K3/AgentSwarm/config.toml anywhere in .claude
```
$ grep -rni 'k2\.7\|\bk3\b\|kimi\|agentswarm\|config\.toml' .claude/
grep exit: 1
```
PASS (empty)

### crit4: every agent frontmatter has name / description with "Use when" / model
All 18 files report `name=1 descUseWhen=1 model=1` (loop over `.claude/agents/*.md` grepping
`^name: `, `^description: .*Use when`, `^model: `). PASS

### crit5: exactly 7 opus agents
```
$ grep -rl 'model: opus' .claude/agents/ | wc -l
7
.claude/agents/ai-code-detector.md
.claude/agents/ai-obfuscation-reviewer.md
.claude/agents/ai-obfuscator.md
.claude/agents/architect-review.md
.claude/agents/audit-orchestrator.md
.claude/agents/coding-challenge-orchestrator.md
.claude/agents/task-orchestrator.md
```
PASS — matches model-mapping exactly.

### crit6: skill references from agents
```
$ grep -rn '~/.claude/skills/' .claude/agents/ | wc -l
18
```
PASS (≥ 17; every agent references its own skill).

### crit7: changed files reported, no commit
No `git commit`/`git push` run. Changed/new files (on top of tasks 01–02 state):
- New untracked dir `.claude/` — 18 agent files, 20 skill dirs, `SKILLFLOW.md`
- `TASKS/001-dual-tool-repo-layout/tasks/03-adapt-claude-skills-agents.md` — `<obstacles>` + `<log>` appended
- `.superpowers/sdd/001-dual-tool-repo-layout/task-3-report.md` — this report

## Manual rule-14 fixes (file:line)
- `.claude/skills/coding-challenge-orchestrator/SKILL.md:65` — `.kimi/` → `.claude/` in the deliverable stop-list.

## Other frontmatter edits (beyond sed)
- All 18 `.claude/agents/*.md` — frontmatter rewritten: `whenToUse` merged into `description`,
  `model:` added, `subagents:` key dropped (3 files: audit-orchestrator, coding-challenge-orchestrator,
  task-orchestrator — key not supported by Claude Code; delegation lists still exist in skill bodies),
  `tools:` kept where present (6 files).
- Agent files written with LF line endings (Python rewrite); `.claude/` is untracked so no CRLF diff noise.
  sed pass on skills/SKILLFLOW preserved original endings; `git status` CRLF warnings observed only
  concern pre-existing `.kimi-code/` files from tasks 01–02, not this task's output.

## Concerns / follow-ups
1. Skill `SKILL.md` files still carry Kimi-shaped frontmatter (`whenToUse:` key). Scope said agent
   frontmatter only; Claude Code ignores unknown frontmatter keys, so functional — but a future
   cleanup could fold `whenToUse` into skill `description` for strict Claude parity.
2. Dropped `subagents:` lists (14 allowed sub-agent names per orchestrator) are now only implicit
   in skill bodies. If Claude-side orchestration needs explicit delegation allowlists, that policy
   needs re-expressing (e.g. in CLAUDE.md or skill text) — flagged for task 04/user decision.
3. `grep -rn 'subagent' .claude/agents/` — no leftover references; bodies speak about sub-agents
   generically, which is fine.
4. Model names `haiku` appears nowhere after the pass (no `kimi-for-coding-highspeed` hits existed);
   model-mapping haiku clause had nothing to map — noted, not an error.
