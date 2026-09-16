---
feature: 001
task: 01
slug: overmind-mvp
status: done
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
2026-09-06 — SDK check: .NET 10.0.400 installed, `net10.0` kept as specified (no TargetFramework adjustment needed).
2026-09-06 — `dotnet new sln` in .NET 10 defaults to the new `.slnx` format; explicit `--format sln` was required to produce `Overmind.sln`.
2026-09-06 — Console project (Overmind.Mcp) needs explicit `<OutputType>Exe</OutputType>` in its csproj, otherwise top-level statements fail with CS8805.
2026-09-06 — Package version verification: nuget.org flat-container API requires lowercase package IDs (`shouldly`, not `Shouldly`).
2026-09-06 — Latest stable versions pinned at execution time: EFCore.Sqlite 10.0.11, OnnxRuntime 1.29.0, Qdrant.Client 1.19.0, ModelContextProtocol 2.2.0 (stable, not the 2.0.0-rc.2 prerelease), Avalonia/Avalonia.Desktop 12.1.2, xunit 2.9.3, Shouldly 4.3.0 (5.x is preview), NSubstitute 6.2.0, Mvc.Testing 10.0.11. All licenses verified via nuspec on nuget.org: MIT / Apache-2.0 / BSD-3-Clause (OnnxRuntime ships a bundled LICENSE file — confirmed MIT text inside the nupkg).
2026-09-06 — Two packages beyond the task's list were added to Directory.Packages.props out of necessity: Microsoft.NET.Test.Sdk 18.9.0 (MIT) and xunit.runner.visualstudio 3.1.5 (Apache-2.0). Without them `dotnet test` cannot discover/execute test projects at all, which would fail the acceptance criteria. xunit.runner.visualstudio pinned to latest 3.x (4.0.0 targets xUnit v3; we use xunit 2.9.3).
</obstacles>

<log>
2026-09-06 — writing-plans — task created from approved spec.
2026-09-06 — implementer — scaffolded Overmind.sln (8 projects: 4 src + 4 tests), Directory.Build.props (net10.0, LangVersion latest, Nullable+ImplicitUsings enable), Directory.Packages.props (central versions, 12 packages, all license-verified), .editorconfig (lf, file-scoped namespaces as error). Build: 0 warnings, 0 errors. Test: all 4 test projects discovered and executed (0 tests, as expected).
</log>
