---
name: dotnet-init
description: Initialize, scan, or analyze .NET projects. Use when the user asks to initialize a project, scan codebase, create or update AGENTS.md, set up Clean Architecture structure, or configure a new microservice. Follows .NET 9/10+, MassTransit, Aspire, and strict code style conventions.
type: prompt
whenToUse: When initializing a .NET project, scanning .NET codebase structure, creating AGENTS.md, setting up project structure, or configuring new microservices.
---

# .NET Project Initialization & Analysis Skill

## Context & Documentation Order
Before making ANY changes, read the project documentation in this strict order:
1. `CONTEXT.md` — System design, architecture decisions, service roles, communication rules, AI Framework details, development phases, MCP server design (Russian)
2. Overmind board — features and task state via `planning_list_features` (ceremony rules: `overmind-ceremony`)
3. `STRUCTURE.md` — Project structure, code style, architecture rules, Do/Don't (English)
4. `AGENTS.md` — Tech stack, build commands, security, conventions (English)

If these files do not exist, create them following the user's conventions.

## Tech Stack & Runtime
- **Runtime**: .NET 9/10+ (prefer latest LTS)
- **Language**: C# 13+ with modern features (records, pattern matching, required init, primary constructors, collection expressions)
- **New Microservices**: Start with **Aspire** orchestration by default
- **Message Brokers**: Use **MassTransit** (not raw RabbitMQ/Azure ServiceBus clients)
- **Architecture**: Clean Architecture (Domain -> Application -> Infrastructure -> API). Vertical Slice when appropriate.
- **API**: Minimal APIs or Carter modules. Avoid heavy Controllers.
- **ORM**: EF Core. Migrations in separate assembly if modular monolith.
- **DI**: Microsoft.Extensions.DependencyInjection only (no external containers like Autofac)
- **Validation**: FluentValidation
- **Logging**: Serilog with structured logging
- **Testing**: xUnit + FluentAssertions + NSubstitute

## Code Style (Strict)
- **NO comments in code.** Code must be self-explanatory through naming.
- **All messages/strings in projects are ONLY in English.** No Russian or other languages in UI strings, logs, exceptions, or enums.
- **Avoid God-Services.** Split responsibilities. One class = one reason to change.
- **Split files:** API endpoints, DTOs, and Services/Handlers must be in **separate files**.
- **Clean Architecture on backend.** No business logic in controllers or minimal API endpoints.
- **No `region` directives, no Hungarian notation, no comments for obvious code, no `async void`.**

## Frontend Rules (if applicable)
- If the project uses **Next.js**: `client` components **CANNOT** make direct HTTP requests to .NET or any external backend.
- All requests from the browser must go to the **Next.js backend** first, which then proxies to the .NET API.
- No direct calls from `use client` components to .NET backend.

## Build & Verification
- Build: `dotnet build`
- Test: `dotnet test`
- Format: `dotnet format`
- Restore: `dotnet restore`
- EF Migrations: `dotnet ef migrations add` / `dotnet ef database update`

## AI/ML Context (Learning)
- When discussing ML: reference PyTorch concepts but explain .NET equivalents (TorchSharp, ML.NET, ONNX Runtime)
- For LLM topics: mention tokenization, quantization, LoRA, inference — but keep code examples in C# where possible
- Prefer ONNX Runtime for inference in .NET over calling Python scripts

## Communication
- **Speak Russian with the user.** All explanations, clarifications, and questions must be in Russian.
- Be concise. Skip boilerplate explanations.
- Ask before adding new NuGet packages to production projects.

## When Scanning a Project
1. Identify .NET version from `.csproj` (`<TargetFramework>`)
2. List all NuGet packages and versions
3. Detect Aspire orchestration (AppHost, ServiceDefaults)
4. Detect MassTransit usage and transport (RabbitMQ, Azure ServiceBus, In-Memory)
5. Determine architecture pattern (Clean Arch, Vertical Slice, Modular Monolith)
6. Identify test framework and coverage
7. Check for Central Package Management (`Directory.Packages.props`)
8. Note CI/CD configuration if present
9. Check for Next.js frontend and its proxy setup

## When Creating AGENTS.md
Generate `AGENTS.md` including:
- Project type (Web API, Worker Service, Library, Aspire AppHost)
- .NET version and C# language version
- Architecture pattern and MassTransit transport if used
- Aspire usage yes/no
- Build/test commands
- Key NuGet packages
- Strict code style rules (no comments, English only, no God-Services, split files)
- Frontend rules (Next.js proxy) if applicable
- Documentation file order (CONTEXT.md -> Overmind board -> STRUCTURE.md -> AGENTS.md)

---

## Output Contract (MANDATORY)
End with the fixed closing block, then STOP:
1. **Done** — summary + artifact paths (skill-prefixed per AGENTS.md provenance rule).
2. **Next step** — `task-orchestrator` intake for the first feature, or `dotnet-coder` for direct implementation.
3. **Alternatives** — `documentation-writer` to polish generated docs (README, AGENTS.md); `architect-review` as a structural baseline.
Suggest, never auto-proceed. The user picks the next step.
