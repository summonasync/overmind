---
feature: 116
slug: crewkit-in-repo
status: validated
created: 2026-10-04
validated: 2026-10-04
---

# Plan 116 — Crew kit in-repo: pack from repo, sync repo → home

<goal>
Pack from the repo kit, document the one-way flow, keep the parent repo clean.
</goal>

<sequence>
- 01 (coder): pack.sh default + dist-layout.env re-path + dirty-kit warning + .gitignore. Gates: `bash -n`, path-resolution sanity (die-with-message paths), one real pack run linux-x64 at closeout.
- 02 (orchestrator): CONTEXT.md source-of-truth statement + MCP→kit closeout rule. Markdown only.
- Reviews: one light review on task 01; task 02 rides the same review pass (docs). Result after.
</sequence>

<verification>
- pack run linux-x64 succeeds from the new default; kit warning observable on a dirty tree (touch a file in the clone).
- Parent `git status` clean with the embedded clone present.
- No home-dependent behavior left in pack.sh defaults.
</verification>

<refs>
- Spec 8a2e649f; build/pack.sh; build/dist-layout.env; src/OvermindCrewKit/.
</refs>

<log>
- 2026-10-04 [agent] — plan draft, pre-flown.
- 2026-10-04 [agent] — validated with human proof: notification 07333013-b954-4918-abc1-c30567161d00 replied 2026-10-04 18:45:33 UTC
</log>