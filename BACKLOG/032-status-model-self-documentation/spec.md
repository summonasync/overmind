---
feature: 032
slug: status-model-self-documentation
status: validated
created: 2026-09-20
validated: 2026-09-20
---

# Spec 032 — Status model self-documentation: lifecycle в onboarding + per-kind статусы в MCP-тулах

<decisions>
- Two agent-facing surfaces change, web deferred: (1) the overmind_onboard quickstart text (OnboardQuickstart.cs — the string agents actually read first) gains a compact lifecycle block; (2) MCP tool descriptions in OvermindPlanningTools.cs gain per-kind artifact status sets. Web needs nothing — board dropdowns already show only allowed targets since 023, and human UI hints are a separate UX concern (anti-scope).
- Quickstart lifecycle block: compact, not a wide table — statuses as one arrow line (draft → ready → inProgress → review → done), then three one-liners: auto-transitions (artifact create → inProgress; all tasks done + spec + plan → review; task create on review → back to inProgress), escape hatch (planning_set_feature_status reaches done from ANY non-done status; done is terminal), per-kind artifact statuses. Agents parse this shape fine and the quickstart stays lean.
- Per-kind sets stated verbatim wherever a status can be written: spec/plan/result: draft, validated — validated is the terminal "done" for documents, result has NO done status; task: todo, inProgress, review, done, blocked. The source of these sets is PlanningArtifact.AllowedStatuses (Core entity) — descriptions must quote it, and a test pins description text against AllowedStatuses output so the docs cannot silently drift from the model (the exact failure mode this feature fixes).
- planning_set_status description carries the per-kind sets + "logNote is mandatory on every move"; planning_upsert_artifact description notes artifacts are created draft and validated via planning_validate; planning_set_feature_status already documents its own rules (style etalon — no change).
- Onboard pre-read hint: one line in the quickstart — before working, read notes/decisions/guards via the overmind://resources / list_entries filters; tombstones join this line when 029 lands (not before — no dead references).
- result-validated → auto-review on InProgress features: NO. The gate answer "approve + auto-review" (2026-09-20) was a misclick — user corrected in-chat immediately ("я хотел первое"); the intended choice is approve WITHOUT auto-review. The stuck state stays resolvable through the 023/08 agent door (planning_set_feature_status → review), and auto-promoting on document validation would fire on spec/plan too, blurring the ceremony gates for zero real benefit. If the model ever changes, that change gets its own feature.
</decisions>

<open-questions>
- none (result→review auto-promotion: NO — gate misclick corrected by user in-chat 2026-09-20)
</open-questions>

<requirements>
- OnboardQuickstart.cs: lifecycle block per decisions (arrow line + auto-transitions line + escape-hatch/terminal-done line + per-kind artifact statuses line + pre-read hint line). English, terse — the whole block ≤ 8 lines.
- OvermindPlanningTools.cs: planning_set_status description carries per-kind sets verbatim + logNote-mandatory; planning_upsert_artifact description carries created-as-draft + validate-via-planning_validate. planning_set_feature_status untouched.
- Tests (Mcp project): pin the quickstart block against the real model — every PlanningFeatureStatus name appears in the quickstart; per-kind listing in tool descriptions matches PlanningArtifact.AllowedStatuses(kind) for all four kinds (name-level match); no stale status words (e.g. "done" never claimed for result).
- Verification: dotnet build + test green; live check — MCP adapter session sees the new quickstart text on overmind_onboard (after adapter restart, binary pickup is deploy-time — pinned in task).
</requirements>

<anti-scope>
- No web UI changes (status hints in UI are out; dropdowns already constrain).
- No lifecycle MODEL changes — this feature documents the model, never changes it (auto-review included: rejected here, needs its own feature if ever wanted).
- No README/docs-tree changes beyond the two code surfaces (BACKLOG materialization carries the rest).
- No tombstone references until 029 exists.
</anti-scope>

<log>
- 2026-09-20 — Gate answer "approve + auto-review" was a misclick; user corrected in-chat ("я хотел первое") — reverted to approve WITHOUT auto-review. v3 restores the docs-only scope; provenance of the flip recorded.
</log>
