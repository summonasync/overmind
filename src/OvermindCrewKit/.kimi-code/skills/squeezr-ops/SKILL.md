---
name: squeezr-ops
description: Set up and operate squeezr, a local hybrid (exact + semantic) code index served to agents over MCP - install the CLI, start the Qdrant/Ollama services, init and index a workspace, register the MCP tools, then query code with code_search, repo_map, expand_span, index_status instead of reading whole files. Use when asked to use or install squeezr, bring up / raise the code index, onboard onto a repository, or do token-cheap code search.
---

# Squeezr — agent onboarding and code search

## 0. Principle: only on explicit request

Install or reconfigure squeezr ONLY after the human explicitly asked for it ("use
squeezr here", "set up the index"). Never onboard a machine proactively and never
install it as a side effect of another task. Sections 1–4 run only in that case.
Section 5 applies whenever the squeezr MCP tools are already registered.

## 1. Install (linux-x64)

Check first; if this prints a version, skip to section 2:

```bash
squeezr --version
```

If missing, install the latest published release (no sudo, user dirs only):

```bash
curl -fsSL http://192.168.1.69:3000/Bob-home-ai/Squeezr/raw/main/scripts/install-release.sh | bash
```

The script resolves the latest Gitea release, unpacks into `~/apps/squeezr/<tag>/`
and repoints the `~/.local/bin/squeezr` symlink, then prints the installed version.
If `~/.local/bin` is not on PATH, add it to the shell profile and open a new terminal
before re-checking. Pin or roll back with `--version <tag>` (e.g. `v1.0.0`); force a
re-download of an installed tag with `--force`. The script is the single source of
install truth — install evolution changes the script, never this skill.

## 2. Services (Qdrant + Ollama)

```bash
squeezr services up
```

Starts the managed docker compose stack (Qdrant + Ollama); the first run pulls
~3.6 GB of images. On success it prints the endpoints — Qdrant has two ports,
only one of them is for squeezr:

- Qdrant gRPC `http://localhost:6334` — what `qdrant.endpoint` must point at
- Qdrant REST `http://localhost:6333` — dashboard/API only, never for squeezr
- Ollama `http://localhost:11434`

The embedding model needs no manual step: the default onnx model ships inside the
release archive, and the ollama provider pulls its model automatically on first
`squeezr index`. New workspaces default to `embeddings.provider: "auto"`: every
indexing run picks ollama when the endpoint is reachable (GPU-accelerated only
when Ollama itself has a GPU — on CPU-only machines both paths embed on CPU, the
choice is process isolation, not speed), otherwise the bundled onnx model. Pin
the provider in workspace config to force one path.

If docker is missing or the daemon is down, do not install docker and do not
improvise — stop and ask the human how they want the services hosted.
`squeezr services status` shows container state; `squeezr services down` stops both
(data volumes are kept).

## 3. Workspace: init → add → index

Run from the project root; each project gets its own workspace:

```bash
squeezr init
squeezr add .
squeezr index --events
```

- `init` creates the workspace in the current directory with default config.
- `add <path> [--name <repo>]` registers a repository; the index stays empty until
  at least one repo is added. `add .` covers the common single-repo case (the
  `.squeezr` store dir is always skipped). Sibling repos join the same workspace
  with `squeezr add <path> --name <repo>` (absolute path or one relative to the
  workspace root), and the `repo` parameter of the search/map tools then narrows
  a query to a single repository.
- `index` builds the trigram + symbol index and embeds chunks for semantic search.
  `--events` streams machine-readable progress events instead of the interactive
  progress bar. Other switches: `--full` (complete rebuild), `--no-semantic`
  (literal-only), `--reembed` (re-embed everything after a model change).

`squeezr status` shows per-repo file/symbol/chunk counts, the configured
embeddings provider and Qdrant state.

## 4. MCP registration

```bash
squeezr mcp install --all --scope user --yes
```

Registers the squeezr MCP server (stdio) in every MCP client detected on the
machine, at user scope, fully non-interactive (`--yes` without `--scope` also
defaults to user). After it succeeds, restart the agent session — MCP clients
load server configs at startup, not live. Six tools then appear: `code_search`,
`symbol_lookup`, `find_references`, `repo_map`, `expand_span`, `index_status`.
`squeezr mcp doctor` verifies the registration later; `squeezr mcp remove` undoes it.

Repo-level onboarding (only when the human explicitly asks for it): after MCP
registration, `squeezr mcp install --project` writes a committed
`squeezr:project-instructions` block into the project's own agent files —
`AGENTS.md` always (created when missing), plus `CLAUDE.md`/`QWEN.md` when they
exist and are not one-line `@AGENTS.md` import pointers. Without `--client`/`--all`
the command only writes the block, no client registration; plain `mcp install`
never touches project files. The block header carries the squeezr version —
re-run the same command after a squeezr upgrade to refresh it;
`squeezr mcp remove --project` strips the block from all carrier files (files
themselves are never deleted).

Claude Code also has an optional advisory hook. `squeezr hook pre-read` reads the
PreToolUse payload from stdin and, when the Read targets a large file or wide
range inside a registered workspace, adds `additionalContext` pointing at
`expand_span` / `symbol_lookup`; otherwise it prints nothing and exits 0. It is
advisory only (never sets `permissionDecision`). The wiring is human-applied —
never edit hook configs from an agent session. Other clients (qwen, kimi) can
wire the same pattern when their hook surface allows. In
`~/.claude/settings.json` (project scope: `.claude/settings.json`):

```json
{
  "hooks": {
    "PreToolUse": [
      {
        "matcher": "Read",
        "hooks": [
          { "type": "command", "command": "squeezr hook pre-read" }
        ]
      }
    ]
  }
}
```

## 5. Tool cheat-sheet (MCP)

Navigation flow: `repo_map` first for orientation → `code_search` /
`symbol_lookup` to locate → `find_references` before changing anything →
`expand_span` to read. Exact string search or a repo outside the index —
plain grep/read is correct, not a violation.

- `repo_map(repo?, depth?, maxTokens?, prefix?)` — directory tree with type/member
  signatures, no bodies. First contact with an unfamiliar workspace. Expect
  `truncated:true` at depth ≥2 on any repo with a few hundred files — that is
  structural, not a failure: orient at depth 1, then walk subtrees with `prefix`
  (labels come back prefix-relative).
- `code_search(query, repo?, prefix?, topK?, maxTokens?)` — hybrid exact + semantic
  search: paraphrases work ("validate config file") and so do exact identifiers
  ("VectorDimensionMismatchException"). Returns file:line matches with snippets,
  never full bodies. `prefix` scopes to one subtree. Prose queries can rank
  docs/tests above src — that is what `indexing.rankExclude` is for (see § 5a).
- `symbol_lookup` — exact symbol lookup: signature, doc-comment, file:span.
- `find_references` — caller-side reference points for a symbol.
- `expand_span(file, startLine, endLine?, contextLines?, maxTokens?)` — the only
  tool that returns raw source lines; reads the live file on disk, so it reflects
  edits newer than the index. Call it on coordinates from other tools.
- `index_status()` — workspace freshness: per-repo counts, pending embeddings,
  Qdrant availability, and the resolved workspace `rootPath` (if it is not your
  project's root, the session serves a different workspace). Call before heavy
  querying to decide on a reindex.

Budgets: every tool is token-budgeted; `code_search` defaults to maxTokens 2000
and sets `truncated` when it overflows — narrow the query or adjust maxTokens
instead of reading files. Pass `topK` to cap the merged hit count when a few
strong hits are enough.

Freshness: reads refresh modified files inline (seconds — verified 2026-09-28: a
file created externally was exact-searchable ~9 s later without any manual run)
or report them in `staleFiles` (then suggest `squeezr index`). The documented
in-memory cache for identical repeated calls did NOT register hits through the
MCP path during that validation (`squeezr stats`: 0 hits across a session of
byte-identical calls) — do not rely on it for token budgeting.

## 5a. Workspace config levers

The workspace config is `~/.squeezr/workspaces/<store-key>/config.json` (store
key from `squeezr fleet`). What actually changes search quality:

- `indexing.ignore` — globs hard-excluded from the index. Defaults already cover
  build output and secret files (`.env*`, `*.key`, `*.pem`, `id_rsa*`, ...). Add
  prose/mirror trees (`**/BACKLOG/**`, `**/.qwen/tmp/**`) so markdown does not
  eat map budget and ranking. Takes effect on the next `squeezr index`.
- `indexing.rankExclude` — globs kept indexed (`expand_span` / `symbol_lookup` /
  `find_references` still reach them, `index_status` names the active patterns)
  but dropped from `code_search`/`repo_map` ranking. Applies live, no reindex.
- `watch.enabled` (default false) — lets the serve-watch daemon refresh the index
  on file changes. Needs a daemon restart to take effect; an MCP-client-owned
  daemon refuses `fleet restart` — restart the agent session instead.
- Pending embeddings drain only on an explicit `squeezr index` run — the watch
  loop does not chew the backlog. If coverage sits below 1 with `pending > 0`,
  run `squeezr index` (fast on Ollama; a few minutes on bundled onnx CPU).

Every tool also has a CLI twin (`squeezr search`, `map`, `symbol`, `refs`,
`expand`) for use outside an MCP session — CLI flags add `--regex`, `-i`,
`--path-glob`, `--language`. Each CLI invocation is its own telemetry/cache
session.

## 6. Troubleshooting

- **qdrant.endpoint / gRPC vs REST** — a warning saying the configured endpoint
  "points at the REST port — squeezr speaks gRPC" means the workspace config has
  6333 where 6334 belongs. Fix `qdrant.endpoint` to `http://localhost:6334`;
  both endpoints are printed by `squeezr services up`.
- **dimension_mismatch** — "Qdrant collection ... vector size X, but the active
  embedding model produces Y": the embedding model changed. Fix is
  `squeezr index --reembed`; warn the human first, re-embedding a large
  workspace takes a while.
- **Tools missing in qwen** — some clients (qwen) deliver MCP tools deferred
  via `tool_search select`: the squeezr tools stay unloaded until the client
  activates them by name. That is the client's mechanism, not a broken squeezr
  registration (`squeezr mcp doctor` still reports the server healthy); check
  the client settings for auto-activation options.
- **Daemons** — `squeezr fleet` lists watch/serve-watch daemons across all
  registered workspaces (pid, uptime, staleness); `squeezr fleet stop <storeKey>`
  and `squeezr fleet restart <storeKey>` manage one (exact store key from the
  table).
- **Telemetry** — `squeezr stats` shows per-tool call counts, token savings
  versus reading files, and cache hit-rate; `squeezr stats --reset` clears.
