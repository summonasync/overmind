---
feature: 001
task: 01
slug: solution-scaffold
status: todo
review: light
created: 2026-09-06
---

# 001/01 — Solution scaffold

<goal>
A buildable solution skeleton exists: `Overmind.sln` with four source projects and four test projects, central package management, and a green `dotnet build`. No business logic — structure only.
</goal>

<context>
Greenfield repo (currently only docs + .gitignore). Everything in the MVP hangs off this layout. Overmind is a win/mac desktop app: one .NET host process (tray + HTTP API + SPA hosting), a separate stdio MCP adapter console app, and a Next.js UI (scaffolded later, task 07 — only create the empty `web/` directory note here, no npm work).
</context>

<scope>
Create:
- `Overmind.sln`
- `src/Overmind.Core/` — classlib, net10.0. Domain entities + interfaces will live here (task 02). No package references.
- `src/Overmind.Infrastructure/` — classlib, net10.0. References: Core.
- `src/Overmind.Host/` — ASP.NET Core web project, net10.0. References: Core, Infrastructure.
- `src/Overmind.Mcp/` — console app, net10.0. References: none yet (HTTP client only).
- `tests/Overmind.Core.Tests/`, `tests/Overmind.Infrastructure.Tests/`, `tests/Overmind.Api.Tests/`, `tests/Overmind.Mcp.Tests/` — xUnit + Shouldly + NSubstitute, matching references to their SUT projects.
- `Directory.Packages.props` — central versions for: Microsoft.EntityFrameworkCore.Sqlite, Microsoft.ML.OnnxRuntime, Qdrant.Client, ModelContextProtocol (pin a stable version; verify exact latest stable at execution time), Avalonia + Avalonia.Desktop, xunit, Shouldly, NSubstitute, Microsoft.AspNetCore.Mvc.Testing.
- `.editorconfig` — minimal: file-scoped namespaces enforced, no `var` ban (leave default), CRLF/LF = lf.

Each project gets exactly one trivial placeholder file only if the SDK requires it; otherwise leave empty. No Program.cs logic in Host beyond the default template's `WebApplication.CreateBuilder` skeleton that compiles.
</scope>

<anti-scope>
- No domain entities, no DbContext, no endpoints — those are tasks 02/03.
- No Avalonia wiring in Host (task 08).
- No Next.js/npm scaffold (task 07); do not create `web/`.
- No Docker/Qdrant anything (task 05).
- No README changes.
</anti-scope>

<constraints>
- .NET 10 SDK, C# 14, `LangVersion` latest, `Nullable` enable, `ImplicitUsings` enable everywhere.
- Central Package Management via Directory.Packages.props (`ManagePackageVersionsCentrally`).
- Every third-party package must pass nuget-licensing-guard (MIT/Apache-2.0/BSD only; all listed above qualify — verify anyway).
- No comments in code.
</constraints>

<acceptance-criteria>
- [ ] `dotnet build` at repo root succeeds with 0 warnings.
- [ ] `dotnet test` runs (0 tests is fine, but all 4 test projects are discovered and execute).
- [ ] Solution layout matches: `src/Overmind.{Core,Infrastructure,Host,Mcp}`, `tests/Overmind.{Core,Infrastructure,Api,Mcp}.Tests`.
- [ ] `Directory.Packages.props` is the only place with package versions (no `Version=` in csproj files).
</acceptance-criteria>

<refs>
- @../plan.md — task 01
- @../spec.md — R1 (host topology), R8 (test stack)
</refs>

<obstacles>
<!-- Filled by executors during work -->
</obstacles>

<log>
2026-09-06 — writing-plans — task created from approved spec.
</log>
