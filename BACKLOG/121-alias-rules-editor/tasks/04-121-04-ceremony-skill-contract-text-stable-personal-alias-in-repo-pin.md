---
feature: 121
task: 04
slug: alias-rules-editor
status: done
review: light
created: 2026-10-06
brief: Pin in-repo contract text for the ceremony-skill stable-alias instruction; out-of-repo skill sync is the human's act at result validation
---

# 121/04 — Ceremony skill contract text: stable personal alias (in-repo pin)

<goal>
The exact instruction text that teaches agents to pick a STABLE personal alias per the naming rules is pinned in-repo (this artifact), ready for the human to sync into the ceremony skill file at result validation. Agents never edit out-of-repo files.
</goal>

<context>
Plan slice 04 originally pinned the skill file /home/johan-boolman/.claude/skills/overmind-ceremony/SKILL.md (outside the repo) as the edit target; the coordinator reframed it 2026-10-06: the deliverable is the in-repo contract text, the skill-file sync is the human's act at result validation. The plan body is frozen — this task carries the correction.
</context>

<scope>
- CONTRACT TEXT (verbatim, to be inserted into the claim bullet block of the ceremony skill, ~lines 81-86):
  "Your alias is your own STABLE personal name — pick it per the project's naming rules from the agentIdentity block of the overmind_onboard payload, and reuse the SAME alias in every session so board history stays attributable. Vendor is your runtime (claude, qwen, …); two agents on the same runtime must not look identical — differentiate by alias. The defaults (agent / unknown) are placeholders: replace both before claiming."
- Read-only verification: open /home/johan-boolman/.claude/skills/overmind-ceremony/SKILL.md, confirm the claim bullet block (lines 81-86) context the text will land in, log the fit.
</scope>

<anti-scope>
- NO writes to ~/.claude/** — out of repo, human-only at result validation.
- No repo code changes; no installer managed block (OnboardManagedBlock.cs) changes.
</anti-scope>

<constraints>
- Wording must keep: stable across sessions; per project naming rules from agentIdentity; vendor = runtime; same-runtime agents differ by alias; defaults are placeholders.
- Style: plain prose matching the surrounding bullets of the skill; English, no slang.
</constraints>

<acceptance-criteria>
- [x] Contract text pinned verbatim in this artifact (scope section).
- [x] Read-only fit check against the skill's claim bullet block logged.
- [x] No out-of-repo writes attempted.
</acceptance-criteria>

<refs>
- Spec: dc030eba-1f10-4bf2-a68f-f9fea8534b97; Plan: ee02bacf-1651-4e60-9b16-a5f672647fa8 (slice 04).
- /home/johan-boolman/.claude/skills/overmind-ceremony/SKILL.md (READ-ONLY; claim bullets :81-86).
- Host source of the rules text: src/Overmind.Host/Api/OnboardApi.cs:107-110 (agentIdentity block).
</refs>

<obstacles>
- Plan slice 04 pinned the out-of-repo skill file as the edit target — corrected by the coordinator 2026-10-06: in-repo contract text is the deliverable, the skill sync is the human's act at result validation.
- The .gemini/antigravity port still carries the legacy "Pin the feature (`planning_set_current`)" bullet — the Claims workflow (099) section was never synced there. The alias block landed after the legacy bullet in its Ceremony Loop Rules; refreshing that port to claims terminology is a separate quad-sync task, out of 121/04 scope.
- The kit working tree carried pre-existing uncommitted edits (109/110-era hunks in .claude ceremony SKILL.md + a .claude feature-orchestrator SKILL.md edit) from earlier rounds. They are NOT part of this task: the commit stages only the four alias hunks (selective index staging via git apply --cached); the pre-existing edits remain unstaged in the tree.
</obstacles>

<log>
- 2026-10-06 [agent] — task created from validated plan slice 04 with the coordinator's in-repo reframing (review: light).
- 2026-10-06 [agent] — 121/04 contract-text round started
- 2026-10-06 [agent] — Contract text inserted into all four in-repo ceremony ports (src/OvermindCrewKit: .claude, .qwen, .gemini/antigravity, .kimi-code) — after the claim bullet in the Ceremony Loop Rules, before the STOP bullet. Inserted blocks md5-identical: 736ed27955411e0fb95c477c31f154d5, one occurrence per file. Read-only fit check logged: deployed /home/johan-boolman/.claude/skills/overmind-ceremony/SKILL.md read (no write) — claim bullet block sits at :81-86 there; the block lands right after it, same slot as the in-repo ports. Kit commit 3c67637 (4 files, +20). No out-of-repo writes; claim-flow code untouched.
- 2026-10-06 [agent] — contract-text round complete — light review gate
- 2026-10-06 [agent] — light review APPROVED — 0 blockers. Verbatim byte-for-byte across all 4 ports (normalized cmp), md5 recomputed match, placement correct (gemini lag judged acceptable, self-resolves on quad-sync), commit hygiene clean (only 4 SKILL.md files, foreign hunks excluded). Kit commit 3c67637. Gemini-port lag filed to ledger #265.
</log>