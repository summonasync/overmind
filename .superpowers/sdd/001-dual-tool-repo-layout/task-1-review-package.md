 M .gitignore
 A .kimi-code/SKILLFLOW.md
R  SOCIAL_DICTIONARY.md -> .kimi-code/SOCIAL_DICTIONARY.md
R  agents/ai-code-detector.md -> .kimi-code/agents/ai-code-detector.md
R  agents/ai-obfuscation-reviewer.md -> .kimi-code/agents/ai-obfuscation-reviewer.md
R  agents/ai-obfuscator.md -> .kimi-code/agents/ai-obfuscator.md
 A .kimi-code/agents/architect-review.md
R  agents/audit-orchestrator.md -> .kimi-code/agents/audit-orchestrator.md
R  agents/coding-challenge-orchestrator.md -> .kimi-code/agents/coding-challenge-orchestrator.md
R  agents/documentation-writer.md -> .kimi-code/agents/documentation-writer.md
R  agents/dotnet-architect.md -> .kimi-code/agents/dotnet-architect.md
R  agents/dotnet-code-reviewer.md -> .kimi-code/agents/dotnet-code-reviewer.md
R  agents/dotnet-coder.md -> .kimi-code/agents/dotnet-coder.md
R  agents/dotnet-init.md -> .kimi-code/agents/dotnet-init.md
R  agents/frontend-designer.md -> .kimi-code/agents/frontend-designer.md
R  agents/maui-coder.md -> .kimi-code/agents/maui-coder.md
R  agents/next-js-architect.md -> .kimi-code/agents/next-js-architect.md
R  agents/next-js-code-reviewer.md -> .kimi-code/agents/next-js-code-reviewer.md
R  agents/next-js-coder.md -> .kimi-code/agents/next-js-coder.md
R  agents/task-orchestrator.md -> .kimi-code/agents/task-orchestrator.md
R  agents/tutor-master.md -> .kimi-code/agents/tutor-master.md
R  config.toml -> .kimi-code/config.toml
R  skills/ai-code-detector/SKILL.md -> .kimi-code/skills/ai-code-detector/SKILL.md
R  skills/ai-obfuscation-reviewer/SKILL.md -> .kimi-code/skills/ai-obfuscation-reviewer/SKILL.md
R  skills/ai-obfuscator/SKILL.md -> .kimi-code/skills/ai-obfuscator/SKILL.md
 A .kimi-code/skills/architect-review/SKILL.md
RM skills/audit-orchestrator/SKILL.md -> .kimi-code/skills/audit-orchestrator/SKILL.md
R  skills/coding-challenge-orchestrator/SKILL.md -> .kimi-code/skills/coding-challenge-orchestrator/SKILL.md
R  skills/documentation-writer/SKILL.md -> .kimi-code/skills/documentation-writer/SKILL.md
R  skills/dotnet-architect/SKILL.md -> .kimi-code/skills/dotnet-architect/SKILL.md
 A .kimi-code/skills/dotnet-best-practices/SKILL.md
RM skills/dotnet-code-reviewer/SKILL.md -> .kimi-code/skills/dotnet-code-reviewer/SKILL.md
RM skills/dotnet-coder/SKILL.md -> .kimi-code/skills/dotnet-coder/SKILL.md
R  skills/dotnet-init/SKILL.md -> .kimi-code/skills/dotnet-init/SKILL.md
R  skills/frontend-designer/SKILL.md -> .kimi-code/skills/frontend-designer/SKILL.md
R  skills/maui-coder/SKILL.md -> .kimi-code/skills/maui-coder/SKILL.md
R  skills/next-js-architect/SKILL.md -> .kimi-code/skills/next-js-architect/SKILL.md
R  skills/next-js-code-reviewer/SKILL.md -> .kimi-code/skills/next-js-code-reviewer/SKILL.md
R  skills/next-js-coder/SKILL.md -> .kimi-code/skills/next-js-coder/SKILL.md
R  skills/nuget-licensing-guard/SKILL.md -> .kimi-code/skills/nuget-licensing-guard/SKILL.md
RM skills/task-orchestrator/SKILL.md -> .kimi-code/skills/task-orchestrator/SKILL.md
R  skills/tutor-master/SKILL.md -> .kimi-code/skills/tutor-master/SKILL.md
 M AGENTS.md
 D TASK/_TEMPLATE.md
 D docs/superpowers/plans/2026-08-31-dual-tool-repo-layout.md
 D docs/superpowers/specs/2026-08-31-dual-tool-repo-layout-design.md
?? .superpowers/
?? BACKLOG.md
?? TASKS/
=====
 .gitignore                                         |   2 +-
 .kimi-code/SKILLFLOW.md                            |  48 ++
 .../SOCIAL_DICTIONARY.md                           |   0
 {agents => .kimi-code/agents}/ai-code-detector.md  |   0
 .../agents}/ai-obfuscation-reviewer.md             |   0
 {agents => .kimi-code/agents}/ai-obfuscator.md     |   0
 .kimi-code/agents/architect-review.md              |  18 +
 .../agents}/audit-orchestrator.md                  |   0
 .../agents}/coding-challenge-orchestrator.md       |   0
 .../agents}/documentation-writer.md                |   0
 {agents => .kimi-code/agents}/dotnet-architect.md  |   0
 .../agents}/dotnet-code-reviewer.md                |   0
 {agents => .kimi-code/agents}/dotnet-coder.md      |   0
 {agents => .kimi-code/agents}/dotnet-init.md       |   0
 {agents => .kimi-code/agents}/frontend-designer.md |   0
 {agents => .kimi-code/agents}/maui-coder.md        |   0
 {agents => .kimi-code/agents}/next-js-architect.md |   0
 .../agents}/next-js-code-reviewer.md               |   0
 {agents => .kimi-code/agents}/next-js-coder.md     |   0
 {agents => .kimi-code/agents}/task-orchestrator.md |   0
 {agents => .kimi-code/agents}/tutor-master.md      |   0
 config.toml => .kimi-code/config.toml              |   0
 .../skills}/ai-code-detector/SKILL.md              |   0
 .../skills}/ai-obfuscation-reviewer/SKILL.md       |   0
 .../skills}/ai-obfuscator/SKILL.md                 |   0
 .kimi-code/skills/architect-review/SKILL.md        |  66 +++
 .../skills}/audit-orchestrator/SKILL.md            |   8 +-
 .../skills}/coding-challenge-orchestrator/SKILL.md |   0
 .../skills}/documentation-writer/SKILL.md          |   0
 .../skills}/dotnet-architect/SKILL.md              |   0
 .kimi-code/skills/dotnet-best-practices/SKILL.md   |  77 ++++
 .../skills}/dotnet-code-reviewer/SKILL.md          |   2 +
 .../skills}/dotnet-coder/SKILL.md                  |   1 +
 {skills => .kimi-code/skills}/dotnet-init/SKILL.md |   0
 .../skills}/frontend-designer/SKILL.md             |   0
 {skills => .kimi-code/skills}/maui-coder/SKILL.md  |   0
 .../skills}/next-js-architect/SKILL.md             |   0
 .../skills}/next-js-code-reviewer/SKILL.md         |   0
 .../skills}/next-js-coder/SKILL.md                 |   0
 .../skills}/nuget-licensing-guard/SKILL.md         |   0
 .../skills}/task-orchestrator/SKILL.md             |  40 +-
 .../skills}/tutor-master/SKILL.md                  |   0
 AGENTS.md                                          |  36 +-
 TASK/_TEMPLATE.md                                  |  53 ---
 .../plans/2026-08-31-dual-tool-repo-layout.md      | 489 ---------------------
 .../2026-08-31-dual-tool-repo-layout-design.md     | 101 -----
 46 files changed, 281 insertions(+), 660 deletions(-)
=====
diff --git a/.gitignore b/.gitignore
index a94f168..c26d16b 100644
--- a/.gitignore
+++ b/.gitignore
@@ -9,4 +9,4 @@ Thumbs.db
 $RECYCLE.BIN/
 
 # Windows shortcuts
-*.lnk
\ No newline at end of file
+*.lnk.superpowers/
diff --git a/.kimi-code/SKILLFLOW.md b/.kimi-code/SKILLFLOW.md
new file mode 100644
index 0000000..22f2e17
--- /dev/null
+++ b/.kimi-code/SKILLFLOW.md
@@ -0,0 +1,48 @@
+# SKILLFLOW.md — Workflow Graph
+
+Every skill reads this file before suggesting its «next step» (see Skill output contract in AGENTS.md).
+The graph suggests — the user decides. Skills NEVER auto-proceed to the next step.
+
+## Main Route (Complex feature)
+
+```
+0.0 requirements-intake ──► 0.1 spec gate ──► 0.2 plan gate ──► tasks ──► 2 implement ──► 3 review ──► 4 fix? ──► 5 arch gate
+     (any-format docs            (USER              (USER            per       (coder per       (2-stage:        (loop back      (end of
+      → requirements.md)          APPROVES)          APPROVES)        task       task file)       spec + code)     to 2)          phase only)
+```
+
+| Step | Skill / Owner | Artifacts | Gate |
+|------|---------------|-----------|------|
+| 0.0 Intake | task-orchestrator (intake mode) | `TASKS/nnn-slug/requirements.md` | User confirms requirements are complete |
+| 0.1 Spec | `superpowers:brainstorming` via task-orchestrator | `TASKS/nnn-slug/spec.md` | **USER APPROVAL — frozen after** |
+| 0.2 Plan | `superpowers:writing-plans` via task-orchestrator | `TASKS/nnn-slug/plan.md` | **USER APPROVAL — frozen after** |
+| Tasks | task-orchestrator | `TASKS/nnn-slug/tasks/NN-*.md` + BACKLOG.md index row | — |
+| 2 Implement | `dotnet-coder` / `next-js-coder` / `maui-coder` (K2.7) | code + `<obstacles>`/`<log>` in task file | `nuget-licensing-guard` before any NuGet change |
+| 3 Review | `dotnet-code-reviewer` / `next-js-coder-reviewer` (K2.7); depth from task frontmatter `review:` — `light` (default) = code reviewer, `hard` = + `architect-review` (Light checklist) | `review-NNN-NN.md` | BLOCKER → Phase 4; Security BLOCKER → STOP, human only |
+| 4 Fix loop | coder ← review comments | updated code | back to Phase 3 |
+| 5 Arch gate | `architect-review` Hard checklist (end of sprint phase / big refactor only) | `architect-review-YYYY-MM-DD.md` + ADR in `TECHNICAL-SOLUTIONS.md` | — |
+
+## Alternative Routes
+
+**Quick fix (bug):**
+`superpowers:systematic-debugging` (root cause first) → coder fix → `superpowers:verification-before-completion` → done. No spec/plan gates. Complex bugs (>3 files) still get Phase 3 review.
+
+**Simple task (<3 files, single layer):**
+coder directly → done. No folder, BACKLOG.md one-liner only.
+
+**Docs-only:**
+`documentation-writer` — skips Phase 3 code review, uses its own verification checklist.
+
+**Coding challenge / тестовое задание:**
+`coding-challenge-orchestrator` — replaces this entire flow with its fixed pipeline.
+
+**System-wide audit (sprint retro, phase end):**
+`audit-orchestrator` — outside the feature flow; outputs `audit-YYYY-MM-DD.md`.
+
+## Hard Rules (always on, any route)
+
+- No auto-commits, ever — `git commit`/`git push` only on explicit user request.
+- Sub-agents: `Kimi K2.7` (default) / `Kimi K3` (orchestration, audit, detection, obfuscation) only. `primary` alias forbidden.
+- Executing sub-agent reads ONLY its task file. spec.md/plan.md belong to orchestrator + reviewer.
+- Frozen artifacts (approved spec.md/plan.md) change only via re-validation + version bump.
+- Every skill ends with the output contract: Done → Next step → Alternatives → STOP.
diff --git a/SOCIAL_DICTIONARY.md b/.kimi-code/SOCIAL_DICTIONARY.md
similarity index 100%
rename from SOCIAL_DICTIONARY.md
rename to .kimi-code/SOCIAL_DICTIONARY.md
diff --git a/agents/ai-code-detector.md b/.kimi-code/agents/ai-code-detector.md
similarity index 100%
rename from agents/ai-code-detector.md
rename to .kimi-code/agents/ai-code-detector.md
diff --git a/agents/ai-obfuscation-reviewer.md b/.kimi-code/agents/ai-obfuscation-reviewer.md
similarity index 100%
rename from agents/ai-obfuscation-reviewer.md
rename to .kimi-code/agents/ai-obfuscation-reviewer.md
diff --git a/agents/ai-obfuscator.md b/.kimi-code/agents/ai-obfuscator.md
similarity index 100%
rename from agents/ai-obfuscator.md
rename to .kimi-code/agents/ai-obfuscator.md
diff --git a/.kimi-code/agents/architect-review.md b/.kimi-code/agents/architect-review.md
new file mode 100644
index 0000000..99420ab
--- /dev/null
+++ b/.kimi-code/agents/architect-review.md
@@ -0,0 +1,18 @@
+---
+name: architect-review
+description: Structural integrity reviewer (light/hard modes, backend + frontend)
+whenToUse: Phase end, big refactor, or task files declaring `review: hard`
+tools:
+  - Read
+  - Grep
+  - Glob
+  - Bash
+  - WebSearch
+  - FetchURL
+---
+
+You are the "architect-review" specialist agent. Before doing anything, read your skill file at ~/.kimi-code/skills/architect-review/SKILL.md and follow it exactly — it is the single source of truth for your workflow, conventions and quality bar.
+
+Rules inherited from the user environment: concise Russian-friendly reporting to the caller, production-grade code standards, no auto-commits, no license-unsafe dependencies.
+
+Your final message is the complete, self-contained handoff for the caller: state what you did, key file paths, verification results, and anything still open. The caller cannot see your intermediate steps.
diff --git a/agents/audit-orchestrator.md b/.kimi-code/agents/audit-orchestrator.md
similarity index 100%
rename from agents/audit-orchestrator.md
rename to .kimi-code/agents/audit-orchestrator.md
diff --git a/agents/coding-challenge-orchestrator.md b/.kimi-code/agents/coding-challenge-orchestrator.md
similarity index 100%
rename from agents/coding-challenge-orchestrator.md
rename to .kimi-code/agents/coding-challenge-orchestrator.md
diff --git a/agents/documentation-writer.md b/.kimi-code/agents/documentation-writer.md
similarity index 100%
rename from agents/documentation-writer.md
rename to .kimi-code/agents/documentation-writer.md
diff --git a/agents/dotnet-architect.md b/.kimi-code/agents/dotnet-architect.md
similarity index 100%
rename from agents/dotnet-architect.md
rename to .kimi-code/agents/dotnet-architect.md
diff --git a/agents/dotnet-code-reviewer.md b/.kimi-code/agents/dotnet-code-reviewer.md
similarity index 100%
rename from agents/dotnet-code-reviewer.md
rename to .kimi-code/agents/dotnet-code-reviewer.md
diff --git a/agents/dotnet-coder.md b/.kimi-code/agents/dotnet-coder.md
similarity index 100%
rename from agents/dotnet-coder.md
rename to .kimi-code/agents/dotnet-coder.md
diff --git a/agents/dotnet-init.md b/.kimi-code/agents/dotnet-init.md
similarity index 100%
rename from agents/dotnet-init.md
rename to .kimi-code/agents/dotnet-init.md
diff --git a/agents/frontend-designer.md b/.kimi-code/agents/frontend-designer.md
similarity index 100%
rename from agents/frontend-designer.md
rename to .kimi-code/agents/frontend-designer.md
diff --git a/agents/maui-coder.md b/.kimi-code/agents/maui-coder.md
similarity index 100%
rename from agents/maui-coder.md
rename to .kimi-code/agents/maui-coder.md
diff --git a/agents/next-js-architect.md b/.kimi-code/agents/next-js-architect.md
similarity index 100%
rename from agents/next-js-architect.md
rename to .kimi-code/agents/next-js-architect.md
diff --git a/agents/next-js-code-reviewer.md b/.kimi-code/agents/next-js-code-reviewer.md
similarity index 100%
rename from agents/next-js-code-reviewer.md
rename to .kimi-code/agents/next-js-code-reviewer.md
diff --git a/agents/next-js-coder.md b/.kimi-code/agents/next-js-coder.md
similarity index 100%
rename from agents/next-js-coder.md
rename to .kimi-code/agents/next-js-coder.md
diff --git a/agents/task-orchestrator.md b/.kimi-code/agents/task-orchestrator.md
similarity index 100%
rename from agents/task-orchestrator.md
rename to .kimi-code/agents/task-orchestrator.md
diff --git a/agents/tutor-master.md b/.kimi-code/agents/tutor-master.md
similarity index 100%
rename from agents/tutor-master.md
rename to .kimi-code/agents/tutor-master.md
diff --git a/config.toml b/.kimi-code/config.toml
similarity index 100%
rename from config.toml
rename to .kimi-code/config.toml
diff --git a/skills/ai-code-detector/SKILL.md b/.kimi-code/skills/ai-code-detector/SKILL.md
similarity index 100%
rename from skills/ai-code-detector/SKILL.md
rename to .kimi-code/skills/ai-code-detector/SKILL.md
diff --git a/skills/ai-obfuscation-reviewer/SKILL.md b/.kimi-code/skills/ai-obfuscation-reviewer/SKILL.md
similarity index 100%
rename from skills/ai-obfuscation-reviewer/SKILL.md
rename to .kimi-code/skills/ai-obfuscation-reviewer/SKILL.md
diff --git a/skills/ai-obfuscator/SKILL.md b/.kimi-code/skills/ai-obfuscator/SKILL.md
similarity index 100%
rename from skills/ai-obfuscator/SKILL.md
rename to .kimi-code/skills/ai-obfuscator/SKILL.md
diff --git a/.kimi-code/skills/architect-review/SKILL.md b/.kimi-code/skills/architect-review/SKILL.md
new file mode 100644
index 0000000..ba54b98
--- /dev/null
+++ b/.kimi-code/skills/architect-review/SKILL.md
@@ -0,0 +1,66 @@
+---
+name: architect-review
+description: |
+  Structural integrity review of the whole solution: dependency direction, layer boundaries,
+  service seams, FSD boundaries on frontend. Two modes — light (per Complex task) and hard
+  (phase end / big refactor). Use when a task file declares `review: hard`, at the end of
+  a sprint phase, or after a structural refactoring.
+type: prompt
+whenToUse: |
+  End of sprint phase, big refactor (>10 files, structural shift), or any task file with
+  `review: hard` in frontmatter. Light mode runs as part of Phase 3 review on Complex tasks.
+---
+
+# Architect Review Skill
+
+Reviews STRUCTURE, not line-level code (that's `dotnet-code-reviewer` / `next-js-coder-reviewer`).
+Question being answered: "Is the system still shaped the way CONTEXT.md / STRUCTURE.md say it should be?"
+
+## Inputs (read in this order)
+1. `CONTEXT.md` — declared architecture, service boundaries, communication rules
+2. `STRUCTURE.md` — folder conventions, Do/Don't
+3. `TECHNICAL-SOLUTIONS.md` — existing ADRs (never contradict silently)
+4. `BACKLOG.md` + current `TASKS/nnn-slug/` — what was actually built this phase
+5. Task frontmatter `review:` field — `light` (default) or `hard`
+
+## Mode Selection
+- Task file says `review: hard` → Hard checklist.
+- Phase end / structural refactor (>10 files, new service, module split) → Hard checklist.
+- Everything else (Complex tasks in Phase 3) → Light checklist.
+
+## Light Checklist (per Complex task)
+- [ ] **Dependency direction:** Domain → Application → Infrastructure → API. No reverse or skip-layer references.
+- [ ] **Layer leaks:** domain entities don't escape to API; infrastructure types don't leak into Application.
+- [ ] **God-services:** no new service with >5 public methods; no class accumulating unrelated responsibilities.
+- [ ] **File placement:** new files land in the layers/folders STRUCTURE.md prescribes (not "where it was convenient").
+- [ ] **No new cyclic dependencies** between projects/modules (`dotnet list reference` / project graph check).
+- [ ] **Frontend (if touched):** FSD boundaries respected — `features/` don't import from each other cross-domain, `shared/` stays dependency-free, no direct backend calls from `'use client'` (BFF rule).
+
+## Hard Checklist (phase end / big refactor) — everything in Light, plus:
+- [ ] **Module boundaries:** each module/service has a single clear purpose; public surface is minimal and intentional.
+- [ ] **Coupling audit:** fan-in/fan-out hotspots; types referenced from 5+ modules are candidates for `shared` or re-seaming.
+- [ ] **Cohesion audit:** files that change together live together; scatter (one feature touching 6+ unrelated folders) = smell.
+- [ ] **ADR coverage:** every structural decision made this phase has an ADR in TECHNICAL-SOLUTIONS.md; contradictions between ADRs and actual code are flagged.
+- [ ] **Split/merge proposals:** concrete recommendations with effort estimate (S/M/L) — not abstract "consider refactoring".
+- [ ] **Frontend (if present):** FSD layer graph (`app` → `pages` → `widgets` → `features` → `shared`) has no upward imports; query-key factories centralized; no BFF bypass.
+
+## Output
+Artifact: `architect-review-YYYY-MM-DD.md` (artifact provenance rule) or a section in the review report when invoked inside Phase 3.
+For each finding:
+1. **Severity:** `BLOCKER` (broken boundary, reverse dependency) / `WARNING` (erosion, will bite later) / `SUGGESTION` (re-seam opportunity)
+2. **Location:** project/module/folder
+3. **Problem:** which declared rule (CONTEXT.md / STRUCTURE.md / ADR) is violated
+4. **Fix:** concrete structural change (move X to Y, introduce seam Z)
+
+Findings that change architecture → propose ADR entry in `TECHNICAL-SOLUTIONS.md` (user approves, never write silently).
+
+## Communication
+- Speak Russian with the user; artifact content in English.
+- No auto-commits. Report changed/created files and ask about commit.
+
+## Output Contract (MANDATORY)
+End with the fixed closing block, then STOP:
+1. **Done** — summary + artifact paths.
+2. **Next step** — recommended follow-up per `SKILLFLOW.md`.
+3. **Alternatives** — 2–3 other valid follow-ups.
+Never auto-proceed.
diff --git a/skills/audit-orchestrator/SKILL.md b/.kimi-code/skills/audit-orchestrator/SKILL.md
similarity index 92%
rename from skills/audit-orchestrator/SKILL.md
rename to .kimi-code/skills/audit-orchestrator/SKILL.md
index 2d0f5c3..1fb8aa6 100644
--- a/skills/audit-orchestrator/SKILL.md
+++ b/.kimi-code/skills/audit-orchestrator/SKILL.md
@@ -109,7 +109,7 @@ Check the entire codebase against project rules:
 - [ ] **Migration backlog:** Pending EF migrations not applied. Migration naming convention followed.
 - [ ] **Package versions:** Outdated NuGet/npm packages with known vulnerabilities.
 - [ ] **License time-bombs:** MassTransit v8 installations flagged with migration plan (support ends after 2026; evaluate OpenTransit readiness).
-- [ ] **Documentation drift:** `CONTEXT.md` / `BACKLOG.md` / `STRUCTURE.md` out of sync with actual code. BACKLOG.md index statuses match task files in `TASK/` (no content duplication between index and files).
+- [ ] **Documentation drift:** `CONTEXT.md` / `BACKLOG.md` / `STRUCTURE.md` out of sync with actual code. BACKLOG.md index statuses match feature folders in `TASKS/` (no content duplication between index and folders).
 
 ### Messaging Audit (if triggered)
 - [ ] **Transport:** RabbitMQ/ASB configured with retry, dead-letter, and outbox (via Wolverine/Rebus; MassTransit only in legacy).
@@ -131,6 +131,10 @@ Run against `documentation-writer` skill rules:
 - [ ] **Consistency:** No contradictions between `CONTEXT.md`, `STRUCTURE.md`, `TECHNICAL-SOLUTIONS.md`, `AGENTS.md`.
 - [ ] **Language:** Each doc follows the language convention from the `AGENTS.md` documentation table.
 - [ ] **Drift:** Docs describing features changed since last audit were updated in the same commits.
+- [ ] **Index/folders sync:** `BACKLOG.md` index rows match existing `TASKS/nnn-slug/` folders (no orphans either way).
+- [ ] **Feature folder integrity:** no loose task files outside `TASKS/nnn-slug/tasks/`; no `spec.md` without at least one task file; no task file whose `<refs>` points to a missing spec/plan.
+- [ ] **Frozen artifacts:** `spec.md`/`plan.md` with `status: approved` were not edited after approval without a `version:` bump (check git history) — silent edits of frozen artifacts are a WARNING.
+- [ ] **Superpowers containment:** no `docs/superpowers/` directory exists as a source of truth — if found, flag as WARNING: content must be migrated into `TASKS/nnn-slug/spec.md` / `plan.md` / `CONTEXT.md` / `TECHNICAL-SOLUTIONS.md` per AGENTS.md «Superpowers Integration».
 
 ---
 
@@ -157,7 +161,7 @@ After completing all audit types, generate a structured report:
 - **Problem:** [Description]
 - **Impact:** [Why this matters]
 - **Recommendation:** [Concrete fix or architectural change]
-- **Backlog link:** [If applicable, link to TASK/NNN-slug.md]
+- **Backlog link:** [If applicable, link to TASKS/nnn-slug/]
 
 ## Action Items
 | Priority | Task | Owner | Due | Linked Backlog Item |
diff --git a/skills/coding-challenge-orchestrator/SKILL.md b/.kimi-code/skills/coding-challenge-orchestrator/SKILL.md
similarity index 100%
rename from skills/coding-challenge-orchestrator/SKILL.md
rename to .kimi-code/skills/coding-challenge-orchestrator/SKILL.md
diff --git a/skills/documentation-writer/SKILL.md b/.kimi-code/skills/documentation-writer/SKILL.md
similarity index 100%
rename from skills/documentation-writer/SKILL.md
rename to .kimi-code/skills/documentation-writer/SKILL.md
diff --git a/skills/dotnet-architect/SKILL.md b/.kimi-code/skills/dotnet-architect/SKILL.md
similarity index 100%
rename from skills/dotnet-architect/SKILL.md
rename to .kimi-code/skills/dotnet-architect/SKILL.md
diff --git a/.kimi-code/skills/dotnet-best-practices/SKILL.md b/.kimi-code/skills/dotnet-best-practices/SKILL.md
new file mode 100644
index 0000000..7683215
--- /dev/null
+++ b/.kimi-code/skills/dotnet-best-practices/SKILL.md
@@ -0,0 +1,77 @@
+---
+name: dotnet-best-practices
+description: |
+  Codified .NET engineering rules (BP-IDs) consumed by dotnet-coder during implementation
+  and by dotnet-code-reviewer as the review checklist. Single source of enforceable code-quality rules.
+type: prompt
+whenToUse: |
+  During any .NET implementation (coder loads it with the task file) and any .NET code review
+  (reviewer cites violations by BP-ID).
+---
+
+# .NET Best Practices — Codified Rules
+
+Each rule has a stable BP-ID. Reviewer cites violations as `BP-NN (severity)`.
+Severity: **[B]** BLOCKER, **[W]** WARNING, **[S]** SUGGESTION.
+
+## Async
+- **BP-01 [B]** No `async void` except event handlers. No `.Result` / `.Wait()` / `.GetAwaiter().GetResult()` — deadlock risk.
+- **BP-02 [W]** `CancellationToken` parameter on every async method that does I/O; pass it through the whole call chain, never swallow it.
+- **BP-03 [W]** No `async` wrapper around a single inner call without adding value — return the `Task` directly.
+- **BP-04 [S]** `ConfigureAwait(false)` in library code only; not needed in ASP.NET Core app code.
+
+## Dependency Injection
+- **BP-10 [B]** Microsoft.Extensions.DependencyInjection only — no external containers, no service-locator patterns.
+- **BP-11 [B]** No `new` for services registered in DI; no injecting `IServiceProvider` to resolve manually (except factory edge cases, justified in code-adjacent ADR).
+- **BP-12 [W]** Correct lifetimes: DbContext and Unit-of-Work = Scoped; stateless/expensive-to-build = Singleton; per-operation state = Transient. Captive dependencies (singleton holding scoped) = BLOCKER.
+- **BP-13 [W]** Constructor injection only; primary constructors allowed. No property injection.
+
+## EF Core & Data
+- **BP-20 [B]** No N+1: use `Include`/`ThenInclude` or projections. A loop issuing queries = automatic BLOCKER.
+- **BP-21 [B]** Read paths use `AsNoTracking()` unless the entity is modified in the same scope.
+- **BP-22 [W]** Migrations in a separate assembly for modular monoliths; never edit applied migrations — add a new one.
+- **BP-23 [W]** No raw SQL without parameterization; `FromSqlRaw` with interpolated values = BLOCKER (SQL injection).
+- **BP-24 [S]** Prefer compiled queries only on proven hot paths — measure first, don't gold-plate.
+
+## API Surface
+- **BP-30 [W]** Minimal APIs or Carter modules; heavy controllers are legacy-only. Thin handlers: HTTP in/out, logic in Application layer.
+- **BP-31 [B]** DTOs at the boundary — domain entities never leave the API layer. Manual mapping or Mapperly (see `nuget-licensing-guard`).
+- **BP-32 [W]** Consistent error shape: ProblemDetails for all error responses; no raw exception text leaking to clients.
+- **BP-33 [W]** Pagination on every collection endpoint; no unbounded `ToListAsync()` exposed via API.
+
+## Validation & Errors
+- **BP-40 [W]** FluentValidation at the Application boundary; validators stay out of domain entities.
+- **BP-41 [W]** Domain errors as results/explicit error types where the flow expects them; exceptions for truly exceptional paths. No empty `catch {}` — ever [B if found].
+- **BP-42 [S]** Guard clauses at method entry for null/invalid args (`ArgumentNullException.ThrowIfNull`); don't nest validation deep.
+
+## Logging & Observability
+- **BP-50 [W]** Serilog structured logging with message templates (`Log.Information("Order {OrderId} processed", id)`) — no string interpolation in templates.
+- **BP-51 [B]** No PII, credentials, tokens, or connection strings in logs.
+- **BP-52 [S]** Log at the right level: Error = actionable failure, Warning = recovered anomaly, Information = business-meaningful event. No Debug-spam in committed code.
+
+## Testing
+- **BP-60 [W]** xUnit + Shouldly + NSubstitute (see AGENTS.md stack). Naming: `Method_Scenario_Expectation` or project convention — but consistent within the test project.
+- **BP-61 [W]** TDD per AGENTS.md «Superpowers Integration» rule 4: domain/business logic gets red-green-refactor; migrations/DI/config/DTOs are exempt.
+- **BP-62 [W]** Tests are independent: no shared mutable state, no order dependence, no hitting real DB/network in unit tests.
+- **BP-63 [S]** One logical assertion per test; multiple Shouldly checks on one outcome are fine, multiple scenarios are not.
+
+## Performance
+- **BP-70 [W]** Hot paths: no LINQ allocations in tight loops without measurement; `Span<T>`/`ArrayPool` only where profiling justifies.
+- **BP-71 [W]** `IAsyncEnumerable` for streaming large result sets instead of materializing lists.
+- **BP-72 [S]** Response caching/output cache where the endpoint is read-heavy and invalidation is trivial.
+
+## Security
+- **BP-80 [B]** No secrets in code, config committed to git, or logs. User Secrets / env vars / vault only.
+- **BP-81 [B]** Authorization explicit on every endpoint — no anonymous-by-default on new endpoints.
+- **BP-82 [B]** All input validated; all SQL parameterized (BP-23); no `string.Format` into commands, URLs to internal services, or file paths from user input.
+
+## Architecture & Style
+- **BP-90 [B]** Clean Architecture dependency direction: Domain ← Application ← Infrastructure/API. An inward-to-outward reference = BLOCKER.
+- **BP-91 [W]** No God-Services: a service exceeding ~300 lines or >5 dependencies is a split candidate — reviewer flags, human decides.
+- **BP-92 [W]** Mediator (martinothamar) for CQRS dispatch; Wolverine for messaging — version-pinned rules in `nuget-licensing-guard`.
+- **BP-93 [S]** API, DTO, Service in separate files. No `#region`. No comments restating code. Match surrounding human style (AI Coding Traces rules apply).
+
+## Usage
+- **Coder:** load with the task file; rules are constraints, not aspirations.
+- **Reviewer:** checklist = this file + task `<acceptance-criteria>` + spec-compliance. Cite as `BP-NN [B/W/S] — file:line — what's wrong — fix diff`.
+- New recurring rule discovered during work → propose adding it here (reflect loop), don't patch it silently into one review.
diff --git a/skills/dotnet-code-reviewer/SKILL.md b/.kimi-code/skills/dotnet-code-reviewer/SKILL.md
similarity index 95%
rename from skills/dotnet-code-reviewer/SKILL.md
rename to .kimi-code/skills/dotnet-code-reviewer/SKILL.md
index c2a5b90..6338782 100644
--- a/skills/dotnet-code-reviewer/SKILL.md
+++ b/.kimi-code/skills/dotnet-code-reviewer/SKILL.md
@@ -21,6 +21,8 @@ Before reviewing code, read project documentation in this strict order:
 
 ## Review Checklist (MUST CHECK)
 
+**BP-IDs are the primary checklist.** Load `~/.kimi-code/skills/dotnet-best-practices/SKILL.md` and check every applicable rule; cite violations as `BP-NN (severity)` in the review output. The checklist below complements, not replaces, BP-IDs.
+
 ### Architecture & Layers
 - [ ] **Clean Architecture respected.** Domain has no infra dependencies. Application has no API dependencies.
 - [ ] **No business logic in API layer.** Controllers/Minimal API endpoints are thin.
diff --git a/skills/dotnet-coder/SKILL.md b/.kimi-code/skills/dotnet-coder/SKILL.md
similarity index 97%
rename from skills/dotnet-coder/SKILL.md
rename to .kimi-code/skills/dotnet-coder/SKILL.md
index 5377404..eacd75b 100644
--- a/skills/dotnet-coder/SKILL.md
+++ b/.kimi-code/skills/dotnet-coder/SKILL.md
@@ -19,6 +19,7 @@ Before writing or editing ANY code, read project documentation in this strict or
 2. `Backlog.md` — Current sprint tasks and execution order (Mixed EN/RU)
 3. `STRUCTURE.md` — Code style, architecture rules, Do/Don't (English)
 4. `AGENTS.md` — Tech stack, build commands, conventions (English)
+5. `~/.kimi-code/skills/dotnet-best-practices/SKILL.md` — codified BP-IDs; enforceable during implementation. When executing a `TASKS/` task file, load this together with the task file.
 
 ## Tech Stack
 - **Runtime**: .NET 9/10+ (latest LTS)
diff --git a/skills/dotnet-init/SKILL.md b/.kimi-code/skills/dotnet-init/SKILL.md
similarity index 100%
rename from skills/dotnet-init/SKILL.md
rename to .kimi-code/skills/dotnet-init/SKILL.md
diff --git a/skills/frontend-designer/SKILL.md b/.kimi-code/skills/frontend-designer/SKILL.md
similarity index 100%
rename from skills/frontend-designer/SKILL.md
rename to .kimi-code/skills/frontend-designer/SKILL.md
diff --git a/skills/maui-coder/SKILL.md b/.kimi-code/skills/maui-coder/SKILL.md
similarity index 100%
rename from skills/maui-coder/SKILL.md
rename to .kimi-code/skills/maui-coder/SKILL.md
diff --git a/skills/next-js-architect/SKILL.md b/.kimi-code/skills/next-js-architect/SKILL.md
similarity index 100%
rename from skills/next-js-architect/SKILL.md
rename to .kimi-code/skills/next-js-architect/SKILL.md
diff --git a/skills/next-js-code-reviewer/SKILL.md b/.kimi-code/skills/next-js-code-reviewer/SKILL.md
similarity index 100%
rename from skills/next-js-code-reviewer/SKILL.md
rename to .kimi-code/skills/next-js-code-reviewer/SKILL.md
diff --git a/skills/next-js-coder/SKILL.md b/.kimi-code/skills/next-js-coder/SKILL.md
similarity index 100%
rename from skills/next-js-coder/SKILL.md
rename to .kimi-code/skills/next-js-coder/SKILL.md
diff --git a/skills/nuget-licensing-guard/SKILL.md b/.kimi-code/skills/nuget-licensing-guard/SKILL.md
similarity index 100%
rename from skills/nuget-licensing-guard/SKILL.md
rename to .kimi-code/skills/nuget-licensing-guard/SKILL.md
diff --git a/skills/task-orchestrator/SKILL.md b/.kimi-code/skills/task-orchestrator/SKILL.md
similarity index 64%
rename from skills/task-orchestrator/SKILL.md
rename to .kimi-code/skills/task-orchestrator/SKILL.md
index 28a3819..2adc5d3 100644
--- a/skills/task-orchestrator/SKILL.md
+++ b/.kimi-code/skills/task-orchestrator/SKILL.md
@@ -29,9 +29,9 @@ Before proposing ANY changes, read documentation in this exact order:
 3. **`BACKLOG.md`** — Sprint backlog, tasks per phase, execution order, blocked items (Mixed EN/RU)
 4. **`STRUCTURE.md`** — Project structure, code style, Do/Don't (English)
 5. **`TECHNICAL-SOLUTIONS.md`** — Architecture Decision Records, trade-offs, historical context (English)
-6. **`TASK/`** — after identifying the current task in BACKLOG.md (Step 2), read its file `TASK/NNN-slug.md` IN FULL. The task file is the authoritative spec; BACKLOG.md is only the index.
+6. **`TASKS/`** — after identifying the current feature in BACKLOG.md (Step 2), open its folder `TASKS/nnn-slug/`. Orchestrator reads `spec.md` and `plan.md` (if present); the executing sub-agent reads ONLY its `tasks/NN-foo.md` IN FULL. Task files are the authoritative execution contracts; BACKLOG.md is only the index.
 
-**Naming rule:** filenames are exact and case-sensitive — always ALL CAPS as listed above.
+**Naming rule:** root filenames are exact and case-sensitive — always ALL CAPS as listed above. Inside feature folders, files are lowercase: `spec.md`, `plan.md`, `tasks/NN-foo.md`.
 
 If any of these files are missing, report which ones are absent and ask the user if they should be created.
 
@@ -42,12 +42,12 @@ After reading documentation, determine the working mode:
 - We are **LOCKED to the backlog scope**. No new features, no scope changes, no "while we're at it" suggestions.
 - Identify the **first unchecked task** (`[ ]`) or task in progress (`[~]`).
 - If the task is marked `[?]` (Blocked / Needs Human) — **STOP**. Report to user: "Задача заблокирована: [описание блокера]. Требуется решение человека."
-- Load the task file `TASK/NNN-slug.md`. Its `<anti-scope>` section is hard constraints — do NOT touch anything listed there. `<acceptance-criteria>` becomes the Phase 3 review checklist.
-- **Never rewrite BACKLOG.md or task files without explicit user approval.** Only update statuses in the BACKLOG.md index (`[ ]` → `[~]` → `[x]`) and append to `<log>` / `<obstacles>` in the task file.
+- Open the feature folder `TASKS/nnn-slug/`, then load the current task file `tasks/NN-foo.md`. Its `<anti-scope>` section is hard constraints — do NOT touch anything listed there. `<acceptance-criteria>` + spec-compliance becomes the Phase 3 review checklist.
+- **Never rewrite BACKLOG.md, spec.md, plan.md or task files without explicit user approval.** Only update statuses in the BACKLOG.md index (`[ ]` → `[~]` → `[x]`), task-file frontmatter `status:`, and append to `<log>` / `<obstacles>`. Approved spec.md/plan.md are frozen — changes require re-validation with the user.
 
 **IF `BACKLOG.md` DOES NOT EXIST or is empty:**
 - Ask the user: "Бэклог не найден или пуст. Создать задачу '[описание]'?"
-- On approval for a Complex task: create `TASK/NNN-slug.md` from `TASK/_TEMPLATE.md` AND add an index row to BACKLOG.md. Simple tasks stay one-liners in BACKLOG.md.
+- On approval for a Complex task: create the feature folder `TASKS/nnn-slug/` from `TASKS/_TEMPLATE/` AND add an index row to BACKLOG.md. Simple tasks stay one-liners in BACKLOG.md.
 - Do NOT start coding until scope is defined and written down.
 
 **IF user asks for something OUTSIDE BACKLOG.md:**
@@ -67,9 +67,17 @@ For ANY complex task (more than 3 files changed, or cross-layer changes, or new
 
 **Exception:** pure documentation tasks (`documentation-writer`) and learning-material tasks (`tutor-master`) skip Phase 3 code review — apply the target skill's own verification checklist instead.
 
+### Phase 0: Spec & Plan Gates (Complex features — MANDATORY, sequential)
+1. **Spec gate.** Build `TASKS/nnn-slug/spec.md` from `TASKS/_TEMPLATE/spec.md` — use `superpowers:brainstorming` as the questioning engine (Socratic, one question at a time) until requirements are unambiguous. **STOP. Present spec.md to the user. No planning until explicit approval.**
+2. **Plan gate.** Build `TASKS/nnn-slug/plan.md` from `TASKS/_TEMPLATE/plan.md` — decomposition style per `superpowers:writing-plans` (small tasks, exact file paths, verification step per task). **STOP. Present plan.md to the user. No task breakdown until explicit approval.**
+3. **Task breakdown.** After plan approval: create `TASKS/nnn-slug/tasks/NN-foo.md` per task from `TASKS/_TEMPLATE/task.md`, each linking back to spec.md/plan.md via `<refs>`. Add/update the BACKLOG.md index row.
+4. Approved spec.md/plan.md become **frozen snapshots**. Any change mid-flight → re-validate with the user, bump `version:` in frontmatter, never edit silently.
+5. Medium features (no validation ceremony needed) MAY skip spec/plan — folder with `tasks/` only. Simple tasks never get a folder.
+
 ### Phase 1: Architecture (if needed)
 - If task involves new routes, API contracts, or service boundaries → consult `dotnet-architect` or `next-js-architect` first.
 - Output: updated `CONTEXT.md`, folder structure, API contract.
+- **Requirements unclear or feature under-specified?** → return to Phase 0 spec gate: `superpowers:brainstorming` as the questioning engine (Socratic, one question at a time). Output lands in `TASKS/nnn-slug/spec.md` / `CONTEXT.md` — NEVER in `docs/superpowers/`.
 
 ### Phase 2: Implementation
 - Delegate work to the appropriate skill:
@@ -80,8 +88,11 @@ For ANY complex task (more than 3 files changed, or cross-layer changes, or new
   - `Learning materials` (study plans, concept explanations, assessments) → `tutor-master`
 - Coder MUST follow all rules from their skill file.
 - Coder MUST output list of changed files.
-- Coder receives the task file `TASK/NNN-slug.md` as primary input. Coder MUST append discovered workarounds, quirks, and environment issues to the task file's `<obstacles>` section, and a one-line entry to `<log>`.
+- Coder receives ONLY its task file `TASKS/nnn-slug/tasks/NN-foo.md` as primary input (spec.md/plan.md stay with orchestrator and reviewer). Coder MUST append discovered workarounds, quirks, and environment issues to the task file's `<obstacles>` section, and a one-line entry to `<log>`.
 - **Before adding/upgrading any third-party NuGet dependency** → consult `nuget-licensing-guard`.
+- **Decomposition style** → `superpowers:writing-plans` (small tasks, exact file paths, verification steps per task); the plan itself lives in `TASKS/nnn-slug/plan.md` (Phase 0 plan gate).
+- **TDD** → `superpowers:test-driven-development` applies ONLY to domain/business logic per AGENTS.md «Superpowers Integration» rule 4. Its commit step is DISABLED — see Git & Commit Control below.
+- **Stuck on a bug?** → `superpowers:systematic-debugging` (root cause first, no guess-fixes) before any fix attempt.
 
 ### Phase 3: Mandatory Review (AUTO-TRIGGER)
 After Phase 2 completion, run reviews in this order:
@@ -91,9 +102,13 @@ After Phase 2 completion, run reviews in this order:
 - `Next.js code changed` → apply `next-js-coder-reviewer` checklist
 - `MAUI code changed` → apply `dotnet-code-reviewer` checklist + `maui-coder` rules (MVVM, code-behind purity, platform isolation)
 
+**Review depth from the task file:** read the `review:` frontmatter field of the executed task (`light` default / `hard`).
+- `light` → matching code reviewer checklist only.
+- `hard` → code reviewer + `architect-review` (Light checklist, both stacks) in the same Phase 3 pass.
+
 **CONDITIONALLY run additional reviews:**
 - **Security-related changes?** (auth, new endpoints, config, secrets, PII handling) → run `dotnet-code-reviewer` Security section with elevated priority.
-- **Large refactoring / Phase completion?** (>10 files, architectural shift, monolith split) → run `architect-review` (check global structure integrity, dependency direction, service boundaries).
+- **Large refactoring / Phase completion?** (>10 files, architectural shift, monolith split) → run `architect-review` Hard checklist (global structure integrity, dependency direction, service boundaries, ADR coverage).
 - **Both stacks changed?** → run BOTH `dotnet-code-reviewer` and `next-js-coder-reviewer` sequentially.
 
 Review output must include:
@@ -101,6 +116,10 @@ Review output must include:
 2. File:line references
 3. Concrete fix diffs
 
+**Two-stage review (per superpowers):** first stage — spec-compliance against `TASKS/nnn-slug/spec.md` + `<acceptance-criteria>`; second stage — code quality. Spec violation is a BLOCKER even if the code is clean.
+
+**Before reporting ANY task as done** → apply `superpowers:verification-before-completion`: prove the fix/feature works (build + tests green, behavior demonstrated), never claim completion on "should work now".
+
 ### Phase 4: Fix Loop
 - **If Security BLOCKER found:** STOP pipeline immediately. Do NOT auto-fix. Present to user with `[SECURITY]` tag and require human approval. Security vulnerabilities (injection, hardcoded secrets, broken auth) must be reviewed by a human before any code changes.
 - **If regular BLOCKERS exist:** return to Phase 2 (coder) with review comments as input.
@@ -108,8 +127,8 @@ Review output must include:
 
 ### Phase 5: Architecture Gate (End-of-Phase only)
 - Run ONLY after completing a sprint phase or large refactoring.
-- Verify: Clean Architecture layers intact, no dependency inversions, service boundaries respected.
-- Output: `TECHNICAL-SOLUTIONS.md` update (new ADR entry) if structural changes occurred.
+- Executed by `architect-review` (Hard checklist): layers intact, no dependency inversions, service boundaries respected, ADR coverage.
+- Output: `architect-review-YYYY-MM-DD.md` + `TECHNICAL-SOLUTIONS.md` update (new ADR entry) if structural changes occurred — user approves ADR writes.
 
 ---
 
@@ -120,6 +139,7 @@ Review output must include:
 - **After completing ALL phases, report the list of changed files** and ask: "Изменены файлы: [список]. Хотите сделать коммит?"
 - If the user says nothing about commit — leave changes unstaged/uncommitted.
 - **Orchestrator MUST enforce this rule:** if any sub-skill attempts to commit, stop it and report to user.
+- **Superpowers skills contain built-in commit steps (spec commit, TDD cycle commit, branch finishing). These are DISABLED.** This file's no-auto-commits rule overrides any skill instruction, including `superpowers:*`.
 
 ---
 
@@ -149,7 +169,7 @@ Task is COMPLEX if:
 
 ## Communication Rules
 - **Speak Russian.**
-- **At start:** Report what was found in documentation: "Бэклог найден, текущая задача: [X] (`TASK/NNN-slug.md`). Anti-Scope: [Y]. Скоуп зафиксирован."
+- **At start:** Report what was found in documentation: "Бэклог найден, текущая фича: [X] (`TASKS/nnn-slug/`), задача: `tasks/NN-foo.md`. Anti-Scope: [Y]. Скоуп зафиксирован."
 - **During work:** Report progress after each phase: "Фаза 1 завершена...", "Фаза 2: изменены файлы...", "Фаза 3: ревью..."
 - **If scope violation detected:** "Этот запрос выходит за рамки бэклога. Добавить задачу?"
 - **If blocked task:** "Задача [N] заблокирована: [причина]. Требуется решение человека."
diff --git a/skills/tutor-master/SKILL.md b/.kimi-code/skills/tutor-master/SKILL.md
similarity index 100%
rename from skills/tutor-master/SKILL.md
rename to .kimi-code/skills/tutor-master/SKILL.md
diff --git a/AGENTS.md b/AGENTS.md
index 5de410a..b2ba2f2 100644
--- a/AGENTS.md
+++ b/AGENTS.md
@@ -105,14 +105,23 @@ Use `SOCIAL_DICTIONARY.md` to speak with user. Addressing terms, dev slang, пи
 | File | Purpose | Language | When to Read |
 |------|---------|----------|-------------|
 | `CONTEXT.md` | System design: architecture decisions, service roles, communication rules, AI Framework details, development phases, MCP server design | English | First — for all architectural context |
-| `BACKLOG.md` | Task board INDEX: ordered task list with statuses (`[ ]`/`[~]`/`[x]`/`[?]`), deferred items, execution order. Task CONTENT never lives here — only one-line titles and links to task files | Mixed (EN headers, RU descriptions) | Before starting work — to understand what's done, what's planned, and which task file to load |
-| `TASK/NNN-slug.md` | Self-contained task spec with XML structure (`<goal>`, `<scope>`, `<anti-scope>`, `<constraints>`, `<acceptance-criteria>`, `<refs>`, `<obstacles>`, `<log>`). Template: `TASK/_TEMPLATE.md`. Only for Complex tasks — Simple tasks stay one-liners in BACKLOG.md | English | Read IN FULL before executing the task — it is the only input an executing sub-agent needs |
+| `BACKLOG.md` | Task board INDEX: ordered feature list with statuses (`[ ]`/`[~]`/`[x]`/`[?]`), deferred items, execution order. CONTENT never lives here — one line per feature folder (`[~] 001-auth-refresh → TASKS/001-auth-refresh/`), plus one-liners for Simple tasks | Mixed (EN headers, RU descriptions) | Before starting work — to understand what's done, what's planned, and which feature folder to load |
+| `TASKS/nnn-slug/` | Atomic feature folder: `spec.md` (what/why — frozen after user approval), `plan.md` (how — frozen after user approval), `tasks/NN-foo.md` (execution contract per task: XML sections `<goal>`, `<scope>`, `<anti-scope>`, `<constraints>`, `<acceptance-criteria>`, `<refs>`, `<obstacles>`, `<log>`). spec/plan are optional (Complex features with validation); the folder is not. Templates: `TASKS/_TEMPLATE/`. Simple tasks stay one-liners in BACKLOG.md | English (spec/plan may be Mixed) | Orchestrator/reviewer read spec.md + plan.md; executing sub-agent reads ONLY its `tasks/NN-foo.md` — in full, before starting |
 | `STRUCTURE.md` | Project structure, code style guidelines, architecture rules, AI agent guidelines, Do/Don't | English | Before writing code — for conventions and patterns |
 | `TECHNICAL-SOLUTIONS.md` | Architecture Decision Records (ADRs): chosen technical solutions, trade-offs, reasons why X was chosen over Y | English | Before proposing alternative approaches or challenging existing patterns — to understand historical context |
+| `SKILLFLOW.md` | Workflow graph: phases, valid transitions, alternative routes (full cycle, quick-fix path, docs-only path). Shared by ALL skills — every skill reads it to suggest its «next step» per the output contract | English | When finishing any skill step (to pick the next step) and before starting a new workflow |
 | `AGENTS.md` | Project-level: tech stack, build commands, security, conventions for AI coding agents | English | For build/run commands and agent-specific rules |
 | `SOCIAL_DICTIONARY.md` | Social vocabulary: addressing terms, slang, dev slang, gachi core, roast rules, stop-words, tone rules | Russian | Before composing any user-facing message — for tone, slang and emotional markup |
 
-**Naming rule:** these filenames are exact and case-sensitive — always ALL CAPS (`CONTEXT.md`, not `context.md`).
+**Naming rule:** these filenames are exact and case-sensitive — always ALL CAPS (`CONTEXT.md`, not `context.md`; `TASKS/`, not `tasks/`). Inside feature folders, files are lowercase: `requirements.md`, `spec.md`, `plan.md`, `tasks/NN-foo.md`.
+
+**Skill output contract (MANDATORY for every skill/agent):** every skill ends with a fixed closing block, then STOPS:
+1. **Done** — one-paragraph summary of what was produced + artifact paths.
+2. **Next step** — the recommended follow-up per `SKILLFLOW.md` (exact command/skill name).
+3. **Alternatives** — 2–3 other valid follow-ups.
+The skill suggests, never auto-proceeds. The user picks the next step.
+
+**Artifact provenance rule (skill-prefix naming):** ANY file generated by a skill outside `TASKS/` MUST start with the producing skill's name in lowercase: `audit-YYYY-MM-DD.md` (audit-orchestrator), `review-NNN-01.md` (code reviewers), `obfuscation-*.md` (ai-obfuscator), `intake-*.md` (requirements intake). Source of every artifact must be visible from `ls` alone — no anonymous reports.
 
 **Rule for agents:** Before proposing an alternative approach (e.g., "let's use Redux instead of Zustand", "let's switch from Minimal APIs to Controllers"), read `TECHNICAL-SOLUTIONS.md`. If the topic is covered there — follow the documented decision. If you believe the decision is outdated — propose updating `TECHNICAL-SOLUTIONS.md` with new rationale, do not silently override.
 
@@ -143,6 +152,25 @@ Current prompt-only candidates to migrate when hooks are available:
 - **dotnet build after changes** → PostToolUse hook on Edit/Write of `*.cs`.
 - **License check** → PreToolUse hook on edits to `*.csproj` / `Directory.Packages.props`.
 
+## Superpowers Integration (plugin = technique library, NOT process owner)
+The `superpowers` plugin is installed. Its skills are used as TECHNIQUES inside this system's phases — they never own the process, the documents, or the git history. This file and `task-orchestrator` win every conflict.
+
+1. **Hierarchy.** `task-orchestrator` owns the pipeline (spec → plan → tasks → review gates). Superpowers skills plug into phases as sub-techniques:
+   - Spec questioning → `superpowers:brainstorming` (Socratic engine, one question at a time)
+   - Plan decomposition → `superpowers:writing-plans` (small tasks with verification steps)
+   - Execution → `superpowers:test-driven-development` (scope limited by rule 4)
+   - Debugging → `superpowers:systematic-debugging` (NO fix without root cause — always applies)
+   - Before claiming "done" → `superpowers:verification-before-completion` (always applies)
+   - `superpowers:brainstorming` has a hard gate that intercepts any "build/add/change" request — route it: Complex tasks go through `task-orchestrator` intake; brainstorming is used only for the questioning phase, not as a parallel pipeline.
+2. **Commit steps DISABLED.** Superpowers skills contain built-in commit steps (spec commit, TDD cycle commit, branch finishing). They are OFF. "No auto-commits" (Development Workflow, rule 6) overrides ANY skill instruction. Replace every commit step with: stop and report the changed-file list.
+3. **Document mapping (no parallel doc system).** `docs/superpowers/` is FORBIDDEN as a source of truth — never create it, never read specs from it. Map outputs onto this system instead:
+   - Brainstorm design output → `TASKS/nnn-slug/spec.md` (validated and approved by the user, then frozen)
+   - Plan decomposition → `TASKS/nnn-slug/plan.md` (validated by the user, then frozen), then broken into `TASKS/nnn-slug/tasks/NN-foo.md` files
+   - Architectural decisions surfaced during brainstorming → ADR entry in `TECHNICAL-SOLUTIONS.md`
+   - Cross-cutting context that outlives the feature → `CONTEXT.md`
+4. **TDD scope.** Strict red-green-refactor applies to domain/business logic and non-trivial algorithms. It does NOT apply to EF migrations, DI wiring, config, DTOs, or glue code. When in doubt — ask the user before choosing TDD on/off for a task.
+5. **Sub-agent spawns from superpowers skills** (e.g. subagent-driven-development) inherit the Sub-Agent Rules above: pool `Kimi K2.7`/`Kimi K3` only, `primary` alias forbidden, STOP-and-report on unavailability.
+
 ## AI Coding Traces (applies to ALL agents)
 Write code like a human engineer, not like a language model. Full catalog of signatures and humanization techniques lives in the `ai-obfuscator` skill — these are the standing rules for everyday work:
 
@@ -164,4 +192,4 @@ Its full rule set (workspace isolation, zero AI leakage, fixed pipeline, seniori
 4. **Review Trigger** — After complex task completion, agent MUST run matching reviewer skill before considering task done.
 5. **No task is "done" until review passes or user explicitly overrides.**
 6. **No auto-commits.** Agents must NOT run `git commit` or `git push` unless user explicitly asks.
-7. **Task files.** For Complex tasks, the spec lives in `TASK/NNN-slug.md` (template: `TASK/_TEMPLATE.md`). BACKLOG.md is the index only (statuses + order) — task content is never duplicated there. The executing sub-agent gets the task file as its primary input; `<acceptance-criteria>` is the review gate's checklist; any agent that hits a workaround/quirk MUST append it to the task file's `<obstacles>` section.
+7. **Feature folders.** Complex work lives in `TASKS/nnn-slug/`: `spec.md` → user validation gate → `plan.md` → user validation gate → `tasks/NN-foo.md` per task (templates: `TASKS/_TEMPLATE/`). spec.md/plan.md are READ-ONLY snapshots once approved — changes require re-validation with the user, never silent edits. BACKLOG.md is the index only (folder links + statuses + order) — content is never duplicated there. The executing sub-agent gets ONLY its task file as input; `<acceptance-criteria>` plus spec-compliance is the review gate; any agent that hits a workaround/quirk MUST append it to the task file's `<obstacles>` section.
diff --git a/TASK/_TEMPLATE.md b/TASK/_TEMPLATE.md
deleted file mode 100644
index 949b789..0000000
--- a/TASK/_TEMPLATE.md
+++ /dev/null
@@ -1,53 +0,0 @@
----
-id: 001
-slug: example-task
-status: todo        # todo | in-progress | blocked | done
-phase: 1
-created: YYYY-MM-DD
----
-
-# 001 — <Task title>
-
-<goal>
-Что должно быть достигнуто. Формулируй через РЕЗУЛЬТАТ, не через процесс (what, не how).
-</goal>
-
-<context>
-Почему задача существует, к какой фазе относится, связи с другими тасками (TASK/NNN).
-Субагент-исполнитель читает ТОЛЬКО этот файл — весь нужный контекст должен быть здесь
-или в явных ссылках <refs>.
-</context>
-
-<scope>
-- Что входит: конкретные файлы, слои, эндпоинты, компоненты
-</scope>
-
-<anti-scope>
-- Что НЕ трогаем ни при каких обстоятельствах (hard constraints)
-</anti-scope>
-
-<constraints>
-- Стек, версии, лимиты (производительность, бюджеты токенов/времени)
-- Лицензионные проверки — через nuget-licensing-guard
-</constraints>
-
-<acceptance-criteria>
-- [ ] Проверяемый критерий (тест, команда, наблюдаемое поведение)
-- [ ] Этот список — чек-лист для review-гейта
-</acceptance-criteria>
-
-<refs>
-- @CONTEXT.md — какая секция
-- @TECHNICAL-SOLUTIONS.md — какой ADR
-- TASK/NNN — зависимые задачи
-</refs>
-
-<obstacles>
-<!-- Заполняется исполнителями ПО ХОДУ работы: workaround'и, кривые флаги,
-     проблемные зависимости, особенности окружения.
-     Следующий агент не должен переоткрывать эти грабли. -->
-</obstacles>
-
-<log>
-<!-- Одна строка на запись: YYYY-MM-DD — кто (агент/скилл) — что сделано. -->
-</log>
diff --git a/docs/superpowers/plans/2026-08-31-dual-tool-repo-layout.md b/docs/superpowers/plans/2026-08-31-dual-tool-repo-layout.md
deleted file mode 100644
index dd6a151..0000000
--- a/docs/superpowers/plans/2026-08-31-dual-tool-repo-layout.md
+++ /dev/null
@@ -1,489 +0,0 @@
-# Dual-Tool Repo Layout (.kimi-code + .claude) Implementation Plan
-
-> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.
-
-**Goal:** Restructure the repo into `.kimi-code/` (Kimi Code CLI) and `.claude/` (Claude Code) homes with adapted copies of all agents/skills, plus CLAUDE.md, README.md, SYNC-STRUCTURE.md and sync scripts.
-
-**Architecture:** Physical `git mv` of existing content into `.kimi-code/`; full adapted copies under `.claude/` produced by a mechanical replacement pass + manual frontmatter rework; drift controlled by SYNC-STRUCTURE.md; deployment by `sync.ps1`/`sync.sh`.
-
-**Tech Stack:** markdown, git mv, PowerShell (robocopy), bash.
-
-**Spec:** `docs/superpowers/specs/2026-08-31-dual-tool-repo-layout-design.md`
-
-## Global Constraints
-
-- **No auto-commits:** commit steps below are suggestions; run `git commit` only after explicit user confirmation in the executing session. `git mv` (staging renames) is allowed.
-- Do not lose files: 17 agent files, 18 skill dirs, `TASK/`, `SOCIAL_DICTIONARY.md`, `config.toml` must all land under `.kimi-code/`.
-- `.claude/` content must contain ZERO occurrences of: `~/.kimi-code`, `K2.7`, `K3`, `kimi-code`, `AgentSwarm`, `config.toml`.
-- `.kimi-code/` content must contain ZERO occurrences of: `~/.claude`, `sonnet`, `opus`, `haiku`, `Claude Code`.
-- Model mapping: K2.7 → `sonnet` (default coder/reviewer); K3 → `opus` (orchestration, audit, ai-code-detector, ai-obfuscator). `haiku` only where a skill mentions cheap/lightweight auxiliary work.
-- Content of code-style/workflow rules is tool-agnostic — copy verbatim, replace only tool-specific fragments.
-- All repo file content in English; conversational language with the user is Russian.
-
-## Replacement Table (used by Tasks 4-6)
-
-Apply in this exact order (longest/most specific first):
-
-| # | Find | Replace |
-|---|------|---------|
-| 1 | `~/.kimi-code/skills` | `~/.claude/skills` |
-| 2 | `~/.kimi-code/plugins/managed/superpowers` | `superpowers plugin for Claude Code` |
-| 3 | `~/.kimi-code/` | `~/.claude/` |
-| 4 | `kimi-code/kimi-for-coding-highspeed` | `haiku` |
-| 5 | `kimi-code/kimi-for-coding` | `sonnet` |
-| 6 | `kimi-code/kimi-k3` | `opus` |
-| 7 | `kimi-code/k3-256k` | `opus` |
-| 8 | `kimi-code/k3` | `opus` |
-| 9 | `Kimi K2.7` | `Claude Sonnet` |
-| 10 | `Kimi K3` | `Claude Opus` |
-| 11 | `K2.7` | `Sonnet` |
-| 12 | `K3` | `Opus` |
-| 13 | `Kimi Code CLI` | `Claude Code` |
-| 14 | `kimi-code` | `claude` (only in path/identifier contexts — review each hit) |
-| 15 | `AgentSwarm` | `multiple Task tool calls in one message` |
-| 16 | `Agent tool` / `` `Agent` `` | `Task tool` / `` `Task` `` |
-
-Rule 14 is the only fuzzy one: every hit must be eyeballed; in prose like "kimi-code's config" it becomes "Claude Code's settings".
-
----
-
-### Task 1: Move existing content into `.kimi-code/`
-
-**Files:**
-- Create: `.kimi-code/` (via git mv)
-- Modify: none
-
-**Interfaces:**
-- Produces: `.kimi-code/agents/` (17 files), `.kimi-code/skills/` (18 dirs), `.kimi-code/TASK/`, `.kimi-code/SOCIAL_DICTIONARY.md`, `.kimi-code/config.toml` — consumed by Tasks 3-6 as the adaptation source.
-
-- [ ] **Step 1: git mv everything**
-
-```bash
-mkdir -p .kimi-code
-git mv agents .kimi-code/agents
-git mv skills .kimi-code/skills
-git mv TASK .kimi-code/TASK
-git mv SOCIAL_DICTIONARY.md .kimi-code/SOCIAL_DICTIONARY.md
-git mv config.toml .kimi-code/config.toml
-```
-
-- [ ] **Step 2: Verify nothing lost**
-
-Run: `git status --short | grep -c '^R'` and `ls .kimi-code/agents | wc -l && ls -d .kimi-code/skills/*/ | wc -l`
-Expected: renames staged; 17 agents, 18 skills. Root contains only: `.kimi-code/`, `docs/`, `.gitignore`, `AGENTS.md`, `.git/`.
-
-- [ ] **Step 3: Verify history follows**
-
-Run: `git log --follow --oneline -1 .kimi-code/agents/dotnet-coder.md`
-Expected: shows prior commit, not "new file".
-
-- [ ] **Step 4: Commit (after user confirmation)**
-
-```bash
-git commit -m "Move agent crew files into .kimi-code home directory"
-```
-
----
-
-### Task 2: Fix self-references inside `.kimi-code/`
-
-**Files:**
-- Modify: `.kimi-code/agents/*.md` (all 17), `.kimi-code/skills/*/SKILL.md`, `.kimi-code/AGENTS-related refs`
-
-**Interfaces:**
-- Consumes: Task 1 layout.
-- Produces: `.kimi-code/` files whose internal `~/.kimi-code/...` references are consistent (they already use `~/.kimi-code` absolute paths, so likely no change needed — this task is a verification gate).
-
-- [ ] **Step 1: Grep for stale relative references**
-
-Run: `grep -rn 'skills/' .kimi-code/agents/ | grep -v '~/.kimi-code/skills'`
-Expected: no hits (agent files already reference `~/.kimi-code/skills/<name>/SKILL.md` absolutely). If hits appear, fix each to the absolute `~/.kimi-code/...` form.
-
-- [ ] **Step 2: Grep for references to repo-root paths that moved**
-
-Run: `grep -rn 'SOCIAL_DICTIONARY\|config.toml' .kimi-code/agents/ .kimi-code/skills/ | grep -v '~/\.kimi-code'`
-Expected: only acceptable hits (e.g. prose mentioning the file name). Any path pointing to old repo location gets corrected to `~/.kimi-code/...`.
-
-- [ ] **Step 3: Commit (after user confirmation)** — only if steps 1-2 produced edits.
-
----
-
-### Task 3: Port AGENTS.md → CLAUDE.md
-
-**Files:**
-- Create: `CLAUDE.md` (repo root)
-- Modify: `AGENTS.md` (root) — add layout note, update Documentation Files paths
-
-**Interfaces:**
-- Consumes: `AGENTS.md` (root, current version with TASK/ workflow), Replacement Table.
-- Produces: `CLAUDE.md` — Claude-side global memory; sync script copies it to `~/.claude/CLAUDE.md`.
-
-- [ ] **Step 1: Create CLAUDE.md as adapted copy**
-
-Copy `AGENTS.md` content, then apply:
-- Title stays `# Global Agent Context`.
-- Apply Replacement Table rules 1-16.
-- Rewrite the `**Enforcement (config, not just prompt):**` block — replace the `config.toml` TOML snippet with:
-
-```markdown
-**Enforcement (config, not just prompt):** sub-agent model choice is enforced by the `model` field
-in each `.claude/agents/*.md` frontmatter (`sonnet` for coder/reviewer agents, `opus` for
-orchestration/audit/ai-obfuscator agents) — not by prompt text alone.
-```
-
-- In `Documentation Files` table: `TASK/NNN-slug.md` row path becomes `~/.kimi-code/TASK/NNN-slug.md` on the Kimi side; in CLAUDE.md write `~/.claude/TASK/NNN-slug.md`.
-- Keep tone/slang/stack/workflow sections verbatim.
-
-- [ ] **Step 2: Verify no Kimi leakage**
-
-Run: `grep -in 'k2\.7\|k3\|kimi\|agentswarm\|config\.toml' CLAUDE.md`
-Expected: zero hits.
-
-- [ ] **Step 3: Update root AGENTS.md**
-
-Add at the top, directly under `# Global Agent Context`:
-
-```markdown
-> Repo layout: this file's crew lives in `.kimi-code/` (agents, skills, TASK, dictionary, config).
-> Claude Code mirror lives in `.claude/` + `CLAUDE.md`. Dual-edit rule: see `SYNC-STRUCTURE.md`.
-```
-
-In the `Documentation Files` table, update the `TASK/NNN-slug.md` row to `~/.kimi-code/TASK/NNN-slug.md` and `SOCIAL_DICTIONARY.md` mention to `~/.kimi-code/SOCIAL_DICTIONARY.md`.
-
-- [ ] **Step 4: Verify AGENTS.md has no Claude leakage**
-
-Run: `grep -in 'claude\|sonnet\|opus\|haiku' AGENTS.md`
-Expected: hits only inside the new layout note (`.claude/`, `CLAUDE.md`, SYNC-STRUCTURE mention) — those are intentional.
-
-- [ ] **Step 5: Commit (after user confirmation)**
-
----
-
-### Task 4: Create `.claude/skills/` adapted copies
-
-**Files:**
-- Create: `.claude/skills/<name>/SKILL.md` for all 18 skills, plus any extra files each skill dir contains
-
-**Interfaces:**
-- Consumes: `.kimi-code/skills/` (post-Task-2), Replacement Table.
-- Produces: Claude-side skills, referenced by `.claude/agents/*.md` (Task 5) and `CLAUDE.md`.
-
-- [ ] **Step 1: Copy the tree**
-
-```bash
-mkdir -p .claude/skills
-cp -r .kimi-code/skills/* .claude/skills/
-```
-
-- [ ] **Step 2: Apply mechanical replacements (rules 1-13, 15-16)**
-
-```bash
-cd .claude/skills
-grep -rl '' . | while read f; do
-  sed -i \
-    -e 's|~/\.kimi-code/skills|~/.claude/skills|g' \
-    -e 's|~/\.kimi-code/plugins/managed/superpowers|superpowers plugin for Claude Code|g' \
-    -e 's|~/\.kimi-code/|~/.claude/|g' \
-    -e 's|kimi-code/kimi-for-coding-highspeed|haiku|g' \
-    -e 's|kimi-code/kimi-for-coding|sonnet|g' \
-    -e 's|kimi-code/kimi-k3|opus|g' \
-    -e 's|kimi-code/k3-256k|opus|g' \
-    -e 's|kimi-code/k3|opus|g' \
-    -e 's|Kimi K2\.7|Claude Sonnet|g' \
-    -e 's|Kimi K3|Claude Opus|g' \
-    -e 's|K2\.7|Sonnet|g' \
-    -e 's|K3|Opus|g' \
-    -e 's|Kimi Code CLI|Claude Code|g' \
-    -e 's|AgentSwarm|multiple Task tool calls in one message|g' \
-    "$f"
-done
-```
-
-- [ ] **Step 3: Handle fuzzy rule 14 and config-pool mentions manually**
-
-Run: `grep -rn 'kimi' .claude/skills/ -i`
-For each hit: eyeball and fix contextually (paths → `~/.claude`, product mentions → Claude Code, the `config.toml` secondary_model pool block → rewritten as `.claude/agents/*.md` `model` frontmatter enforcement, same wording as Task 3 Step 1).
-
-- [ ] **Step 4: Verification gate (must be empty)**
-
-Run: `grep -rni 'k2\.7\|\bk3\b\|kimi\|agentswarm\|config\.toml' .claude/skills/`
-Expected: zero hits. Any hit = go back to Step 3.
-
-- [ ] **Step 5: Verify skill count parity**
-
-Run: `ls -d .kimi-code/skills/*/ | wc -l && ls -d .claude/skills/*/ | wc -l`
-Expected: 18 and 18.
-
-- [ ] **Step 6: Commit (after user confirmation)**
-
----
-
-### Task 5: Create `.claude/agents/` adapted copies
-
-**Files:**
-- Create: `.claude/agents/*.md` (17 files)
-
-**Interfaces:**
-- Consumes: `.kimi-code/agents/`, `.claude/skills/` paths from Task 4.
-- Produces: agents loadable by Claude Code; frontmatter fields `name`, `description`, `model` consumed by Claude Code runtime.
-
-- [ ] **Step 1: Copy and mechanically replace**
-
-```bash
-mkdir -p .claude/agents
-cp .kimi-code/agents/*.md .claude/agents/
-```
-
-Apply the same sed script as Task 4 Step 2 against `.claude/agents/`.
-
-- [ ] **Step 2: Rework frontmatter per file**
-
-Current frontmatter shape:
-
-```yaml
----
-name: dotnet-coder
-description: Senior .NET/C# engineer that writes and refactors production code
-whenToUse: Creating or modifying C# code, services, endpoints, DTOs, tests
----
-```
-
-Claude Code shape (per file):
-
-```yaml
----
-name: dotnet-coder
-description: Senior .NET/C# engineer that writes and refactors production code. Use when creating or modifying C# code, services, endpoints, DTOs, tests.
-model: sonnet
----
-```
-
-Rules: merge `whenToUse` into `description` as a "Use when ..." sentence (Claude Code has no `whenToUse` field); set `model:` per Global Constraints mapping — `opus` for: `audit-orchestrator`, `coding-challenge-orchestrator`, `task-orchestrator`, `ai-code-detector`, `ai-obfuscator`, `ai-obfuscation-reviewer`; `sonnet` for everything else.
-
-- [ ] **Step 3: Manual pass on remaining kimi hits**
-
-Run: `grep -rni 'kimi\|k2\.7\|\bk3\b\|agentswarm\|config\.toml' .claude/agents/`
-Fix each contextually (same rules as Task 4 Step 3). Expected after pass: zero hits.
-
-- [ ] **Step 4: Verify count parity**
-
-Run: `ls .kimi-code/agents/*.md | wc -l && ls .claude/agents/*.md | wc -l`
-Expected: 17 and 17.
-
-- [ ] **Step 5: Commit (after user confirmation)**
-
----
-
-### Task 6: Mirror TASK/ into `.claude/TASK/`
-
-**Files:**
-- Create: `.claude/TASK/_TEMPLATE.md`
-
-**Interfaces:**
-- Consumes: `.kimi-code/TASK/_TEMPLATE.md`.
-- Produces: verbatim mirror referenced by CLAUDE.md (`~/.claude/TASK/...`).
-
-- [ ] **Step 1: Copy**
-
-```bash
-mkdir -p .claude/TASK
-cp .kimi-code/TASK/_TEMPLATE.md .claude/TASK/_TEMPLATE.md
-```
-
-- [ ] **Step 2: Grep for tool-specific refs in template**
-
-Run: `grep -ni 'kimi\|claude' .claude/TASK/_TEMPLATE.md`
-Expected: zero hits (template is tool-agnostic). If a hit exists, generalize the wording.
-
-- [ ] **Step 3: Commit (after user confirmation)**
-
----
-
-### Task 7: Write SYNC-STRUCTURE.md
-
-**Files:**
-- Create: `SYNC-STRUCTURE.md` (root)
-
-**Interfaces:**
-- Consumes: final file lists from Tasks 1-6.
-- Produces: the dual-edit contract referenced by AGENTS.md, CLAUDE.md, README.md.
-
-- [ ] **Step 1: Write the file**
-
-Content skeleton (fill the mapping table with the real 17+18 pairs — generate it with `ls` output, one row per pair):
-
-```markdown
-# Sync Structure Contract
-
-This repo serves two runtimes: Kimi Code CLI (`.kimi-code/`) and Claude Code (`.claude/`).
-
-## The Rule
-
-**Edit one side — edit the other in the same commit.** Content differences between the sides
-are allowed ONLY for tool-specific fragments (model names, tool-home paths, runtime features).
-
-## Mapping
-
-| Kimi side | Claude side | Notes |
-|-----------|-------------|-------|
-| `AGENTS.md` | `CLAUDE.md` | CLAUDE.md = adapted port |
-| `.kimi-code/agents/<name>.md` | `.claude/agents/<name>.md` | Claude adds `model:` frontmatter |
-| `.kimi-code/skills/<name>/` | `.claude/skills/<name>/` | full copy, tool refs swapped |
-| `.kimi-code/TASK/` | `.claude/TASK/` | verbatim mirror |
-| `.kimi-code/SOCIAL_DICTIONARY.md` | — | shared via AGENTS.md/CLAUDE.md reference to the Kimi copy? NO — see below |
-| `.kimi-code/config.toml` | `~/.claude/settings.json` | tool-local, not synced by content |
-
-## Translation table (Kimi → Claude)
-
-[paste Replacement Table from the plan/spec]
-
-## Pre-commit checklist
-
-- [ ] Pairs in sync (diff both sides of any file you touched)?
-- [ ] `grep -rni 'k2\.7\|\bk3\b\|kimi\|agentswarm' .claude/` empty?
-- [ ] `grep -rni 'claude\|sonnet\|opus\|haiku' .kimi-code/` empty?
-```
-
-Note: SOCIAL_DICTIONARY.md is referenced by both AGENTS.md and CLAUDE.md but lives once under `.kimi-code/`; the sync script copies it to both homes. State that explicitly in the file.
-
-- [ ] **Step 2: Generate the real mapping rows**
-
-Run: `ls .kimi-code/skills/` and `ls .kimi-code/agents/` — paste as table rows.
-
-- [ ] **Step 3: Commit (after user confirmation)**
-
----
-
-### Task 8: Write README.md with prerequisites
-
-**Files:**
-- Create: `README.md` (root)
-
-**Interfaces:**
-- Consumes: layout from Tasks 1-7.
-- Produces: entry doc; superpowers prerequisite stated here.
-
-- [ ] **Step 1: Write README.md**
-
-Required sections (write full prose, English):
-
-1. **What this is** — single source of truth for the agent crew across Kimi Code CLI and Claude Code.
-2. **Prerequisites:**
-   - Kimi Code CLI, with the `superpowers` plugin installed at `~/.kimi-code/plugins/managed/superpowers` (workflow skills — brainstorming, writing-plans, TDD, etc. — depend on it).
-   - Claude Code; for workflow parity, the Superpowers plugin for Claude Code.
-3. **Layout** — the tree from the spec (`.kimi-code/`, `.claude/`, CLAUDE.md, AGENTS.md, SYNC-STRUCTURE.md, sync scripts).
-4. **Install / update** — `sync.ps1` (Windows) / `sync.sh` (*nix), dry-run flag, what gets copied where.
-5. **Editing rule** — pointer to SYNC-STRUCTURE.md.
-
-- [ ] **Step 2: Verify links resolve**
-
-Run: `grep -o '\`[^\`]*\`' README.md | tr -d '\`' | while read p; do [ -e "$p" ] || echo "MISSING: $p"; done`
-Expected: no MISSING lines (paths referenced in README exist in repo).
-
-- [ ] **Step 3: Commit (after user confirmation)**
-
----
-
-### Task 9: Write sync scripts
-
-**Files:**
-- Create: `sync.ps1`, `sync.sh` (root)
-
-**Interfaces:**
-- Consumes: final layout.
-- Produces: `sync.ps1 [-DryRun]`, `sync.sh [--dry-run]` — idempotent deploy to `~/.kimi-code` and `~/.claude`.
-
-- [ ] **Step 1: Write sync.ps1**
-
-```powershell
-param([switch]$DryRun)
-
-$ErrorActionPreference = 'Stop'
-$root = Split-Path -Parent $MyInvocation.MyCommand.Path
-$kimiHome = Join-Path $HOME '.kimi-code'
-$claudeHome = Join-Path $HOME '.claude'
-$rc = if ($DryRun) { '/L' } else { '/MIR' }
-
-function Mirror($src, $dst) {
-    robocopy $src $dst /MIR $(if ($DryRun) { '/L' }) /NFL /NDL /NJH | Out-Null
-}
-
-Mirror (Join-Path $root '.kimi-code/agents')  (Join-Path $kimiHome 'agents')
-Mirror (Join-Path $root '.kimi-code/skills')  (Join-Path $kimiHome 'skills')
-Mirror (Join-Path $root '.kimi-code/TASK')    (Join-Path $kimiHome 'TASK')
-Copy-Item (Join-Path $root '.kimi-code/SOCIAL_DICTIONARY.md') $kimiHome -Force:$(-not $DryRun) -WhatIf:$DryRun
-Copy-Item (Join-Path $root '.kimi-code/config.toml') $kimiHome -Force:$(-not $DryRun) -WhatIf:$DryRun
-
-Mirror (Join-Path $root '.claude/agents') (Join-Path $claudeHome 'agents')
-Mirror (Join-Path $root '.claude/skills') (Join-Path $claudeHome 'skills')
-Mirror (Join-Path $root '.claude/TASK')   (Join-Path $claudeHome 'TASK')
-Copy-Item (Join-Path $root 'CLAUDE.md') $claudeHome -Force:$(-not $DryRun) -WhatIf:$DryRun
-
-if ($DryRun) { Write-Host 'Dry run complete — no changes made.' }
-```
-
-- [ ] **Step 2: Dry-run test**
-
-Run: `powershell -File sync.ps1 -DryRun`
-Expected: prints robocopy `/L` listings, exit code < 8, no filesystem changes (`~/.kimi-code` untouched).
-
-- [ ] **Step 3: Write sync.sh**
-
-```bash
-#!/usr/bin/env bash
-set -euo pipefail
-ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
-DRY=""
-[[ "${1:-}" == "--dry-run" ]] && DRY="echo"
-
-$DRY cp -r "$ROOT/.kimi-code/agents" "$HOME/.kimi-code/"
-$DRY cp -r "$ROOT/.kimi-code/skills" "$HOME/.kimi-code/"
-$DRY cp -r "$ROOT/.kimi-code/TASK" "$HOME/.kimi-code/"
-$DRY cp "$ROOT/.kimi-code/SOCIAL_DICTIONARY.md" "$ROOT/.kimi-code/config.toml" "$HOME/.kimi-code/"
-$DRY cp -r "$ROOT/.claude/agents" "$ROOT/.claude/skills" "$ROOT/.claude/TASK" "$HOME/.claude/"
-$DRY cp "$ROOT/CLAUDE.md" "$HOME/.claude/CLAUDE.md"
-```
-
-`chmod +x sync.sh`.
-
-- [ ] **Step 4: Dry-run test**
-
-Run: `./sync.sh --dry-run`
-Expected: echoes the cp commands, no changes.
-
-- [ ] **Step 5: Commit (after user confirmation)**
-
----
-
-### Task 10: Final cross-contamination gate
-
-**Files:** none (verification only)
-
-- [ ] **Step 1: Claude side clean**
-
-Run: `grep -rni 'k2\.7\|\bk3\b\|kimi\|agentswarm\|config\.toml' .claude/ CLAUDE.md`
-Expected: zero hits.
-
-- [ ] **Step 2: Kimi side clean**
-
-Run: `grep -rni 'claude code\|sonnet\|opus\|haiku' .kimi-code/`
-Expected: zero hits (word `claude` alone may appear nowhere either — check).
-
-- [ ] **Step 3: Structure check**
-
-Run: `ls .kimi-code .claude`
-Expected: `.kimi-code`: agents skills TASK SOCIAL_DICTIONARY.md config.toml; `.claude`: agents skills TASK.
-
-- [ ] **Step 4: Update .gitignore if needed**
-
-Check `.gitignore` — no entries should exclude the new dirs. No change expected.
-
-- [ ] **Step 5: Final commit (after user confirmation)**
-
----
-
-## Self-Review Notes
-
-- Spec coverage: git mv (T1), self-refs (T2), CLAUDE.md+AGENTS.md (T3), .claude/skills (T4), .claude/agents (T5), TASK mirror (T6), SYNC-STRUCTURE.md (T7), README+superpowers prerequisite (T8), sync scripts (T9), verification gates (T10). All spec sections covered.
-- Placeholder scan: SYNC-STRUCTURE.md mapping table is generated from live `ls` output at execution — intentional, command provided. No other placeholders.
-- Type consistency: replacement strings identical between Global Constraints, Replacement Table, sed script, and grep gates.
diff --git a/docs/superpowers/specs/2026-08-31-dual-tool-repo-layout-design.md b/docs/superpowers/specs/2026-08-31-dual-tool-repo-layout-design.md
deleted file mode 100644
index 0b7dcf2..0000000
--- a/docs/superpowers/specs/2026-08-31-dual-tool-repo-layout-design.md
+++ /dev/null
@@ -1,101 +0,0 @@
-# Design: Dual-tool repo layout (.kimi-code + .claude)
-
-Date: 2026-08-31
-Status: approved (verbal), pending written-spec review
-
-## Goal
-
-Restructure the repo so it serves as the single source of truth for TWO agent runtimes:
-Kimi Code CLI (`~/.kimi-code`) and Claude Code (`~/.claude`). Same agent crew, same skills,
-adapted to each tool's conventions, with an explicit sync contract to prevent drift.
-
-## Chosen approach
-
-**Mirror with full adapted copies** (approved over: shared-core generator, symlinks).
-
-- Physical `git mv` of current content into `.kimi-code/` — single home for Kimi files.
-- `.claude/` holds full, adapted copies of agents and skills (no symlinks, no shared references).
-- Duplication risk is managed by `SYNC-STRUCTURE.md` (explicit file-to-file mapping + edit-both rule).
-
-Rejected: generator/templating layer (YAGNI for markdown), symlinks (Windows pain + diverging frontmatter formats anyway).
-
-## Target layout
-
-```
-├── .kimi-code/
-│   ├── agents/                  # git mv from agents/ (17 files)
-│   ├── skills/                  # git mv from skills/ (18 dirs)
-│   ├── TASK/                    # git mv from TASK/ (_TEMPLATE.md + future task files)│   ├── SOCIAL_DICTIONARY.md     # git mv from root
-│   └── config.toml              # git mv from root (Kimi-specific config)
-├── .claude/
-│   ├── agents/                  # adapted copies, Claude frontmatter
-│   ├── skills/                  # adapted full copies, Kimi refs replaced
-│   └── TASK/                    # verbatim mirror of .kimi-code/TASK/
-├── CLAUDE.md                    # full port of AGENTS.md, Kimi-specifics swapped to Claude equivalents
-├── AGENTS.md                    # stays in root; updated to reflect new paths/structure
-├── README.md                    # repo overview + prerequisites + sync usage
-├── SYNC-STRUCTURE.md            # dual-edit contract + file mapping
-├── sync.ps1                     # deploy: .kimi-code/* -> ~/.kimi-code, .claude/* + CLAUDE.md -> ~/.claude
-├── sync.sh                      # same for *nix
-└── .gitignore
-```
-
-`TASK/` is part of the global workflow documentation system (referenced by AGENTS.md), not Kimi-specific
-config. It moves to `.kimi-code/TASK/` and is mirrored verbatim to `.claude/TASK/` (template content is
-tool-agnostic — identical copy, covered by SYNC-STRUCTURE.md). Both AGENTS.md and CLAUDE.md reference it
-via tool-home-relative paths (`~/.kimi-code/TASK/...` / `~/.claude/TASK/...`).
-
-## Adaptation rules: Kimi -> Claude
-
-### Agents (.claude/agents/*.md)
-
-- Frontmatter: `name`, `description`, `tools`, `model`.
-- Skill references: `~/.kimi-code/skills/<name>/SKILL.md` -> `~/.claude/skills/<name>/SKILL.md`.
-- Model mapping (sub-agent rules): K2.7 (default coder/reviewer) -> `sonnet`; K3 (orchestration, audit, AI-code detection, obfuscation) -> `opus`. Cheap/fast tier -> `haiku` where a skill mentions lightweight work.
-
-### Skills (.claude/skills/<name>/SKILL.md)
-
-- Content copied in full; code/style/workflow rules unchanged (they are tool-agnostic).
-- Kimi-specific fragments replaced: model names (K2.7/K3 -> sonnet/opus), paths (`~/.kimi-code` -> `~/.claude`), Kimi CLI features (secondary_model pool, `Agent`/`AgentSwarm` params) -> Claude Code equivalents (Task tool, `model` frontmatter field).
-
-### CLAUDE.md (root)
-
-- Full port of AGENTS.md content.
-- `Sub-Agent Rules` rewritten for Claude Code: allowed models `sonnet`/`opus` (sonnet default, opus for orchestration/audit/obfuscation), enforcement via `.claude/settings.json` note instead of `config.toml` pool.
-- `Documentation Files` table: TASK paths updated to `~/.kimi-code/TASK/...`; the mirrored copy on the Claude side is `~/.claude/TASK/...`.
-- Everything else (tone, slang, .NET stack, frontend stack, AI-traces, workflow) carried over verbatim.
-
-## README.md (root)
-
-- What this repo is: single source of truth for the agent crew across Kimi Code CLI and Claude Code.
-- **Prerequisites section (explicit install requirements):**
-  - Kimi Code CLI + the `superpowers` plugin installed at `~/.kimi-code/plugins/managed/superpowers` — the workflow skills (brainstorming, writing-plans, TDD, etc.) depend on it.
-  - Claude Code; optionally the Superpowers plugin for Claude Code for workflow parity.
-- Layout overview (`.kimi-code/` vs `.claude/`), sync script usage (`sync.ps1` / `sync.sh`, dry-run), pointer to `SYNC-STRUCTURE.md` for the dual-edit rule.
-
-## SYNC-STRUCTURE.md
-
-- Mapping table: every `.kimi-code/skills/X` <-> `.claude/skills/X`, `.kimi-code/agents/Y` <-> `.claude/agents/Y`, `AGENTS.md` <-> `CLAUDE.md`.
-- Rule: edit one side -> edit the other in the same commit.
-- Pre-commit checklist: pairs in sync? Kimi-specifics not leaked into `.claude/`? Claude-specifics not leaked into `.kimi-code/`?
-- Drift markers: each adapted file keeps no AI-style banner; drift is caught by review, not by file headers.
-
-## Sync script
-
-- `sync.ps1` (primary, Windows): robocopy `/MIR` `.kimi-code/agents` -> `~/.kimi-code/agents`, `.kimi-code/skills` -> `~/.kimi-code/skills`, `.kimi-code/TASK` -> `~/.kimi-code/TASK`, plus config/dictionary; robocopy `.claude/agents`, `.claude/skills`, `.claude/TASK` -> `~/.claude/...`, copy `CLAUDE.md` -> `~/.claude/CLAUDE.md`.
-- `-DryRun` switch (robocopy `/L`).
-- `sync.sh` — cp -r based equivalent, `--dry-run` via rsync if available, else plain cp.
-- Idempotent; no deletes outside the mirrored target dirs.
-
-## Error handling / risks
-
-- `git mv` preserves history; verify with `git log --follow`.
-- Claude frontmatter validation: each agent file must parse (name/description/tools/model) — checked manually in review gate.
-- Risk of drift between copies: mitigated only by SYNC-STRUCTURE.md discipline (accepted trade-off).
-
-## Testing / verification
-
-- After move: `git status` clean renames, no lost files (17 agents, 18 skills, TASK/, dictionary, config).
-- Diff audit: every `.claude` file contains zero `~/.kimi-code` / `K2.7` / `K3` / `kimi-code` strings (grep gate).
-- Reverse grep: `.kimi-code/` contains zero `~/.claude` / `sonnet` / `opus` strings.
-- `sync.ps1 -DryRun` prints expected copy plan without touching the filesystem.
