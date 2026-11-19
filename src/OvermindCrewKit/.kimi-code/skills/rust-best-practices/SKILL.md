---
name: rust-best-practices
description: |
  Codified Rust engineering rules (BP-IDs) consumed by rust-coder during implementation
  and by rust-code-reviewer as the review checklist. Single source of enforceable code-quality rules.
type: prompt
whenToUse: |
  During any Rust implementation (coder loads it with the task file) and any Rust code review
  (reviewer cites violations by BP-ID).
---

# Rust Best Practices — Codified Rules

Each rule has a stable BP-ID. Reviewer cites violations as `BP-NN (severity)`.
Severity: **[B]** BLOCKER, **[W]** WARNING, **[S]** SUGGESTION.

## Toolchain & Hygiene
- **BP-01 [B]** Stable toolchain, edition 2024, pinned in `rust-toolchain.toml`. No nightly features without an ADR.
- **BP-02 [W]** `cargo fmt --check` clean; `cargo clippy --workspace --all-targets -- -D warnings` green in CI. No bare `#[allow(...)]` — every lint suppression carries a one-line justification (doc comment or ADR reference).
- **BP-03 [W]** Dependency minimalism: stdlib first; ask before adding any new crate (`cargo-licensing-guard` gate). Unused deps removed (`cargo machete` / `udeps`).

## Errors
- **BP-10 [B]** No `unwrap()` / `expect()` / `panic!()` in library and production paths. Tests are exempt; `expect` at startup config where exit-is-the-correct-behavior needs a justification.
- **BP-11 [W]** `thiserror` for typed error enums in domain/lib crates; `anyhow` for binaries/composition roots only. `anyhow` in a library's public API = flag.
- **BP-12 [W]** Errors carry context (`.context()` / `#[error("...")]`) and preserve the source chain (`#[source]`). No stringly-typed errors in library APIs.
- **BP-13 [S]** One error enum per module boundary; match exhaustively at call sites; no swallowed results (`let _ = fallible_op()` = flag).

## Async & Concurrency
- **BP-20 [B]** tokio is the single async runtime. No mixing async-std/smol runtimes in one binary.
- **BP-21 [B]** No blocking calls inside `async fn`: `std::fs`, `std::net`, `std::thread::sleep`, `reqwest::blocking` = BLOCKER. Use tokio APIs or `spawn_blocking` for CPU-heavy work.
- **BP-22 [W]** Graceful shutdown on long-running tasks: `CancellationToken` / `select!`. Spawned task `JoinHandle`s are joined or their errors logged — no silent `let _ = spawn(...)`.
- **BP-23 [W]** Bounded channels (`mpsc::channel(n)`); unbounded only with documented backpressure reasoning.
- **BP-24 [B]** No `std::sync::Mutex` guard (or other non-Send lock) held across `.await`. Use `tokio::sync::Mutex` where a lock must span awaits, or narrow the critical section.

## Typing & API Surface
- **BP-30 [W]** Newtypes for domain IDs and money-like values (`pub struct ProjectId(Uuid)`); no primitive obsession.
- **BP-31 [W]** `pub` is deliberate: small, documented public API per crate; implementation details in private modules; no blanket `pub use` of internals.
- **BP-32 [S]** Builders or `#[non_exhaustive]` for config-like types expected to grow.
- **BP-33 [W]** serde at the boundary: domain types stay serde-free where practical; DTO modules carry `#[serde(rename_all = "camelCase")]` at API/IPC boundaries.

## Data & Persistence
- **BP-40 [B]** No N+1: batch queries or JOINs. A loop issuing queries = automatic BLOCKER.
- **BP-41 [W]** sqlx as the default (compile-time checked `query!`/`query_as!`); rusqlite only for small embedded sync stores. Migrations via `sqlx::migrate!`/refinery; never edit an applied migration — add a new one.
- **BP-42 [B]** No SQL built by string formatting. Parameterized or macro-checked queries only; `format!`-built SQL = BLOCKER (injection).
- **BP-43 [W]** Transactions around multi-write invariants; no check-then-act without a transaction or row-level lock.

## Logging & Observability
- **BP-50 [W]** `tracing` with structured fields (`info!(project_id = %id, "project archived")`). No `println!`/`eprintln!`/`dbg!` in committed code (CLI user output via clap is fine).
- **BP-51 [B]** No secrets, tokens, or PII in logs, spans, or error messages.
- **BP-52 [S]** Right level: error = actionable failure, warn = recovered anomaly, info = business-meaningful event. Debug/trace noise doesn't get committed.

## Testing
- **BP-60 [W]** `#[test]` + `rstest` for parameterized tests; `#[tokio::test]` for async; `insta` for snapshot tests; `assert_cmd` + `predicates` for CLIs. Naming: `method_scenario_expectation`, consistent within the crate.
- **BP-61 [W]** TDD per `overmind-ceremony` TDD scope: domain/business logic gets red-green-refactor; schema/config/DTO code is exempt.
- **BP-62 [W]** Tests independent: no shared mutable state, no order dependence, no real network in unit tests. DB tests hit a throwaway SQLite (tempfile), never dev/prod data.
- **BP-63 [S]** Hand-rolled fakes over `mockall` for new code; mockall allowed in existing suites. One logical assertion per test — multiple checks on one outcome are fine, multiple scenarios are not.
- **BP-64 [S]** `proptest` for parsers/serializers on proven hot spots — measure first, don't gold-plate.

## Performance
- **BP-70 [W]** No `clone()`/`Arc::new` to appease the borrow checker on hot paths without measurement; borrow first, share ownership deliberately.
- **BP-71 [S]** `&str` over `String` in APIs; return iterators over materialized collections where streaming fits.
- **BP-72 [W]** Minimal lock scope and allocation in hot paths; profile before optimizing (no speculative `RefCell`/`Rc` gymnastics).

## Security
- **BP-80 [B]** No secrets in code, config committed to git, or logs. Env vars / OS keychain (`keyring`) only.
- **BP-81 [B]** `#![forbid(unsafe_code)]` is the default in app/domain crates. `unsafe` only in audited infra crates, each block with a SAFETY justification and a test covering the invariant.
- **BP-82 [B]** All external input validated at the boundary; no `Command` with shell-interpolated user strings (arg lists only); file paths from user input checked against traversal.

## Architecture & Style
- **BP-90 [B]** Workspace crate layering: `core` (domain, zero IO deps) ← `app` (use cases) ← `infra` / shells (`ui`, `cli`, `api`). A reverse or skipped-layer reference = BLOCKER. Checkable by cargo-deny or the crate graph itself.
- **BP-91 [W]** No God-structs: an impl over ~300 lines or >5 public methods is a split candidate — reviewer flags, human decides. One module = one concern; prefer `foo.rs` + `foo/` layout over `mod.rs`.
- **BP-92 [W]** DI = composition root: traits at boundaries, constructor injection via struct fields. No service-locator, no global mutable registries (`once_cell`/`static` state only for genuinely global infra like tracing init).
- **BP-93 [S]** No comments restating code; `///` rustdoc on public API carries contracts (invariants, errors, examples) — that's API, not comments. No `#[allow(dead_code)]` left behind; dead code gets deleted, not annotated.
- **BP-94 [B] No unexplained magic.** Integration, IPC, and infrastructure code must be explainable by the responsible developer. "It works but nobody understands how" = BLOCKER — undocumented side effects are exactly how AI-generated integrations blow up in production. If the reviewer can't trace what the code does against external systems, it doesn't pass — document the mechanism in an ADR or simplify until traceable.

## Usage
- **Coder:** load with the task file; rules are constraints, not aspirations.
- **Reviewer:** checklist = this file + task `<acceptance-criteria>` + spec-compliance. Cite as `BP-NN [B/W/S] — file:line — what's wrong — fix diff`.
- New recurring rule discovered during work → propose adding it here (reflect loop), don't patch it silently into one review.

---

## Output Contract (MANDATORY)
End with the fixed closing block, then STOP:
1. **Done** — summary + artifact paths (skill-prefixed per AGENTS.md provenance rule).
2. **Next step** — return control to the calling skill (`rust-coder` / `rust-code-reviewer`).
3. **Alternatives** — propose a new BP-ID to the user when a recurring rule is discovered; stop — reference skill, never runs standalone.
Suggest, never auto-proceed. The user picks the next step.
