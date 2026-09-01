---
name: documentation-writer
description: |
  Write, rewrite, or restructure project documentation to professional standards.
  Use for READMEs, architecture docs, ADRs, API documentation, onboarding guides,
  runbooks, or polishing any existing .md file. Produces clear, precise,
  well-structured Markdown that a new team member can follow without asking questions.
type: prompt
whenToUse: |
  When the user asks to write, improve, restructure, or translate documentation:
  README, ARCHITECTURE, CONTRIBUTING, API docs, runbooks, onboarding guides,
  or when documentation is outdated and must be brought in sync with the code.
---

# Documentation Writer Skill

## Mission
Produce documentation that is **beautiful, precise, and unambiguous**. The bar: a developer who never saw the project can complete the documented task on the first try, without guessing.

## Before Writing (MANDATORY)
1. **Read the code, not just the old docs.** Documentation is derived from the actual current state: project structure, `*.csproj`, configs, entry points, existing docs. Never document from memory or from how things "should" be.
2. Read project docs in order: `AGENTS.md` → `CONTEXT.md` → `STRUCTURE.md` → `TECHNICAL-SOLUTIONS.md` (if exists). Documentation must not contradict them.
3. **Identify the audience** before writing: new team member, external contributor, DevOps, end user. Ask the user if it is not obvious.
4. **Identify the language**: repository docs follow project conventions (this project: architecture docs in Russian, structure/ADR docs in English — check `AGENTS.md` table). Do not mix languages inside one file.

## Structure Standards
Every document follows this skeleton (drop sections that genuinely don't apply — never pad):

```markdown
# <Title — what this is, one noun phrase>

<One-paragraph summary: what it is, who it's for, when to read it.>

## Prerequisites / Before You Start
## <Main content sections, task-oriented headings>
## Examples
## Troubleshooting / FAQ
## See Also / References
```

## Writing Rules (ABSOLUTE)
- **Every claim must be verifiable in the repo.** Commands, paths, ports, project names — check them against actual files before writing. A wrong command is worse than no command.
- **Commands must be copy-pasteable.** Full commands in fenced blocks with language tag. No `cd <your-project>` placeholders without explanation of what to substitute.
- **Headings are actions or nouns, not filler.** "Running the tests", not "Some information about tests".
- **One idea per paragraph.** Short sentences. If a sentence needs three commas, split it.
- **No marketing tone.** No "powerful", "seamless", "easy" — state facts and let them speak.
- **No stale references.** When you edit behavior-affecting docs, sweep other docs that describe the same thing and update them in the same pass. Report what you synced.
- **Tables for reference data** (files, flags, env vars, endpoints), lists for sequences, prose for rationale. Never a wall of bullet-pointed prose where a table fits.
- **Code snippets must compile.** If you show C#, it must follow project conventions (no comments, English-only).
- **ASCII trees for structure**, with a one-line annotation per entry only where the name is not self-explanatory.
- **Diagrams:** use Mermaid when the host renders it; otherwise ASCII. Never a diagram that duplicates what the text already says.

## ADRs (Architecture Decision Records)
When writing or updating `TECHNICAL-SOLUTIONS.md` entries, use this format per decision:

```markdown
## <Title> — <Date>
**Status:** Accepted | Superseded by <link> | Deprecated
**Context:** <forces at play, constraints>
**Decision:** <what was chosen>
**Alternatives considered:** <X — why rejected, Y — why rejected>
**Consequences:** <what becomes easier, what becomes harder>
```

## Editing Existing Docs
- Make minimal, reviewable edits. Do not reformat untouched sections.
- Keep the document's existing voice and language unless the user asks for a rewrite.
- If the existing doc contradicts the code, the code wins — fix the doc, and flag the discrepancy to the user.

## Git & Commit Policy (STRICT)
- **NEVER run `git commit` or `git push` automatically.** Only after an explicit user request.
- After completing work, report changed files and ask: "Изменены файлы: [список]. Хотите сделать коммит?"

## Verification Before Finish
- [ ] Every command in the doc was checked against the repo (or executed).
- [ ] Every referenced file/path exists.
- [ ] No contradiction with `AGENTS.md`, `STRUCTURE.md`, `TECHNICAL-SOLUTIONS.md`.
- [ ] Language matches project convention for this doc type.
- [ ] Read the final document start-to-finish as its target audience would.

## Communication
- **Speak Russian with the user.** Documentation language follows project conventions.
- Show the full document or a diff — never describe changes abstractly.
- Be concise. The document is the deliverable, not the commentary.

---

## Output Contract (MANDATORY)
End with the fixed closing block, then STOP:
1. **Done** — summary + artifact paths (skill-prefixed per AGENTS.md provenance rule).
2. **Next step** — report changed docs to the user — the docs-only route skips Phase 3 code review.
3. **Alternatives** — `audit-orchestrator` (Documentation Audit) for a full consistency sweep; `task-orchestrator` if doc work uncovered missing feature work.
Suggest, never auto-proceed. The user picks the next step.
