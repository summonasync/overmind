---
feature: 006
slug: crew-package-manager
status: draft
priority: p3
version: 1
created: 2026-09-27
---

# Feature 006 — Crew as an installable package: namespace/marker ownership, clean install-update-uninstall

## Idea (user, 2026-09-28)

Make the crew a properly installable / removable / updatable PACKAGE with a recognizable namespace — user's working name «mindslash-set/skill» — so ownership checks in deployed homes become trivial (prefix/marker glob) instead of manifest archaeology.

## Why it is tempting

- One-command install/update/uninstall already exists (manifest.json + install/uninstall scripts), but ownership in homes is implicit: a directory in ~/.qwen/skills has no marker saying «crew-owned».
- The 003 rename exposed the gap: merge-mode homes (qwen) orphan removed crew entries (005 exists only to sweep that by hand).
- A prefix (mindslash-*) would make orphan detection a dumb glob.

## Reviewer's take (2026-09-28, to resolve at the spec gate)

- **Mass rename of 23 agents / 26 skills to mindslash-* is the expensive path**: duplicates 003's churn ×4 runtimes, touches every cross-reference, pollutes every skill listing — for an ownership problem that is already solved by the manifest.
- **Cheaper equivalent**: keep names; make ownership explicit — (a) marker file per deployed dir (e.g. .mindslash-owned) written by the installer, (b) uninstall/mirror logic becomes marker-aware (removes exactly marked entries), (c) add `install.sh --check` that audits homes: orphans, drift, counts. Same ergonomics, zero renames.
- **Update flow** is already `git pull && ./install.sh`; `--check` closes the loop.
- If branding still wanted: decide via naming-helper at the spec gate; note that a namespace prefix on FUTURE entries only creates a mixed namespace — worse than either pure option.

## Non-goals (until spec'd)

- No renames of existing entries as part of this feature without an explicit user decision at the spec gate.

<removed 2026-09-28 — user's original «prefix on everything» formulation parked in favor of the marker-based alternative above; the prefix path is NOT dead, it is gated behind the spec decision.>

## Refs

- Born from 005 (deploy/orphan sweep exposed the ownership gap).
- Blocked by nothing idea-wise; do not start before 004 settles (queue hygiene).