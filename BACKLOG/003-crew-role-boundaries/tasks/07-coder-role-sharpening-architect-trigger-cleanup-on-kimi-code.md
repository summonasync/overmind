---
feature: 003
task: 07
slug: crew-role-boundaries
status: done
review: light
created: 2026-09-27
brief: Coder role sharpening + architect trigger cleanup on .kimi-code
---

<goal>Sharpen coder role boundaries and clean architect triggers on .kimi-code.</goal>
<scope>dotnet-coder / next-js-coder / maui-coder SKILL.md: remove «review» from frontmatter description and whenToUse («Write, edit, or refactor…»). dotnet-coder: replace the «Post-Task Review Protocol» section (self-applying the reviewer checklist) with a handoff rule: Complex task → reviewer as a SEPARATE agent dispatched by feature-orchestrator Phase 3; coder never applies the reviewer checklist to its own output; coder keeps build/test verification duties. Check next-js-coder and maui-coder for self-review sections and apply the same replacement. dotnet-architect + next-js-architect: strip «plan features / planning large features» from description/whenToUse; add «consulted BY feature-orchestrator (Phase 1) — never runs planning gates itself».</scope>
<anti-scope>No rename work (T1 owns it); no review-depth changes; no port sides; no BACKLOG/ or audits/ edits.</anti-scope>
<constraints>Reviewer independence preserved; English-only; NO git commit.</constraints>
<acceptance-criteria>
- [x] No coder frontmatter description/whenToUse claims «review»
- [x] dotnet-coder has no self-review protocol; the handoff rule is present
- [x] Architect frontmatter contains no «plan features»/«planning large features»
</acceptance-criteria>
<refs>Spec f0fdb479 §4–5; Plan T7. Port side depends on this task (T8).</refs>
<obstacles></obstacles>
<log></log>

<log>
- 2026-09-27 [agent] — взята в работу исполнителем
- 2026-09-27 [agent] — Done. dotnet-coder: frontmatter description/whenToUse stripped of review; «Post-Task Review Protocol (MANDATORY)» replaced by «Review Handoff (NOT self-review)» (complexity criteria list kept); Output Contract Next step reworded to orchestrator-dispatched review. next-js-coder: frontmatter stripped of review; body checked — no self-review section existed, no body change. maui-coder: frontmatter stripped of review; self-review section replaced by the same Review Handoff shape (complexity criteria kept). dotnet-architect + next-js-architect: description/whenToUse stripped of plan-features triggers (architecture triggers kept), one-line ownership note added under H1. Verified via grep: no 'review' in any coder frontmatter, no 'Post-Task Review Protocol' anywhere, no 'plan features/planning large/planning features' in architects. Scope: only 5 files under .kimi-code; mirrors (.claude/.gemini/.qwen) intentionally untouched per task constraints. No git commits.
- 2026-09-27 [agent] — executor done: all 3 ACs verified by grep and ticked; moved to review
- 2026-09-27 [agent] — Orchestrator review passed: all three coder frontmatters free of review-verbs, self-review protocols replaced by «Review Handoff (NOT self-review)» (dotnet-coder:85, maui-coder:72) with Complex criteria preserved, next-js-coder body verified as having no self-review section. Architect plan-features triggers gone (0 hits), ownership notes present (wording: **Ownership:** consulted BY `feature-orchestrator`). New drift-class leftover found during review: dotnet-architect description/whenToUse still say «MassTransit topology» — routed to T8 as a logged deviation across all four sides (same class as tutor-master C# 13).
</log>
