# Global kit materialization — implementation design (feature 251 T0)

Decisions locked here feed T1–T6. Owner rulings live in the validated spec v4 (directive f99a57c5 + chat ballot); this doc is the implementation-level spelling of those rulings.

## 1. Detection rule — "installed" agent

An agent from `AgentRegistry.All` counts as **installed** when its first existing config directory resolves: walk `ConfigDirCandidates` in order (`$ENV` candidates expanded first — `KIMI_CODE_HOME`, `CODEX_HOME` — then the literal `~/…` fallbacks); the first candidate that exists on disk wins, and the agent is installed. No binary-on-PATH check.

- False positive: config dir survives a deleted CLI install (harmless — writing skills there is still valid).
- False negative: a CLI installed but never launched has no config dir yet — nothing to deploy to until first launch, acceptable.
- Home skills dir per platform comes from `PlatformDirectoryMap` (`platforms.json`); it is joined onto the RESOLVED config dir when the candidate was env-based (`$CODEX_HOME` → `<codexHome>/.agents/skills`), else onto the literal `~/…` root from platforms.json. Env-var home roots take precedence over `~` in the same way `AgentRegistry` consumers already resolve.

## 2. Shared-homes table — merge everywhere, mirror never (app door)

| platform | home skills dir | semantics | third-party content |
|---|---|---|---|
| claude | `~/.claude/skills` | merge | yes — Anthropic-synced skills live here (SYNC-STRUCTURE 2026-10-15 ruling) |
| qwen | `~/.qwen/skills` | merge | yes — non-crew skills hosted |
| codex | `$HOME/.agents/skills` | merge | yes — the alias is also read by Kimi/Gemini |
| kimiCode | `~/.kimi-code/skills` | merge | treated as potentially shared |
| gemini | `~/.gemini/skills` | merge | treated as potentially shared |
| antigravity | `~/.antigravity/skills` | merge | treated as potentially shared |

The app door **never** whole-directory-deletes anything under a home: deploy writes tracked files only, dematerialize deletes tracked rows only. `mirror` mode remains an install.sh bootstrap concept (with its crew-owned exclusions) and does not exist in the door. Missing home skills dir = **skip, never create** (ruling 2) — a skipped directory lands in the run report as `skipped` with the reason.

## 3. Deploy source

`SkillKit.SourcePath` (Bundled/Imported kits). Kit-level skip rules, surfaced in the report, never thrown:
- `SourceKind == Manual` or `SourcePath == null` → `skipped: manual kit has no deploy source`;
- SourcePath missing on disk → `skipped: source path missing`;
- resolved home skills dir missing → `skipped: home dir missing` (per agent).

Deploy payload = the SourcePath `skills/` subtree only (agent roles, prose config files and hooks are NOT home-deployed by the door; install.sh stays responsible for those, and the R6 weave is the prose path — see §4).

## 4. Marker grammar (R6 sociality weave)

```
===OVERMIND SOCIALITY KIT START===
<content>
===OVERMIND SOCIALITY KIT END===
```

- Kit token = `kit.Name` uppercased, non-alphanumerics → `_` (`overmind-ceremony` → `OVERMIND_CEREMONY`).
- Insert: file missing → create with the block; file exists without the block → append `\n` + block at EOF; file ends without trailing newline → add one first.
- Re-deploy: replace the whole block between the kit's own markers (byte-stable outside). Multiple kits' blocks coexist; each kit only ever touches its own block.
- Dematerialize: cut the block and one following newline; if the file becomes whitespace-only, delete the file; files outside markers are never touched.
- Entry file per agent = `<resolved config dir>/<AgentFileCandidates[0]>` (claude → `~/.claude/CLAUDE.md`, qwen → `~/.qwen/QWEN.md`, codex → `<codexHome>/AGENTS.md`, kimi → `~/.kimi-code/AGENTS.md`, gemini → `~/.gemini/GEMINI.md`, antigravity → `~/.gemini/config/AGENTS.md`).
- Marker content source: `*.md` files at the kit SourcePath TOP level, excluding the INFRA names (`README.md`, `SYNC-STRUCTURE.md`, `KIT-MEMBERSHIP.md`, `BACKLOG.md`), concatenated in sorted order with `\n\n` separators. For `sociality` this resolves to `SOCIAL_DICTIONARY.md`.
- Managed zone: hand edits between markers are overwritten by the next deploy (kits-page copy states it); content outside markers is byte-untouchable.
- Weave applies to prose-bearing kits regardless of entry count (zero-entry kits like sociality get exactly this surface).

## 5. Tracking — new Core entities (ruling 1)

`src/Overmind.Core/Entities/GlobalMaterializedFile.cs`:
`Id, KitId, Platform (AgentPlatform), RelativePath, Sha256, MaterializedAt` — the home-root analogue of `MaterializedFile` (project-rooted entity untouched).

`src/Overmind.Core/Entities/GlobalMarkerDeployment.cs`:
`Id, KitId, Platform, EntryFileName, ContentSha256, DeployedAt` — one row per (kit, platform) with a woven block; `ContentSha256` is the hash of the exact block body for drift detection.

EF configuration + migration in `Overmind.Infrastructure` (T1).

## 6. Guards and run report (033/074 parity)

- Untracked file at a planned path, or a tracked file with local edits → refuse that file, `refused` in the report; explicit `force: true` overwrites with per-file warnings.
- Hash parity scan = drift: `deployed` / `drifted` (local edit or untracked collision or marker block missing/edited) / `undeployed`, per kit × platform.
- Run diagnostics: lastSuccess/lastAttempt parity with the project materializer's run records, scoped `kind = global`; per-file written/skipped/refused + warnings.

## 7. API shape (KitsApi extension)

```
POST   /api/kits/{kitId}/global/{agent}    deploy (query: force=false)
DELETE /api/kits/{kitId}/global/{agent}    dematerialize
GET    /api/kits/global/state              per kit × platform state matrix + drift
```

- `agent` validated against `AgentRegistry` names (case-insensitive, as `Find` does).
- Mandatory kit: deploy allowed for every platform, remove **refused** (no opt-out, ruling 3); per-project activation already refused by `EnsureProjectActivatable`.
- Manual kits: deploy refused with the same skip reason as §3.
- Errors mapped through the existing `MaterializationErrors` / `KitActivationErrors` mappers.

## 8. Boot-seed / migration legs (T2)

Registration replaces the deliberate null in `DependencyInjection.cs`; `BundledSeedService`'s existing mandatory-deploy call goes live; swallow-and-log stays for a null/failed door; the crew→6 migration deploys ceremony globally when the door is present. Tests cover: fresh seed invokes DeployAsync per detected agent, null-door path still logged, migration swallow path.
