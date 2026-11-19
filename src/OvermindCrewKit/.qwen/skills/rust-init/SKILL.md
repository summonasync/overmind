---
name: rust-init
description: Initialize, scan, or analyze Rust projects. Use when the user asks to initialize a project, scan a codebase, create or update AGENTS.md, set up a cargo workspace, or configure a new Rust app/service. Follows edition 2024, tokio/thiserror stack, workspace Clean Architecture, and strict code style conventions.
type: prompt
whenToUse: When initializing a Rust project, scanning Rust codebase structure, creating AGENTS.md, setting up a cargo workspace, or configuring a new Rust app or service.
---

# Rust Project Initialization & Analysis Skill

## Context & Documentation Order
Before making ANY changes, read the project documentation in this strict order:
1. `CONTEXT.md` — System design, architecture decisions, service roles, development phases (Russian)
2. Overmind board — features and task state via `planning_list_features` (ceremony rules: `overmind-ceremony`)
3. `STRUCTURE.md` — Project structure, code style, architecture rules, Do/Don't (English)
4. `AGENTS.md` — Tech stack, build commands, security, conventions (English)

If these files do not exist, create them following the user's conventions.

## Tech Stack & Toolchain
- **Toolchain**: Rust stable, edition 2024, pinned in `rust-toolchain.toml`
- **Architecture**: Clean Architecture via cargo workspace crates (core -> app -> infra -> ui/cli/api shells)
- **Errors**: thiserror (libs) / anyhow (binaries)
- **Async**: tokio as the single runtime
- **Data**: sqlx (compile-time checked); rusqlite only for small embedded sync stores
- **CLI**: clap (derive); **Logging**: tracing + tracing-subscriber
- **Testing**: `#[test]` + rstest + insta + assert_cmd
- **UI stack**: decided per project via ADR (`rust-architect` decision matrix) — never assumed

## Code Style (Strict)
- **NO comments restating code.** `///` rustdoc on public API only.
- **All messages/strings in projects are ONLY in English.**
- **No God-structs.** Split by responsibility. One module = one concern.
- **New public type = new file.** No `types.rs` dumps.
- **No `unwrap()`/`expect()`/`panic!()` in production paths. No `unsafe` by default. No blocking calls in `async fn`.**

## Build & Verification
- Build: `cargo build` / fast check: `cargo check`
- Lint: `cargo clippy --workspace --all-targets -- -D warnings`
- Test: `cargo test`
- Format: `cargo fmt` (verify: `cargo fmt --check`)
- Licenses & advisories: `cargo deny check` / `cargo audit`
- Unused deps: `cargo machete`

## Default Workspace Scaffold
```
<project>/
├── Cargo.toml            # workspace, members, shared deps
├── rust-toolchain.toml   # pinned stable toolchain
├── deny.toml             # cargo-deny: licenses, advisories
├── .github/workflows/ci.yml   # fmt + clippy + test + deny
├── crates/
│   ├── <app>-core/       # domain: entities, value objects, events (zero IO deps)
│   ├── <app>-app/        # use cases, ports (traits)
│   ├── <app>-infra/      # sqlx stores, fs, external APIs
│   └── <app>-cli/        # or -ui / -api shell: clap main, composition root
└── AGENTS.md
```
UI shell crate added only after the stack ADR (Tauri/egui/Slint/Iced — see `rust-architect`).

## When Scanning a Project
1. Read workspace `Cargo.toml`: members, `[workspace.dependencies]`, shared lints
2. Detect toolchain pin (`rust-toolchain.toml`) and edition
3. Map crate graph to the layering rule (core ← app ← infra ← shells); flag violations
4. List all third-party crates and check them against `cargo-licensing-guard`
5. Detect UI framework (Tauri/egui/Slint/Iced/GTK) if present
6. Identify test layout (unit/integration/snapshot) and coverage shape
7. Check `deny.toml`, CI config, clippy configuration
8. Check for `unsafe` (`#![forbid(unsafe_code)]` presence per crate)
9. Note doc files present: CONTEXT.md / STRUCTURE.md / AGENTS.md

## When Creating AGENTS.md
Generate `AGENTS.md` including:
- Project type (desktop app, service, library, CLI; workspace layout)
- Toolchain and edition; UI stack + link to the ADR if chosen
- Architecture pattern and crate boundary rules
- Build/test/lint commands (clippy -D warnings as the gate)
- Key crates and their pinned rules (`cargo-licensing-guard` pointer)
- Strict code style rules (no comments, English only, no God-structs, split files, no unwrap)
- Documentation file order (CONTEXT.md -> Overmind board -> STRUCTURE.md -> AGENTS.md)

## Communication
- **Speak Russian with the user.** All explanations, clarifications, and questions must be in Russian.
- Be concise. Skip boilerplate explanations.
- Ask before adding new crates to production projects (`cargo-licensing-guard` first).

---

## Output Contract (MANDATORY)
End with the fixed closing block, then STOP:
1. **Done** — summary + artifact paths (skill-prefixed per AGENTS.md provenance rule).
2. **Next step** — `feature-orchestrator` intake for the first feature, or `rust-coder` for direct implementation.
3. **Alternatives** — `documentation-writer` to polish generated docs; `architect-review` as a structural baseline; `rust-architect` for the UI stack ADR.
Suggest, never auto-proceed. The user picks the next step.
