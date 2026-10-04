---
feature: 116
task: 01
slug: crewkit-in-repo
status: todo
review: light
created: 2026-10-04
brief: pack.sh repo-kit default + layout re-path + dirty warning + gitignore
---

# Task 01 — pack.sh stages the repo kit

<goal>
pack.sh defaults to the in-repo crew kit and speaks its layout; a dirty kit tree warns loudly instead of silently bundling drift.
</goal>

<scope>
- build/pack.sh: DEFAULT_CREW_KIT resolves to <repo_root>/src/OvermindCrewKit (repo_root computed before the default applies; --crew-kit still overrides).
- build/dist-layout.env: CREW_ITEMS re-pathed to the kit repo layout (.claude/agents, .claude/skills, .claude/CLAUDE.md, .claude/SKILLFLOW.md, SOCIAL_DICTIONARY source — follow the actual env contract; verify every path exists in the clone).
- SKILL.md count guard follows the new path ($crew_kit/.claude/skills|agents).
- Dirty-kit warning: `git -C "$crew_kit" status --porcelain` non-empty → loud warning line to stderr, continue (do NOT die).
- Parent .gitignore: src/OvermindCrewKit/ entry.
</scope>

<anti-scope>
- CONTEXT.md docs (task 02); install.sh; gemini/antigravity layouts; submodule machinery.
</anti-scope>

<constraints>
- Absolute-path discipline, no pipes eating exit codes (deploy-lesson legend).
- bash -n clean; no new external tool dependencies.
</constraints>

<acceptance-criteria>
- [ ] pack.sh with no --crew-kit resolves and validates the repo kit (path check pass observable without a full build); dirty-kit warning fires on an uncommitted kit tree.
- [ ] .gitignore ignores the embedded clone; parent git status clean.
- [ ] bash -n green on the script.
</acceptance-criteria>

<refs>
- Spec 8a2e649f; plan dccd9ac1; build/pack.sh; build/dist-layout.env; src/OvermindCrewKit/manifest.json.
</refs>

<log>
- 2026-10-04 [agent] — task cut post-validation (human proof 07333013).
</log>