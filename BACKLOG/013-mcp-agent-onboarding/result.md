---
feature: 013
slug: mcp-agent-onboarding
status: validated
created: 2026-09-19
validated: 2026-09-19
---

# Result 013 — MCP agent onboarding ceremony (discover + remember project ID)

<outcome>
013-mcp-agent-onboarding shipped in 0.1.7. All four tasks done, all reviews passed (PASS / PASS WITH NOTES with in-tree fixes). Suite: 770/770 green (baseline was 735 → +35 tests across the feature).

Three doors, one brain on the host:
- `POST /api/onboard` (src/Overmind.Host/Api/OnboardApi.cs) — resolves a project from the reported cwd (GetFullPath + trailing-slash trim + best-effort symlink resolve, ordinal compare vs Project.DiskPath) or an explicit projectId override; auto-creates on miss (dir name, `-N` suffix on name collisions); returns projectId + board summary (counts by status + pinned slug via PlanningFeature.IsCurrent) + the quickstart text. Quickstart single source: OnboardQuickstart.Text; FluentValidation OnboardRequestValidator per repo convention.
- MCP tool `overmind_onboard` (src/Overmind.Mcp/OvermindOnboardingTools.cs) — no args, sends Environment.CurrentDirectory (env OVERMIND_PROJECT_ID wins), quickstart passed through verbatim, description opens with "Call this first", ServerInstructions mention. Tool count 15 → 16, additive.
- `overmind-mcp install --onboard` (src/Overmind.Mcp/Registration/) — AgentFileCandidates in the 009 registry (claude→CLAUDE.md, kimi/codex/antigravity→AGENTS.md, gemini→GEMINI.md, qwen→QWEN.md); managed block between `<!-- overmind:begin/end -->` markers (second implementation structurally aligned with Infrastructure/Planning/ManagedBlock.cs — adapter references nothing); conflict scanner warns on BACKLOG/file-ceremony lines without touching them; host-down → placeholder + warning, exit 0; `--onboard --remove` → usage error; dry-run skips the onboard HTTP call entirely (post-review fix).
</outcome>

<evidence>
Live dogfood on the installed 0.1.7 bundle (2026-09-20):
- Squeezr by cwd: adapter spawned with cwd=/mnt/Ubuntu-Dev/Projects/AI-Tools/Squeezr → `overmind_onboard` returned `Project: Squeezr (31f33e30-6056-40a3-8447-c43d889fee9f)`, board `draft: 5, ready: 4, done: 9`, current `045-squeezr-everywhere`, quickstart verbatim. tools/list = 16, overmind_onboard present.
- Fixture repo (/tmp/onboard-fixture, CLAUDE.md prescribing BACKLOG file ceremony): dry-run → "would update onboard block", conflict warning listing lines 3-4, placeholder note, zero writes; real run → .mcp.json added + managed block appended with the REAL auto-created project id (created:true path exercised), human content byte-preserved; second run → "onboard block unchanged", md5 identical before/after.
- Auto-create: unknown repo produced project 'onboard-fixture' (14f6c51a-b18d-4e0b-89cd-6c0721151e2b), visible via API, deleted after evidence capture (204).
- Pack: overmind-0.1.7-linux-x64.tar.gz (110M), deployed to ~/apps/overmind-0.1.7-linux-x64, symlink switched, host healthy on 4761 (version 1.0.0+0e3decf).
- Reviews: audits/review-013-01.md, review-013-02.md, review-013-03.md — no blockers anywhere.
</evidence>

<deferred>
- BOM round-trip in OnboardBlockWriter eats a UTF-8 BOM on first write (corner case; most agent files have no BOM) — recorded from review-013-03.
- `--all --onboard` processes a shared AGENTS.md three times (kimi/codex/antigravity) — noisy output, harmless (1 created + 2 unchanged).
- TOCTOU double-create on concurrent onboard (DiskPath has no unique index, same as existing ProjectsApi.Create); cwd=`/` → raw 500 instead of 4xx — recorded from review-013-01.
- Pre-existing repo-wide `dotnet format` redness on generated EF migrations (IDE0161) — already tracked as hardening candidate.
</deferred>