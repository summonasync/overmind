---
name: node-best-practices
description: |
  Codified Node.js backend engineering rules (BP-IDs) consumed by node-coder during implementation
  and by node-code-reviewer as the review checklist. Single source of enforceable code-quality
  rules for the standalone Node lane: async and error handling, security, config, logging.
type: prompt
whenToUse: |
  During any standalone Node backend implementation (coder loads it with the task file)
  and any Node code review (reviewer cites violations by BP-ID).
---

# Node Best Practices — Codified Rules

Each rule has a stable BP-ID. Reviewer cites violations as `BP-NN (severity)`.
Severity: **[B]** BLOCKER, **[W]** WARNING, **[S]** SUGGESTION.
IDs are scoped to this catalog; when citing across lanes, name the catalog first (`web-best-practices BP-31`).

## Async & Errors
- **BP-01 [B]** No floating promises: every promise awaited or explicitly handled with a logged `.catch`. `void someAsync()` to silence the checker = BLOCKER.
- **BP-02 [B]** Empty `catch {}` = broken code. A catch rethrows enriched or logs the error object and decides: retry, requeue, or fail.
- **BP-03 [W]** `AbortSignal` on every async API doing I/O; timeouts via `AbortSignal.timeout(ms)`; the signal propagates to `fetch` and DB drivers. Unbounded awaits on third-party calls = WARNING.
- **BP-04 [W]** Typed domain errors thrown by services, mapped to transport responses at the edge. HTTP status codes scattered through business logic = WARNING.
- **BP-05 [W]** Graceful shutdown on SIGTERM/SIGINT: stop accepting → drain in-flight work with a timeout → close DB/queue connections → exit. Missing on a long-running process = BLOCKER.
- **BP-06 [S]** `unhandledRejection` / `uncaughtException` log and exit — never keep serving from a corrupt state.

## Security
- **BP-10 [B]** All queries and commands parameterized. String-concatenated SQL/Cypher/shell, `eval`, `new Function`, or user input interpolated into paths/URLs = BLOCKER.
- **BP-11 [B]** No secrets in code, config in git, or logs. Env validated at boot; log serializers must not print tokens, keys, or credentials.
- **BP-12 [B]** Password hashing via argon2 or bcrypt — md5/SHA for password storage = BLOCKER.
- **BP-13 [W]** AuthN/authZ enforced in middleware/plugins at the route layer, not re-implemented per handler. JWT verification checks signature, expiry, audience/issuer.
- **BP-14 [W]** Error responses carry no stack traces or internal details in production.
- **BP-15 [W]** Public endpoints carry body size limits, rate limiting, and a sane CORS policy — or a documented edge-layer equivalent.

## Config & Secrets
- **BP-20 [B]** `process.env` parsed once at boot with zod; a missing required variable crashes startup before a port binds. Reading env mid-request = BLOCKER.
- **BP-21 [B]** No config reads outside the config module — services receive typed config objects.
- **BP-22 [W]** A secret default value ("dev-only fallback" left in) = BLOCKER; the app fails fast without a real value instead.
- **BP-23 [S]** Config shape mirrors deployment: one schema, environment-specific values only — no code branches on `NODE_ENV` scattered through services.

## Logging
- **BP-30 [B]** No `console.*`. Structured JSON logging via pino (or the repo's logger); child loggers per request/job.
- **BP-31 [B]** No PII, tokens, credentials, or connection strings in logs — redaction at the serializer level.
- **BP-32 [W]** No string interpolation in message templates: `log.info({ orderId }, "order accepted")`, never `` log.info(`order ${orderId}`) ``.
- **BP-33 [S]** Levels mean something: error = actionable failure, warn = recovered anomaly, info = business event. Debug spam does not get committed.

## Usage
- **Coder:** load with the task file; rules are constraints, not aspirations.
- **Reviewer:** checklist = this file + task `<acceptance-criteria>` + spec-compliance. Cite as `BP-NN [B/W/S] — file:line — what's wrong — fix diff`.
- New recurring rule discovered during work → propose adding it here (reflect loop), don't patch it silently into one review.

---

## Output Contract (MANDATORY)
End with the fixed closing block, then STOP:
1. **Done** — summary + artifact paths (skill-prefixed per AGENTS.md provenance rule).
2. **Next step** — return control to the calling skill (`node-coder` / `node-code-reviewer`).
3. **Alternatives** — propose a new BP-ID to the user when a recurring rule is discovered; stop — reference skill, never runs standalone.
Suggest, never auto-proceed. The user picks the next step.
