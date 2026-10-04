---
feature: 116
slug: crewkit-in-repo
status: draft
created: 2026-10-04
---

# Spec 116 — Crew kit in-repo: pack from repo, sync repo → home

<goal>
Overmind releases stage the crew kit from the in-repo clone (src/OvermindCrewKit — the embedded dotNET-Coding-Agents-Crew checkout), not from whatever happens to be deployed in ~/.claude. The flow becomes one-directional: repo → bundle → home. Never home → bundle.
</goal>

<scope>
- R1. pack.sh default: `--crew-kit` defaults to `<repo_root>/src/OvermindCrewKit` (resolved from repo_root, not the hardcoded home path). CREW_ITEMS in build/dist-layout.env re-pathed to the kit repo layout (`.claude/agents`, `.claude/skills`, `.claude/CLAUDE.md`, `.claude/SKILLFLOW.md`, SOCIAL_DICTIONARY source — per the actual layout.env contract); the SKILL.md count guard follows the new path.
- R2. Drift guard: when the kit working tree carries uncommitted changes, pack.sh prints a loud warning (bundling as-is) but does not die — the kit is a separate repo the user pushes on their own rhythm.
- R3. Parent repo .gitignore: `src/OvermindCrewKit/` — the embedded clone lives its own git life with its own origin; no gitlink, no submodule (challengeable: submodule re-considered if release pinning of kit versions is ever wanted).
- R4. Docs + process rule: CONTEXT.md states the kit is the in-repo source of truth and the one-way flow; the user's «MCP changes flow into the kit» is a DISCIPLINED closeout step (a feature touching MCP surface — tools, gates, statuses, quickstart texts — must land its crew-kit docs sync in the same wave), NOT an auto-copy of texts between C# constants and skill markdown (two sources of truth drift; 099-W1 and 094-W1 precedents).
- R5. install.sh unpacking side stays untouched — it already consumes the bundled kit.
</scope>

<anti-scope>
- No submodule / gitlink in the parent repo.
- No auto-sync machinery between MCP texts and skill markdown.
- No gemini/antigravity runtime work.
- No changes to what the bundle contains (same CREW_ITEMS content, new paths only).
</anti-scope>

<constraints>
- pack.sh stays bash, absolute-path discipline (no pipes eating exit codes — deploy-lesson legend).
- The kit clone is the user's repo: pack reads it, never commits/pushes it.
- Verification: a real `bash build/pack.sh --version … --bundle-version … --rids linux-x64` run once at closeout (the only honest gate for a packing script), plus `bash -n` shellcheck-style pass.
</constraints>

<acceptance-criteria>
- [ ] pack.sh with no --crew-kit stages from src/OvermindCrewKit (paths per new layout); dirty-kit warning fires on an uncommitted kit tree.
- [ ] Parent .gitignore ignores the embedded clone; git status of the parent repo stays clean around it.
- [ ] CONTEXT.md carries the source-of-truth statement and the MCP→kit closeout rule.
- [ ] One real pack run (linux-x64) succeeds end-to-end from the repo default; result drafted.
</acceptance-criteria>

<refs>
- User intake 2026-10-04 (p0 by user order: «фикс для pack.sh — обязателен, тоже p0»); build/pack.sh; build/dist-layout.env; src/OvermindCrewKit/manifest.json (kit's own runtime layout); 114 (first in-repo kit exercise); 116 name-sake feature body.
</refs>

<log>
- 2026-10-04 [agent] — spec pre-flown draft (user away; conservative defaults, challengeable items marked). Priority p0 per user order.
</log>