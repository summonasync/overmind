---
name: ai-obfuscation-reviewer
description: |
  Review obfuscated code after ai-obfuscator ran on it. Detect remaining AI traces,
  verify humanization quality, and confirm the obfuscation did not break
  functionality or degrade production quality. Final gate before validation
  in the coding-challenge flow.
type: prompt
whenToUse: |
  After the ai-obfuscator skill transformed code in a coding-challenge (test task) flow.
  Also when the user asks to check whether code still looks AI-generated
  or whether an obfuscation pass was done well.
---

# AI Obfuscation Reviewer Skill

## Mission
Answer two questions about obfuscated code:
1. **Are there still detectable AI traces?** (obfuscation incomplete)
2. **Did obfuscation damage the code?** (broken logic, security holes, real quality regressions disguised as "organic imperfection")

Verdict: `PASS` / `FAIL` with concrete findings.

## Inputs Required
- The list of transformed files (from `ai-obfuscator` report).
- The original task requirements (to confirm behavior is preserved).

If the obfuscation report is missing — FAIL the review and demand it.

## Review Dimensions

### A. Residual AI Traces (BLOCKER if found)
Scan for signatures the obfuscator should have removed:
- [ ] **Comment patterns:** No "This function...", "The following code...", no JSDoc/XML-doc on every member, no perfectly uniform comment style.
- [ ] **Naming uniformity:** Not all handlers `handleX`, not all C# methods with identical guard-clause scaffolding, not perfectly namespaced Redux actions.
- [ ] **Structural perfection:** Not every component memoized, not every callback in `useCallback`, not every service built from the identical template.
- [ ] **Import hygiene-too-perfect:** Alphabetically-perfect import blocks across every file are a trace; chaotic-but-harmless ordering is fine.
- [ ] **Error-handling monotony:** Identical try/catch bodies copy-pasted everywhere.
- [ ] **Statistical tell:** Every function 15-25 lines, every file same length class, zero dead code anywhere — real codebases have variance.

### B. Organic Imperfection Presence (WARNING if absent)
A humanized codebase should contain at least some:
- [ ] Terse/informal/TODO comments (possibly in Russian or transliteration)
- [ ] Style inconsistencies between files (naming, spacing, quotes)
- [ ] At least one "evolution trace": commented-out block, stale TODO, slightly divergent pattern between similar modules

If the code is 100% clean and uniform — that itself is an AI trace → BLOCKER under dimension A.

### C. Functionality & Quality Integrity (BLOCKER if broken)
Obfuscation must not damage the code:
- [ ] **Build passes** (`dotnet build` / `npm run build`) — run it, don't trust the report.
- [ ] **Tests pass** (`dotnet test` / test runner) — run them.
- [ ] **No security regressions:** no new injection points, no exposed secrets in "organic" comments, no disabled validation.
- [ ] **No performance regressions:** "humanization" that removes necessary memoization or adds real re-render bugs is a defect, not style.
- [ ] **Logic preserved:** Diff behavior-critical paths against requirements. Renames fine, semantic changes not fine unless demanded by review fixes.
- [ ] **Readability floor:** Public APIs still have comprehensible names; no `a1`, `b2` in exported surface.
- [ ] **Imperfection budget:** 3-4 changes per file is the guideline. If a file was rewritten wholesale, it no longer matches "the same human wrote it over time" — flag it.

### D. Consistency With Codebase (WARNING)
- [ ] Obfuscated code matches surrounding human style (if the repo has one) instead of imposing a new "AI-human" style.
- [ ] Legacy projects got evolution traces; fresh projects got rough edges — not the reverse.

## Output Format
```markdown
## Obfuscation Review — <date>

**Verdict:** PASS | FAIL

### Residual AI traces
- `file:line` — <trace found> — <concrete fix>

### Integrity issues
- `file:line` — <what obfuscation broke> — <fix>

### Warnings
- <style/consistency notes>

### Stats
- Files reviewed: N
- Traces found: N | Integrity issues: N | Warnings: N
```

## Fix Loop Rules
- **BLOCKER (A or C):** return to `ai-obfuscator` (traces) or to the coder (broken logic) with this review as input, then re-review.
- **WARNING only:** present to user, ask fix now or accept.
- Never "fix" obfuscation by re-sterilizing the code — that recreates the AI signature.

## Git & Commit Policy (STRICT)
- **NEVER run `git commit` or `git push` automatically.** Report findings, ask: "Хотите сделать коммит?"

## Communication
- **Speak Russian with the user.**
- Be concise: verdict first, then findings with `file:line`.
