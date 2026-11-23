---
name: release-docs-sync
description: |
  Release docs sync: after anything ships to GitHub (release deployed, version bumped,
  feature batch merged) — update the shipped repo's README, refresh the summonasync.dev
  product data (PROJECTS.md rows, VERIFY-grounded, honest trade-offs), and publish a /log
  devlog entry over the Umbraco Management API. Use when the user asks to update the
  README/site after a release, or right after a deploy lands.
type: prompt
---

# Release Docs Sync — three surfaces, one pass

Every release touches three public surfaces. Missing any one of them is a stale-docs bug:
the repo README, the site's product data, and the site's devlog. This skill walks all three
in order and stops at the end with the fixed output contract.

## When to run

- A release just deployed (bundle cut, PRs merged into the clean history, version bumped).
- A merged feature batch changed product facts (new feature, changed trade-off, new platform).
- The user asks: «обнови ридми/сайт», "update the README/site", "задокументируй релиз".

## Surface 1 — the released repo's README

- Read the repo's `README.md` first; touch only what the release made stale (features,
  commands, platform lists, version references). Never restructure or re-style it.
- English only, no comments-as-changelog: the README sells what IS, not what happened.
- If the repo has its own docs conventions (AGENTS.md), they win.

## Surface 2 — the site's product data

- Source of truth: `/mnt/Storage/Projects/Websites/summonasync/PROJECTS.md` (renders the
  `/kit` page and the home cards). Update the rows the release changed: features, honest
  trade-offs (плюсы/минусы), spec numbers.
- Grounding rule is the site's brand promise ("we admit limits"): only shipped, verified
  facts. Anything inferred or stale stays marked **VERIFY** until confirmed against the live
  repo — or gets dropped. Never invent a spec.
- The site repo is otherwise read-only for this skill; do not refactor its copy, styles or
  views. Its own AGENTS.md rules win inside that repo.

## Surface 3 — the /log devlog entry

- One entry per release: what shipped, honest trade-offs, version and date. Newest-first is
  the site's own order; never renumber or reorder existing entries.
- Publishing goes through the **agent publishing path** — read
  `/mnt/Storage/Projects/Websites/summonasync/docs/publishing-path.md` before the first call
  of a session: Management API over `client_credentials`, draft → publish two-step.
- Credentials are environment-only (`SUMMON_API_BASE`, `SUMMON_AGENT_CLIENT_ID`,
  `SUMMON_AGENT_CLIENT_SECRET`). Never read, print or persist secret values; never write
  them into files, logs or the entry content.
- If the environment or the API is unavailable, deliver the entry as a markdown draft under
  `audits/release-log-<version>.md` in the released repo and say the publish step is pending.

## Hard rules

- **Clean-history policy:** no personal paths, emails, machine names or owner-identifying
  details in any of the three surfaces. The owner re-creates repos to scrub exactly this —
  do not reintroduce it through the front door.
- No invented claims, no marketing superlatives: honest trade-offs are the product.
- Board linkage: if the release rode board features, link the feature numbers in the log
  entry body (plain `#NNN` references are enough).
- Tombstones rule: never resurrect a removed claim from PROJECTS.md history — if it was
  `<removed ...>`, it is dead.

## Output contract

Per SKILLFLOW.md: **Done** (what was updated on each of the three surfaces + paths),
**Next step** (recommended follow-up), **Alternatives** (2–3).
