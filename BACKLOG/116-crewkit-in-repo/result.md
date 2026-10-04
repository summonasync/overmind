---
feature: 116
slug: crewkit-in-repo
status: draft
created: 2026-10-04
---

# Result 116 — Crew kit in-repo: pack from repo, sync repo → home

## What shipped

- **pack.sh** stages the crew kit from the in-repo clone `src/OvermindCrewKit` by default (`--crew-kit` overrides; hardcoded home default removed, `repo_root` resolved before the default applies). Flow is one-way: **repo → bundle → home**.
- **dist-layout.env**: `CREW_ITEMS` re-pathed to the clone layout (`.claude/agents`, `.claude/skills`, `.claude/CLAUDE.md`, `.claude/SKILLFLOW.md`, `.kimi-code/SOCIAL_DICTIONARY.md` — the dictionary's real source per the kit's own manifest; the brief's `.claude/…` guess does not exist in the clone). Bundle basenames unchanged — the shipped content is identical.
- **Dirty-kit warning**: uncommitted changes in the clone → loud stderr warning, pack continues (the kit repo is pushed on the user's rhythm).
- **.gitignore**: `src/OvermindCrewKit/` — parent status clean around the embedded clone.
- **CONTEXT.md**: «Crew kit source of truth (116)» bullet in Distribution — one-way flow, clone-layout items, the **MCP→kit closeout discipline** (feature touching MCP surface lands its kit docs sync in the same wave; deliberately not auto-copying texts — 099-W1/094-W1 drift precedents).

## Verification

- Coder dry-check: default resolves to the clone, all 5 items exist, 43 SKILL.md, bundle basenames identical to the old flat layout; negative probes (bogus rid dies at the right gate, missing --crew-kit path dies, non-git kit skips warning silently — accepted obstacle).
- Reviewer live proofs: errexit construction verified on dirty and non-git kits, validation order before heavy stages, byte-identical SOCIAL_DICTIONARY/SKILLFLOW vs home, .gitignore targeting confirmed.
- **Final honest gate (spec constraint): a real `bash build/pack.sh --version 0.6.1 --bundle-version 9 --rids linux-x64` from the repo default — EXIT 0, 111M archive; the bundled `crew/skills/overmind-ceremony/SKILL.md` is the REPO version (Claims workflow section present), manifest `bundleVersion: 9`.**
- Known drift (expected, this feature fixes it): home currently differs from the clone on `.claude/CLAUDE.md` and the two 114-touched SKILL.md — the next real deploy unpacks this bundle and the repo versions land in home.

## Review

Task 01 light PASS-WITH-NITS, 0 blockers (nits → microbatch #191 find|wc under pipefail + hardcoded .claude paths duplicating CREW_ITEMS; #192 non-git --crew-kit silent skip — accepted obstacle, optional else-branch). Task 02 orchestrator-executed markdown, reviewed in the same pass.

## Files

- build/pack.sh, build/dist-layout.env, .gitignore, CONTEXT.md
- (ca84975 task 01, fa5f94c task 02)

<log>
- 2026-10-04 [agent] — both tasks done (01 light review PASS-WITH-NITS, 02 orchestrator-executed); final real-pack gate EXIT 0 with repo-version kit verified inside the archive; result drafted.
</log>