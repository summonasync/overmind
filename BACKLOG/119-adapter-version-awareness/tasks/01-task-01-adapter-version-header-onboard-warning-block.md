---
feature: 119
task: 01
slug: adapter-version-awareness
status: done
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
- [x] Header on every host call from the adapter.
- [x] Onboard block: mismatch warns, missing warns with pre-dates phrasing, match silent — pinned.
- [x] Build + affected suites green.
</acceptance-criteria>

<refs>
- Spec 1b62e29e; plan cc299ef1; Program.cs (RegisterAsync, CreateHttpClient stamp); OnboardApi/OnboardContracts; incident 2026-10-04.
</refs>

<log>
- 2026-10-04 [agent] — task cut post-validation (human proof 8c9c0ee7).
- 2026-10-05 [agent] — taken by orchestrator — dispatching dotnet-coder (sonnet) for adapter version header + onboard warning (Simple path, one task)
- 2026-10-05 [agent] — coded: adapter stamps X-Overmind-Adapter-Version (AdapterVersion.Current, assembly informational) in AdapterSessionHandler — same single pipeline point as X-Overmind-Session, unconditional; Program.cs ServerInfo now reuses AdapterVersion.Current (one version source). Host: AdapterVersionHeader.Read + AdapterVersionCheck (pinned Mismatch/Predates phrases, HostVersion = host assembly informational); onboard payload gains OnboardAdapterBlock(adapterVersion, hostVersion, warning) — mismatch → «tools may be missing…», missing/blank header → «predates version reporting…», match → silent. Scope-plus (logged for review): the adapter's overmind_onboard tool renders the block — without it the warning never reaches the LLM (Format() is the only agent-visible channel; a stale adapter can't render what predates it, but this build's adapters will warn on future host drift — the incident-2026-10-04 scenario). Tool description extended + pinned. Test fix: AdapterVersionHeaderTests mutated static AdapterSession.Id in parallel with SessionLifecycleTests → flake; both classes now share the AdapterSessionState xUnit collection (sequential). Gates: dotnet build 0w/0e; Mcp.Tests 494/494; Api.Tests 661/661; dotnet format --verify-no-changes clean on all four touched projects.
- 2026-10-05 [agent] — coding round complete — build 0w/0e, Mcp.Tests 494/494, Api.Tests 661/661, format clean; 3/3 AC ticked; handing to reviewer (light) per plan.
- 2026-10-05 [agent] — closeout complete: review PASS-WITH-NITS 0 blockers (audits/review-119-01.md — single stamp point, scope-plus render accepted with early-exits, version-comparison verified on binaries, race closed via shared collection), quickstart nit closed in-tree (Api 661 re-green, format OK), result ab6f738d drafted with click-check. Feature goes to review; done-flip waits on the human tap.
</log>