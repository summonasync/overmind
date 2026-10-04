---
feature: 116
task: 01
slug: crewkit-in-repo
status: done
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
- [x] pack.sh with no --crew-kit resolves and validates the repo kit (path check pass observable without a full build); dirty-kit warning fires on an uncommitted kit tree.
- [x] .gitignore ignores the embedded clone; parent git status clean.
- [x] bash -n green on the script.
</acceptance-criteria>

<refs>
- Spec 8a2e649f; plan dccd9ac1; build/pack.sh; build/dist-layout.env; src/OvermindCrewKit/manifest.json.
</refs>

<log>
- 2026-10-04 [agent] — task cut post-validation (human proof 07333013).
- 2026-10-04 [agent] — [agent] Claimed by Van (claude), taken by orchestrator — dispatching dotnet-coder (sonnet, shell lane).
- 2026-10-04 [agent] — [agent] Obstacles stash (batch-task quirk, no obstacles section): (1) SOCIAL_DICTIONARY source is .kimi-code/SOCIAL_DICTIONARY.md per the kit's own manifest.json — the brief's guess (.claude/…) does not exist in the clone; byte-identical to home today. (2) CREW_EXCLUDES skills/synced kept (bundled-tree path unchanged, no-op in clone, future-proofing). (3) Known home-vs-clone drift: .claude/CLAUDE.md + the two 114-touched SKILL.md differ — the next real pack bundles the REPO versions (114 changes reach home on deploy; user informed). (4) Non-git kit dir via --crew-kit silently skips the dirty warning (best-effort, accepted).
- 2026-10-04 [agent] — [orchestrator] Light review PASS-WITH-NITS (0 blockers, commit ca84975): errexit-конструкция подтверждена (live-пруф на не-git и грязном ките), валидационный порядок до тяжёлых стадий, все 5 CREW_ITEMS существуют, SOCIAL_DICTIONARY байт-идентичен home, бандл-дерево не изменилось (anti-scope holds), .gitignore цел. Nits → microbatch #191 (find|wc под pipefail + hardcoded .claude-пути дублируют CREW_ITEMS) / #192 (не-git --crew-kit молча скипает drift-check — уже accepted obstacle, опциональная else-ветка).
- 2026-10-04 [agent] — [agent] Light review passed (PASS-WITH-NITS, 0 blockers — nits #191/#192 to microbatch ledger), all 3 AC ticked. Code in ca84975. The real linux-x64 pack gate runs at closeout per spec constraint.
</log>