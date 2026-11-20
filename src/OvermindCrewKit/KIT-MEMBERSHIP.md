# kit-membership.json — shape and classification contract

`kit-membership.json` (kit root) is the single membership map of the six Overmind
kits (feature 250, spec R1). It says which kit owns which file. It does not move
anything: the four mirror sides under `.claude/`, `.qwen/`, `.kimi-code/`,
`.codex/` stay exactly where they are, and `manifest.json` keeps deploying the
whole tree to the tool homes unchanged.

Read this file before touching the manifest, `build/pack.sh` or the bundle shape.

## Shape

```json
{
  "overmind-ceremony": [ ".claude/skills/overmind-ceremony", ".qwen/agents/wave-marshal.md", "…" ],
  "dotnet-crew":       [ "…" ],
  "rust-crew":         [ "…" ],
  "js-crew":           [ "…" ],
  "sociality":         [ "…" ],
  "crew-extras":       [ "…" ]
}
```

Five properties the consumer can rely on, all enforced by
`tools/check-kit-membership.py`:

1. **Six keys, fixed order** — ceremony, dotnet, rust, js, sociality, extras. No
   metadata keys inside the manifest: mandatory-ness and other kit flags are domain
   data, not membership data.
2. **Every value is a flat array of strings** — no nesting, no per-kind sub-objects.
3. **Paths are kit-root-relative**, forward slashes, no leading `./`, no trailing
   slash. Skill entries are directories (`.claude/skills/dotnet-coder`), agent and
   prose entries are files.
4. **Sorted, deduplicated, and each path appears exactly once across all six arrays.**
5. **Every path exists on disk.** A stale entry is a check failure, not a warning.

For `pack.sh` that means: read the array for the kit, copy each entry into
`bundled/<kit>/` preserving its relative path, and the per-kit file inventory is
done. Nothing else needs parsing.

## Generation and check

```
python3 tools/gen-kit-membership.py            # rebuild the manifest from disk
python3 tools/check-kit-membership.py          # membership + frontmatter gates
python3 tools/check-kit-membership.py --verbose
```

The manifest is generated, never hand-edited. The classification table lives in
`tools/gen-kit-membership.py`; add a skill there when you add a skill to the kit,
regenerate, commit both. Re-run the generator after a mirror-parity change (rust
landing on the other three sides is feature 250 T5) — until then the generated
arrays describe what is actually on disk, side by side.

## Classification (spec R1)

| Kit | Members | Notes |
|-----|---------|-------|
| `overmind-ceremony` (mandatory) | `overmind-ceremony` plus the language-agnostic process and orchestration roles: `architect-review`, `audit-orchestrator`, `coding-challenge-orchestrator`, `documentation-writer`, `fast-fixer-coder`, `feature-orchestrator`, `naming-sensei`, `pipeline-doctor`, `product-strategist`, `solution-architect`, `sprint-planner`, `wave-marshal` | also owns all per-side prose and runtime config (see below) |
| `dotnet-crew` | `dotnet-architect`, `dotnet-best-practices`, `dotnet-code-reviewer`, `dotnet-coder`, `dotnet-init`, `nuget-licensing-guard` | |
| `rust-crew` | `rust-architect`, `rust-best-practices`, `rust-code-reviewer`, `rust-coder`, `rust-init`, `cargo-licensing-guard` | on all four sides since the 2026-11-19 T5 parity |
| `js-crew` | `web-*` (4), `node-*` (4), `js-platform-*` (3), `frontend-designer` | |
| `sociality` | `.kimi-code/SOCIAL_DICTIONARY.md` | see *Open boundary* below |
| `crew-extras` | `go-*` (3), `python-*` (3), `maui-coder`, `augur-*` (6), `ai-code-detector`, `ai-obfuscator`, `ai-obfuscation-reviewer`, `git-commit-ai-detector`, `umbraco-sensei`, `tutor-master`, `squeezr-ops` | the leftovers bucket the owner defined |

Spec R1 also names `stuck` in the extras bucket. There is no `stuck` skill
directory in the kit — it is a runtime-bundled Qwen Code skill, never a kit member,
so it is not assignable and appears nowhere in the manifest.

Skill counts across the four sides: 57 skill dirs and 46 agent definitions on
every side — rust parity landed on all four sides 2026-11-19 (feature 250 T5), so
the `.claude`-only asymmetry is closed; 15 prose/config files. Membership is
derived from disk by `tools/gen-kit-membership.py` and re-checked by
`tools/check-kit-membership.py`, which prints the per-run denominators (scanned,
skipped, parsed) — counts quoted in prose rot, the checker's do not.

## Recorded decisions

- **`maui-coder` → `crew-extras`, not `dotnet-crew`.** The owner's mapping is
  explicit: maui belongs in extras even though it is a .NET stack.
- **`solution-architect`, `architect-review`, `product-strategist`,
  `sprint-planner`, `feature-orchestrator`, `wave-marshal`, `audit-orchestrator`,
  `coding-challenge-orchestrator`, `pipeline-doctor`, `naming-sensei`,
  `documentation-writer`, `fast-fixer-coder` → `overmind-ceremony`.** These are
  stack-agnostic process roles; R1 puts them in the mandatory core, so a project
  that installs only the ceremony still gets planning, review, decomposition and
  naming.
- **Per-side prose and runtime config → `overmind-ceremony`.** `CLAUDE.md`,
  `QWEN.md`, both `AGENTS.md` files, all four `SKILLFLOW.md`, `WAKE-CHANNEL.md`,
  `.claude/settings.json`, `.kimi-code/config.toml`, `.qwen/settings-snippet.jsonc`
  and `.qwen/hooks/post-edit-verify.{ps1,sh}`. Task T1's rule is "per-side config
  files follow their side's primary kit": each side's primary kit is the mandatory
  one, and the contract file plus its enforcement layer must install together or the
  side is inert. The contract prose also carries the tone rules that `sociality`
  would otherwise own — a single file cannot split across two kits, so it stays with
  the contract.
- **One physical file, four logical deploy targets.** `SOCIAL_DICTIONARY.md` and
  `WAKE-CHANNEL.md` exist once (under `.kimi-code/`) and `install.*` copies them
  into all four homes. The manifest lists the source path once, never four.
- **Kit-root infrastructure is not kit content and stays out of the manifest:**
  `manifest.json`, `install.*` / `uninstall.*`, `README.md`, `SYNC-STRUCTURE.md`,
  `KIT-MEMBERSHIP.md`, `kit-membership.json` itself, `tools/`, `BACKLOG/`,
  `BACKLOG.md`, `audits/`, `.gitattributes`, `.gitignore`.
- **Reviewer-agent tool grants are frontmatter content, not membership content.**
  Feature 242's repair (the `mcp__overmind__*` grant was sitting below the closing
  `---` on the `.qwen` and `.kimi-code` sides) lives inside the agent files; the
  checker parses it on every markdown side and fails if any grant line falls
  outside the frontmatter, then sweeps *all* side markdown — skills included — for
  the same defect shape. Measured on this checkout: 12 affected files per side on
  `.qwen` and `.kimi-code` (8 `*reviewer` roles plus `ai-code-detector`,
  `git-commit-ai-detector`, `naming-sensei`, `pipeline-doctor` — the same defect, so
  the same pass), `.claude` already correct on all 13 grant-bearing roles, and
  `.codex` not a carrier at all: its TOML roles expose only `name`, `description`,
  `model`, `developer_instructions`, so there is no misplaced list to move and a
  `tools` field makes the loader drop the role. Measured: zero `mcp__` tokens
  across all 46 `.codex/agents/*.toml` (re-measured 2026-11-19 after rust parity,
  was 42 before) — on that side MCP access is declared in
  `.codex/AGENTS.md` and the skill bodies, never per role (SYNC-STRUCTURE's
  "prose inside `developer_instructions`" wording said otherwise; corrected 2026-11-19
  against the content). So 242 is a three-side repair plus one structurally exempt
  side, and the codex exemption is a *container* property, not a grant that was
  checked and found in place.
- **The codex role loader is strict but not silent — measured 2026-11-19 on
  codex-cli 0.160.1** (scratch `CODEX_HOME` under the lane worktree, live config
  untouched): a role file with an extra field is dropped with a stderr warning at
  session start — `warning: Ignoring malformed agent role definition: failed to
  deserialize agent role file at …`. Two drop flavors: an unknown field name
  reports `unknown field`, and a `tools` key is worse than unknown — it collides
  with Codex's tool-*configuration* namespace (deserialized against
  `WebSearchToolConfigInput`), so even a value that parsed would configure tools,
  never allowlist them. A per-role tool restriction cannot be expressed in the
  role container at all.
- **Codex per-role tool restriction: accepted risk (owner-facing decision,
  2026-11-19, closes the T1 WARNING-1 handoff).** Options weighed: (a) restriction
  wording in `developer_instructions` — prose only, no enforcement, and it would
  rewrite all 46 roles for a guarantee the codex side never claimed; (b)
  `agents/openai.yaml` per skill — measured against docs: `dependencies.tools`
  *declares* MCP dependencies and `policy.allow_implicit_invocation` gates prompt
  triggering; neither restricts the session's tool surface, so it is not the 242
  mechanism; (c) accept explicitly. Chosen: **(c) + (a) as mitigation-in-place** —
  the codex side's enforcement was already declared behavioral/prompt-only by crew
  decision (SYNC-STRUCTURE, "Deterministic enforcement" row), and the read-only
  reviewer contract lives in the reviewer skill bodies, which every codex session
  loads. Residual risk: a codex reviewer role *can* write, unlike the three
  allowlist sides; nothing gates it at load time. If Codex ever ships a role-level
  restriction field, revisit; tracked nowhere else — this bullet is the record.

## Open boundary

`sociality` currently carries one file. The tone/communication rules that belong to
it are written into the per-side contract files (`Communication Style`,
`Identity`, the slang dictionary table), which cannot be split without a prose
rewrite — outside feature 250's scope. Splitting them into their own shipped file
is the follow-up that makes this kit more than a pointer.
