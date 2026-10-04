---
feature: 119
slug: adapter-version-awareness
status: draft
created: 2026-10-04
---

# Plan 119 — Adapter version awareness: onboard reports stale adapter + tool drift

<goal>
One vertical slice: adapter header → host detection → onboard warning block, fully pinned.
</goal>

<sequence>
- 01 (dotnet coder): X-Overmind-Adapter-Version header at the 099 stamp point; onboard adapter block (adapterVersion/hostVersion + mismatch & missing-header warnings); test matrix (header format, three warning paths); pins on warning phrasing.
- Review: light. Result after.
</sequence>

<verification>
- dotnet build + Api/Mcp suites green.
- Live sanity: run a fresh adapter against a scratch host — onboard shows matching versions silently; tamper the header → warning fires.
</verification>

<refs>
- Spec 1b62e29e; Program.cs (RegisterAsync, stamp point); OnboardApi.cs / OnboardContracts.cs.
</refs>

<log>
- 2026-10-04 [agent] — plan draft (1 task).
</log>