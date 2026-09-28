---
name: architect-review
description: |
  Structural integrity review of the whole solution: dependency direction, layer boundaries,
  service seams, FSD boundaries on frontend. Two modes — light (per Complex task) and hard
  (phase end / big refactor). Use when a task file declares `review: hard`, at the end of
  a sprint phase, or after a structural refactoring.
type: prompt
whenToUse: |
  End of sprint phase, big refactor (>10 files, structural shift), or any task file with
  `review: hard` in frontmatter. Light mode runs as part of Phase 3 review on Complex tasks.
---

# Architect Review Skill

Reviews STRUCTURE, not line-level code (that's `dotnet-code-reviewer` / `web-code-reviewer`).
Question being answered: "Is the system still shaped the way CONTEXT.md / STRUCTURE.md say it should be?"

## Inputs (read in this order)
1. `CONTEXT.md` — declared architecture, service boundaries, communication rules
2. `STRUCTURE.md` — folder conventions, Do/Don't
3. `TECHNICAL-SOLUTIONS.md` — existing ADRs (never contradict silently)
4. Overmind board — current feature + its artifacts: what was actually built this phase
5. Task frontmatter `review:` field — `light` (default) or `hard`

## Mode Selection
- Task file says `review: hard` → Hard checklist.
- Phase end / structural refactor (>10 files, new service, module split) → Hard checklist.
- Everything else (Complex tasks in Phase 3) → Light checklist.

## Light Checklist (per Complex task)
- [ ] **Dependency direction:** Domain → Application → Infrastructure → API. No reverse or skip-layer references.
- [ ] **Layer leaks:** domain entities don't escape to API; infrastructure types don't leak into Application.
- [ ] **God-services:** no new service with >5 public methods; no class accumulating unrelated responsibilities.
- [ ] **File placement:** new files land in the layers/folders STRUCTURE.md prescribes (not "where it was convenient").
- [ ] **No new cyclic dependencies** between projects/modules (`dotnet list reference` / project graph check).
- [ ] **Frontend (if touched):** FSD boundaries respected — `features/` don't import from each other cross-domain, `shared/` stays dependency-free, no direct backend calls from `'use client'` (BFF rule).

## Hard Checklist (phase end / big refactor) — everything in Light, plus:
- [ ] **Module boundaries:** each module/service has a single clear purpose; public surface is minimal and intentional.
- [ ] **Coupling audit:** fan-in/fan-out hotspots; types referenced from 5+ modules are candidates for `shared` or re-seaming.
- [ ] **Cohesion audit:** files that change together live together; scatter (one feature touching 6+ unrelated folders) = smell.
- [ ] **ADR coverage:** every structural decision made this phase has an ADR in TECHNICAL-SOLUTIONS.md; contradictions between ADRs and actual code are flagged.
- [ ] **Split/merge proposals:** concrete recommendations with effort estimate (S/M/L) — not abstract "consider refactoring".
- [ ] **Frontend (if present):** FSD layer graph (`app` → `pages` → `widgets` → `features` → `shared`) has no upward imports; query-key factories centralized; no BFF bypass.

## Output
Artifact: `audits/architect-review-YYYY-MM-DD.md` (artifact provenance rule) or a section in the review report when invoked inside Phase 3.
For each finding:
1. **Severity:** `BLOCKER` (broken boundary, reverse dependency) / `WARNING` (erosion, will bite later) / `SUGGESTION` (re-seam opportunity)
2. **Location:** project/module/folder
3. **Problem:** which declared rule (CONTEXT.md / STRUCTURE.md / ADR) is violated
4. **Fix:** concrete structural change (move X to Y, introduce seam Z)

Findings that change architecture → propose ADR entry in `TECHNICAL-SOLUTIONS.md` (user approves, never write silently).

## Communication
- Speak Russian with the user; artifact content in English.
- No auto-commits. Report changed/created files and ask about commit.

## Output Contract (MANDATORY)
End with the fixed closing block, then STOP:
1. **Done** — summary + artifact paths.
2. **Next step** — recommended follow-up per `SKILLFLOW.md`.
3. **Alternatives** — 2–3 other valid follow-ups.
Never auto-proceed.
