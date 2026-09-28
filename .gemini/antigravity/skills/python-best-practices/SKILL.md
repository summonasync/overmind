---
name: python-best-practices
description: |
  Codified Python engineering rules (BP-IDs) consumed by python-coder during implementation and by
  python-code-reviewer as the review checklist. Single source of enforceable code-quality rules for the
  Python lane: uv/poetry packaging, ruff, mypy strict, asyncio and error handling, security, structured
  logging, pytest, ML/DL code discipline.
type: prompt
whenToUse: |
  During any Python implementation (coder loads it with the task file)
  and any Python code review (reviewer cites violations by BP-ID).
---

# Python Best Practices — Codified Rules

Each rule has a stable BP-ID. Reviewer cites violations as `BP-NN (severity)`.
Severity: **[B]** BLOCKER, **[W]** WARNING, **[S]** SUGGESTION.
IDs are scoped to this catalog; when citing across lanes, name the catalog first (`node-best-practices BP-10`).

## Tooling & Packaging
- **BP-01 [B]** uv-managed project with a committed `uv.lock`; `pyproject.toml` (PEP 621) is the single source of truth. A repo where `setup.py` or `requirements.txt` drifts against pyproject = BLOCKER. Poetry is respected where it pre-exists, never introduced fresh.
- **BP-02 [B]** `ruff check` and `ruff format --check` clean, config under `[tool.ruff]` in pyproject.toml. Inline suppressions cite the exact code (`# noqa: E501`); a blanket per-file ignore without justification = WARNING.
- **BP-03 [B]** mypy runs with `strict = true` and is clean. A bare `# type: ignore` (no error code) = BLOCKER.
- **BP-04 [W]** Applications pin everything through the lockfile; published libraries declare minimum bounds only. A floating `*`/`latest` pin in an app = WARNING.
- **BP-05 [B]** `requires-python` floor declared, and the code never uses syntax newer than the floor; CI runs the declared matrix.
- **BP-06 [S]** Tool config lives in pyproject.toml (ruff, mypy, pytest) — no parallel `setup.cfg` / `.flake8` duplicates.

## Typing Discipline
- **BP-10 [B]** Public callables fully annotated. A missing annotation on a boundary function = BLOCKER, inside a private helper = WARNING.
- **BP-11 [B]** Boundary data parsed with pydantic models — HTTP bodies, env, queue messages, external API responses. A `dict[str, Any]` crossing into business logic = BLOCKER.
- **BP-12 [W]** `X | None` and builtin generics; `typing.Optional`/`typing.List` in new code = WARNING.
- **BP-13 [S]** TypedDict, Protocol, or dataclass over plain dicts for internal contracts; composition over inheritance.

## Asyncio & Error Handling
- **BP-20 [B]** No blocking calls inside `async def` — `time.sleep`, `requests`, sync drivers block the loop. Use `asyncio.sleep`, an async client, or `asyncio.to_thread`. Blocking the loop = BLOCKER.
- **BP-21 [B]** No bare `except:` and no `except Exception: pass`. A broad catch logs and decides: retry, requeue, fail. Re-raises keep the chain (`raise ... from err`).
- **BP-22 [W]** Every external await carries a timeout (`asyncio.timeout`); unbounded awaits on third-party I/O = WARNING.
- **BP-23 [W]** Concurrency via `asyncio.TaskGroup`; fire-and-forget tasks are held by reference or grouped. Orphaned tasks = WARNING.
- **BP-24 [B]** A package-level exception hierarchy exists; services raise domain errors and the transport maps them in one place. `HTTPException` inside services = WARNING, ad-hoc status codes scattered across services = WARNING.

## Security
- **BP-30 [B]** SQL is parameterized or built via a query builder/ORM. f-string or `%`-formatted SQL = BLOCKER.
- **BP-31 [B]** No `eval`, `exec`, `pickle.loads`, or `yaml.load` without `SafeLoader` on untrusted input = BLOCKER.
- **BP-32 [B]** `subprocess` takes an argument list with `shell=False`. `shell=True` with interpolated input = BLOCKER.
- **BP-33 [B]** Secrets come from env or a secret store, parsed once via pydantic-settings; none in code, logs, or error messages. A default credential = BLOCKER.
- **BP-34 [W]** Paths via pathlib; user-influenced paths validated against traversal before touching the filesystem.
- **BP-35 [W]** AuthN/authZ enforced at the framework dependency layer; public endpoints carry body limits and rate limiting (or a documented edge equivalent).

## Logging & Observability
- **BP-40 [B]** Structured logging via structlog (or the repo's logger) with key-value fields to stdout. `print` used as service logging = BLOCKER.
- **BP-41 [W]** No f-string interpolation inside message templates — attach fields (`log.info("order_accepted", order_id=oid)`).
- **BP-42 [S]** Log at boundaries and decisions, not inside tight loops; correlation ids propagate through the request/job context.

## Testing
- **BP-50 [B]** pytest with meaningful assertions. An assertion-free test or one that can never fail = BLOCKER.
- **BP-51 [W]** Fixtures over setup/teardown; `parametrize` over copy-pasted branches; test names state behavior (`test_returns_422_when_email_missing`).
- **BP-52 [W]** No sleeps — event-driven waits, freezegun for time, pytest-asyncio for coroutines.
- **BP-53 [W]** Unit tests use fakes at repository seams; no network, no real database. Integration tests marked and isolated.
- **BP-54 [S]** Every error path and rule branch has a test; coverage is a symptom, assertions are the point.

## ML/DL Code (PyTorch lane)
- **BP-60 [W]** Seeds set for every stochastic source (python, numpy, torch, cuda) from one place in config when results must be reproducible.
- **BP-61 [W]** Device-agnostic code: `torch.device` resolved once, tensors moved at the boundary — no hard-coded `"cuda"` literals.
- **BP-62 [B]** Training loops checkpoint model + optimizer + step and can resume; a long run that restarts from zero after a crash = BLOCKER.
- **BP-63 [S]** Inference leaving Python goes through a pinned-opset `torch.onnx.export` verified against torch output on a golden sample; the .NET side consumes it with ONNX Runtime — Python stays out of the serving path.

## Usage
- **Coder:** load with the task file; rules are constraints, not aspirations.
- **Reviewer:** checklist = this file + task `<acceptance-criteria>` + spec-compliance. Cite as `BP-NN [B/W/S] — file:line — what's wrong — fix diff`.
- New recurring rule discovered during work → propose adding it here (reflect loop), don't patch it silently into one review.

---

## Output Contract (MANDATORY)
End with the fixed closing block, then STOP:
1. **Done** — summary + artifact paths (skill-prefixed per AGENTS.md provenance rule).
2. **Next step** — return control to the calling skill (`python-coder` / `python-code-reviewer`).
3. **Alternatives** — propose a new BP-ID to the user when a recurring rule is discovered; stop — reference skill, never runs standalone.
Suggest, never auto-proceed. The user picks the next step.
