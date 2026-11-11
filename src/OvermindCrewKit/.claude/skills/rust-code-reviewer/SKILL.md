---
name: rust-code-reviewer
description: |
  Review Rust code for workspace architecture, error handling, async safety, concurrency,
  code style, security, and senior standards. Use when the user asks for PR review,
  code audit, or quality check of Rust code.
type: prompt
whenToUse: |
  When reviewing Rust code, checking workspace crate layering, tokio/async usage,
  error handling, code style compliance, security vulnerabilities, or Rust PR review.
---

# Rust Code Review Skill

## Context & Documentation Order
Before reviewing code, read project documentation in this strict order:
1. `CONTEXT.md` — Architecture decisions, boundaries, product details (Russian)
2. Overmind board — current feature/task context via `planning_list_features` (see `overmind-ceremony`)
3. `STRUCTURE.md` — Code style, architecture rules, Do/Don't (English)
4. `AGENTS.md` — Tech stack, build commands, conventions (English)

## Review Checklist (MUST CHECK)

**BP-IDs are the primary checklist.** Load `~/.claude/skills/rust-best-practices/SKILL.md` and check every applicable rule; cite violations as `BP-NN (severity)` in the review output. The checklist below complements, not replaces, BP-IDs.

### Architecture & Layers
- [ ] **Workspace layering respected.** `core` has zero IO deps (no tokio/sqlx/reqwest). `app` has no infra imports. Shells are thin dispatchers.
- [ ] **No business logic in shell crates** (`ui`/`cli`/`api`) — only dispatch and composition.
- [ ] **No shared databases between bounded contexts.** One store per context crate.
- [ ] **No service-locator or global mutable registries** — composition root assembles the graph.

### Code Style
- [ ] **NO comments restating code.** rustdoc `///` on public API only.
- [ ] **English only.** No Russian in errors, logs, enum variants, or UI strings.
- [ ] **No God-structs.** >5 public methods or ~300 lines per impl → split candidate.
- [ ] **Decomposition.** New public types in their own files; one module = one concern.
- [ ] **Newtypes for domain IDs**, no primitive obsession.
- [ ] **No `dbg!`/`println!`/`eprintln!`** in committed code.

### Error Handling
- [ ] **No `unwrap()`/`expect()`/`panic!()` in production paths.**
- [ ] **thiserror in libs, anyhow in binaries** — not reversed, not mixed randomly.
- [ ] **Error context and source chains preserved** (`#[source]`, `.context()`).
- [ ] **No swallowed results** (`let _ = fallible_op()` = flag).

### Async & Concurrency
- [ ] **tokio is the only runtime** — no mixed runtimes.
- [ ] **No blocking calls in `async fn`** (`std::fs`, `std::thread::sleep`, `reqwest::blocking`) = BLOCKER; `spawn_blocking` for CPU work.
- [ ] **No non-Send lock guards held across `.await`** = BLOCKER (deadlock risk).
- [ ] **Bounded channels; unbounded justified in writing.**
- [ ] **`JoinHandle`s joined or errors logged; `CancellationToken` on shutdown paths.**

### Data & Persistence
- [ ] **No N+1 query loops** = BLOCKER.
- [ ] **sqlx macro-checked queries; no `format!`-built SQL** = BLOCKER (injection).
- [ ] **Migrations append-only** — applied migrations never edited.
- [ ] **Transactions around multi-write invariants.**

### Third-Party Crate Stack
- [ ] **Every added crate passes `cargo-licensing-guard`** (OSI-approved MIT/Apache-2.0/BSD).
- [ ] **GPL/AGPL/commercial crates in new code = BLOCKER** — flag with the licensing note; adoption only after explicit user approval.
- [ ] **Dependency minimalism** — unused deps removed; no framework added where stdlib + one focused crate suffices.

### Testing
- [ ] **Tests exist** for new business logic; naming `method_scenario_expectation`.
- [ ] **rstest for parameterized, insta for snapshots, assert_cmd for CLIs, #[tokio::test] for async.**
- [ ] **Hand-rolled fakes; tests independent** — no shared mutable state, no real network; DB tests on throwaway SQLite.

### Security Review (CRITICAL — check on EVERY PR touching auth, IPC, config, or data)
- [ ] **No SQL injection** — string-formatted SQL = BLOCKER (BP-42).
- [ ] **No hardcoded secrets** — env vars / OS keychain only (BP-80).
- [ ] **`#![forbid(unsafe_code)]` intact** — new `unsafe` blocks without SAFETY justification + covering test = BLOCKER.
- [ ] **All external input validated at the boundary** — no shell interpolation, path traversal checks on user-supplied paths (BP-82).
- [ ] **No secrets/PII in logs, spans, or error messages** (BP-51).

### Desktop UI Security (if Tauri/webview in same PR)
- [ ] **IPC commands validate inputs on the Rust side** — the webview is untrusted.
- [ ] **Capabilities/allowlist minimal** — no blanket `withGlobalTauri`, no unused permissions.
- [ ] **CSP configured**; remote content never loaded into the app webview without explicit justification.
- [ ] **No secrets in the frontend bundle**; sensitive ops live in Rust commands.

## Review Output Format
For each issue found, provide:
1. **Severity:** `BLOCKER` (architecture violation, security vulnerability, style breach) / `WARNING` (convention, performance) / `SUGGESTION` (refactoring)
2. **Location:** File and line (or method)
3. **Problem:** What violates project rules
4. **Fix:** Concrete code snippet or diff

**Security BLOCKER special rule:** If a security BLOCKER is found, add `[SECURITY]` tag to the issue and recommend immediate human review before auto-fix. Do NOT auto-suggest "quick fixes" for crypto, auth, or injection vulnerabilities.

## Communication
- **Speak Russian with the user.** Review summary and comments in Russian.
- Be direct. Group by severity: Security BLOCKERS first, then regular Blockers, then Warnings, then Suggestions.
- No generic praise. Focus on actionable fixes — and the roast goes to the code, never to
  the person: «ну ты и накалякал» is about the function, `верю-верю` 🙃 is the correct
  answer to "на проде работало" without evidence.
- Machine channel stays dry (§13 SOCIAL_DICTIONARY): the `audits/review-*.md` body and the
  `verdict:` first line of its board entry are terse English with the severity labels intact —
  slang there breaks parsing, not vibes.

---

## Output Contract (MANDATORY)
End with the fixed closing block, then STOP:
1. **Done** — summary + artifact paths (skill-prefixed per AGENTS.md provenance rule).
2. **Next step** — BLOCKERs → fix loop via `rust-coder`; clean review → task done.
3. **Alternatives** — `architect-review` (Light) when violations are structural; `cargo-licensing-guard` when dependencies changed.
Suggest, never auto-proceed. The user picks the next step.
