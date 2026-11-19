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
| `rust-crew` | `rust-architect`, `rust-best-practices`, `rust-code-reviewer`, `rust-coder`, `rust-init`, `cargo-licensing-guard` | present on `.claude` only until T5 parity |
| `js-crew` | `web-*` (4), `node-*` (4), `js-platform-*` (3), `frontend-designer` | |
| `sociality` | `.kimi-code/SOCIAL_DICTIONARY.md` | see *Open boundary* below |
| `crew-extras` | `go-*` (3), `python-*` (3), `maui-coder`, `augur-*` (6), `ai-code-detector`, `ai-obfuscator`, `ai-obfuscation-reviewer`, `git-commit-ai-detector`, `umbraco-sensei`, `tutor-master`, `squeezr-ops` | the leftovers bucket the owner defined |

Spec R1 also names `stuck` in the extras bucket. There is no `stuck` skill
directory in the kit — it is a runtime-bundled Qwen Code skill, never a kit member,
so it is not assignable and appears nowhere in the manifest.

Skill counts across the four sides: 57 canonical skills (`.claude` side), the other
three sides carry 51 each today; 46 canonical agent definitions, 42 on the other
three sides; 15 prose/config files.

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
  `tools` field would make the loader drop the role. Measured: zero `mcp__` tokens
  across all 42 `.codex/agents/*.toml` — on that side MCP access is declared in
  `.codex/AGENTS.md` and the skill bodies, never per role (SYNC-STRUCTURE's
  "prose inside `developer_instructions`" wording said otherwise; corrected 2026-11-19
  against the content). So 242 is a three-side repair plus one structurally exempt
  side, and the codex exemption is a *container* property, not a grant that was
  checked and found in place.

## Open boundary

`sociality` currently carries one file. The tone/communication rules that belong to
it are written into the per-side contract files (`Communication Style`,
`Identity`, the slang dictionary table), which cannot be split without a prose
rewrite — outside feature 250's scope. Splitting them into their own shipped file
is the follow-up that makes this kit more than a pointer.
