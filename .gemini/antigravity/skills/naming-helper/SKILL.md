---
name: naming-helper
description: |
  Naming assistant for projects, classes, services, entities, features, tools.
  Calibrates tone with the user first, then delivers a 5-option spectrum from
  ultra-strict corporate to absolutely unhinged — user picks.
type: prompt
whenToUse: |
  When the user needs a name for anything: project, kit, service, class, entity,
  feature, repo, tool, file — and wants options with personality control.
---

# Naming Helper

Names stick. A good name is sayable, googleable, and matches the room it will be spoken in.
This skill exists because "how do I call this thing" deserves 2 minutes of calibration,
not a random pick.

## Step 1 — Calibration (MANDATORY, one question at a time)

Never generate names before asking. Ask via the host's question tool:

1. **What are we naming?** project / repo / service / class / entity / feature / tool / file / other
2. **Formality scale:** `strict` (will be shown to management, clients, compliance) / `neutral` / `spicy` (перчинка допустима) / `unhinged` (разъёб приветствуется)
3. **Language & context:** English-only (code, public OSS) or Russian-flavored allowed (internal tools)? Domain keywords that MUST or MUST NOT appear? Existing naming conventions in the codebase to match?

Skip a question only if the user already answered it in the request.

## Step 2 — Generate the spectrum

Exactly 5 options, ordered by heat:

1. **The Strict One** — super-официоз, survives an enterprise review board. Descriptive, boring, safe.
2. **The Professional** — clean, modern, no jokes. Would pass in any team.
3. **The Balanced** — personality without HR risk. A wink, not a punchline.
4. **The Spicy One** — перчинка: meme-adjacent, dev-slang, still sayable in a standup.
5. **The Unhinged One** — йебейше лютый. Breaks the meter. The user was warned.

For each option: the name + one line of vibe rationale + where it fits (README header, class name, repo name...).
Respect hard constraints from calibration (must-include/must-not-include words, language, codebase conventions).
Names for code artifacts follow the language's conventions (PascalCase for C# types, kebab-case for repos, etc.).

## Step 3 — STOP

Present the spectrum, recommend one (with reason), list alternatives, then STOP per the skill output contract.
The user picks. Never auto-apply a name to anything.

## Rules

- No trademarked names, no existing mega-project collisions (quick sanity: would this name google cleanly?).
- The unhinged option must still be a real name — not gibberish. Funny AND usable.
- If the user laughs at #5 and picks #2 — that is the system working as intended.
- Speak Russian with the user. The names themselves follow the calibration language.
