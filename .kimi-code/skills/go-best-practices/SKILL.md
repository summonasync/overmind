---
name: go-best-practices
description: |
  Codified Go engineering rules (BP-IDs) consumed by go-coder during implementation and by
  go-code-reviewer as the review checklist. Single source of enforceable code-quality rules for the
  Go lane: modules and tooling, error handling, context and concurrency, package and API design,
  security, structured logging, table-driven testing.
type: prompt
whenToUse: |
  During any Go implementation (coder loads it with the task file)
  and any Go code review (reviewer cites violations by BP-ID).
---

# Go Best Practices — Codified Rules

Each rule has a stable BP-ID. Reviewer cites violations as `BP-NN (severity)`.
Severity: **[B]** BLOCKER, **[W]** WARNING, **[S]** SUGGESTION.
IDs are scoped to this catalog; when citing across lanes, name the catalog first (`node-best-practices BP-10`).

## Modules & Tooling
- **BP-01 [B]** `go.mod` declares the compatibility floor in its `go` line, and the code never uses stdlib packages or language features newer than the floor.
- **BP-02 [B]** `gofmt -l .` is empty and `go vet ./...` is clean. Formatting debates end at gofmt; vet findings are fixed, not suppressed.
- **BP-03 [B]** `go mod tidy` produces zero diff; go.sum is committed and consistent with go.mod. Drift between them = BLOCKER.
- **BP-04 [B]** No `replace` directives pointing at local paths in committed code; a temporary local replace is removed before handoff.
- **BP-05 [W]** Every third-party module is justified against the standard library first and `golang.org/x/*` second; a dependency duplicating stdlib = WARNING.
- **BP-06 [S]** Tool dependencies pinned via go.mod `tool` directives, not ad-hoc `go run module@latest` invocations scattered through scripts and docs.

## Error Handling
- **BP-10 [B]** Every error return is handled: checked, wrapped, or explicitly discarded (`_ = ...`) where failure is provably irrelevant. A discarded error that can corrupt state = BLOCKER.
- **BP-11 [B]** Wrapping uses `%w` and callers match with `errors.Is` / `errors.As`. String comparison on error text = BLOCKER.
- **BP-12 [W]** Sentinel errors for expected cases (`ErrNotFound`), typed error structs where handlers need fields; ad-hoc error strings invented at every call site = WARNING.
- **BP-13 [B]** No `panic` for expected failures in library or service code — I/O, parsing, and user input return errors. `panic` is startup-wiring only.
- **BP-14 [W]** The transport layer maps domain errors to responses in one place; ad-hoc status codes scattered across services = WARNING.

## Context & Concurrency
- **BP-20 [B]** `context.Context` is the first parameter of request-scoped and I/O functions; `context.Background` appears only at process entry.
- **BP-21 [B]** Every external call carries a deadline (`context.WithTimeout` or the per-request deadline); unbounded I/O = BLOCKER.
- **BP-22 [B]** Every goroutine has an owner that stops it (`WaitGroup`, `errgroup`, stop channel); a fire-and-forget goroutine = BLOCKER.
- **BP-23 [B]** Channels are closed by the sender only; shared mutable state is guarded or confined to one goroutine; unsynchronized map access across goroutines = BLOCKER.
- **BP-24 [B]** `go test -race` is clean on concurrent packages; a data race = BLOCKER.
- **BP-25 [W]** Fan-out work uses `errgroup` with first-error cancellation; a plain `WaitGroup` join is acceptable only where errors cannot occur.
- **BP-26 [S]** `context.Context` is never stored in a struct field, except a long-lived worker holding its own derived ctx + cancel.

## Package & API Design
- **BP-30 [B]** Layering holds: api → service → store; a store never imports a service; SQL and query strings never leave store packages.
- **BP-31 [W]** Accept interfaces, return concrete types; interfaces are defined where consumed and stay small (1–2 methods). A mega-interface anticipating implementers = WARNING.
- **BP-32 [W]** Package names are short, lowercase, singular, no underscores; identifiers do not stutter with the package name (`user.UserService` = WARNING).
- **BP-33 [S]** `internal/` is the default home for code; `pkg/` exists only with a real cross-module consumer.
- **BP-34 [S]** Files are named after content (`order_service.go`); a `utils`/`helpers` grab-bag package = WARNING.

## Security
- **BP-40 [B]** SQL is parameterized or query-builder built; string-formatted SQL = BLOCKER.
- **BP-41 [B]** `exec.Command` takes an argument list with no shell interpolation; user input reaching `sh -c` or `cmd /c` = BLOCKER.
- **BP-42 [B]** Secrets come from env or a secret store, parsed once into a typed config at startup; none in code, logs, or error values. A default credential = BLOCKER.
- **BP-43 [W]** User-influenced file paths are validated against traversal (`filepath.Clean` + containment check); missing on a file-serving endpoint = BLOCKER.
- **BP-44 [W]** AuthN/authZ enforced in HTTP middleware, not re-implemented per handler; token checks cover signature, expiry, audience/issuer.
- **BP-45 [W]** Error responses leak no internals: no stack traces, file paths, or SQL text to clients.

## Logging & Observability
- **BP-50 [B]** `log/slog` with the JSON handler to stdout is the service logger; `fmt.Println` or `log.Printf` as service logging = BLOCKER.
- **BP-51 [W]** Messages are constants with key-value attrs; `fmt.Sprintf` interpolation inside log messages = WARNING.
- **BP-52 [W]** A derived logger per request carries correlation ids from ctx; request ids survive across goroutines.
- **BP-53 [S]** Log at boundaries and decisions, not inside tight loops.

## Testing
- **BP-60 [B]** Table-driven tests with `t.Run` subtests and meaningful assertions. An assertion-free test, or one that can never fail = BLOCKER.
- **BP-61 [W]** HTTP tested via `httptest.NewRecorder` / `httptest.Server`; real network ports or external services in unit tests = WARNING.
- **BP-62 [W]** Fakes at the store seam are small hand-written types; no `time.Sleep` — channels, `WaitGroup`, or deadline-bounded polling.
- **BP-63 [S]** `t.Parallel()` on independent tests; `t.Cleanup` for teardown.
- **BP-64 [S]** Every error path and rule branch has a test; coverage is a symptom, assertions are the point.

## Usage
- **Coder:** load with the task file; rules are constraints, not aspirations.
- **Reviewer:** checklist = this file + task `<acceptance-criteria>` + spec-compliance. Cite as `BP-NN [B/W/S] — file:line — what's wrong — fix diff`.
- New recurring rule discovered during work → propose adding it here (reflect loop), don't patch it silently into one review.

---

## Output Contract (MANDATORY)
End with the fixed closing block, then STOP:
1. **Done** — summary + artifact paths (skill-prefixed per AGENTS.md provenance rule).
2. **Next step** — return control to the calling skill (`go-coder` / `go-code-reviewer`).
3. **Alternatives** — propose a new BP-ID to the user when a recurring rule is discovered; stop — reference skill, never runs standalone.
Suggest, never auto-proceed. The user picks the next step.
