---
feature: 119
task: 01
slug: adapter-version-awareness
status: todo
review: light
created: 2026-10-04
brief: Adapter version header + onboard stale-adapter warning (Simple path)
---

# Task 01 — Adapter version header + onboard warning block

<goal>
A stale adapter identifies itself on every call; the onboard response warns loudly when adapter and host versions diverge.
</goal>

<scope>
- Adapter: X-Overmind-Adapter-Version header (assembly informational version) stamped at the same single point as X-Overmind-Session (CreateHttpClient, 099 pattern).
- Host: onboard response gains the adapter block — adapterVersion (request header, null when absent), hostVersion (host assembly), warning line on mismatch («tools may be missing from your toolset; restart your MCP session») and on missing header («adapter predates version reporting; restart recommended"); match → no warning.
- Tests: header presence/format (adapter side), three host paths (mismatch / missing / match), pins on warning phrasing.
</scope>

<anti-scope>
- No persistence/migration; no tool-drift enumeration; no forced disconnects; claim semantics untouched.
</anti-scope>

<acceptance-criteria>
- [ ] Header on every host call from the adapter.
- [ ] Onboard block: mismatch warns, missing warns with pre-dates phrasing, match silent — pinned.
- [ ] Build + affected suites green.
</acceptance-criteria>

<refs>
- Spec 1b62e29e; plan cc299ef1; Program.cs (RegisterAsync, CreateHttpClient stamp); OnboardApi/OnboardContracts; incident 2026-10-04.
</refs>

<log>
- 2026-10-04 [agent] — task cut post-validation (human proof 8c9c0ee7).
</log>