---
feature: 100
task: 04
slug: tech-debt-revalidation
status: review
review: light
created: 2026-10-06
depends_on: 02, 03
brief: Classify all findings (blocker/warning/suggestion + file:line), write the final report audits/architect-review-2026-10-06-100.md (7 sections), P0 follow-up sketches + numbered ledger items, coverage self-check; architect-review on opus, read-only except this one file
---

# Task 04 — Classification + final report + self-check (plan steps 6-7)

<goal>
Classify all findings, write the final report, self-verify coverage.
</goal>

<scope>
- Severity classification of every finding: blocker | warning | suggestion, each with file:line, evidence, proposed action.
- P0 follow-up sketches (title + one-paragraph goal + file map); cosmetic findings as numbered microbatch ledger items.
- Write audits/architect-review-2026-10-06-100.md with the 7 contract sections in order: executive summary, methodology, findings, god-file verdicts, follow-up map, coverage checklist, scope context (wave commits 86e8eed / 2fa454c / 04104b9 listed as context, no landed decision reopened).
- Self-check: every named area covered findings-or-clean; git status clean except audits/.
</scope>

<anti-scope>
No board mutations (no feature creation, no ledger writes) — flow-back happens later, on user request. No source edits.
</anti-scope>

<constraints>
Executor: architect-review on opus. The only write target is audits/architect-review-2026-10-06-100.md. The report itself is the deliverable — review light.
</constraints>

<acceptance-criteria>
- [ ] Report exists with all 7 sections in order.
- [ ] Every P0 finding has a follow-up sketch or numbered ledger item, pre-sized with the file map.
- [ ] Coverage checklist self-verified: three god-files + src direction/DI/EF + web FSD + outlier scan, each findings-or-clean.
- [ ] git status after the run: only audits/ changed.
</acceptance-criteria>

<refs>
- spec 2086fff7-a874-46b0-8169-a3411449a677 (AC cross-map source), plan f40abff9-612c-4d0c-9500-a91ac2efed9c (this feature) — procedure steps 6-7.
</refs>

<obstacles>
None at breakdown time.
</obstacles>

<log>
- 2026-10-06 [agent] — task created by feature-orchestrator (task breakdown after consolidated validation sitting).
- 2026-10-06 [agent] — audit task 04 → review: §9 classification (0 blocker-candidates, 4 WARNINGs, 11 SUGGESTIONs → 10 new ledger items #250-#259 + 1 into F5 sketch, 8 declines/watch incl. 5 dedupes), §10 final report + 5 structural follow-up proposals (F1-F5) + superseding-ADR proposal §10.8 (human-gated), §11 self-check (spec AC → sections, clean audit git status). Re-verified on fresh HEAD; fixed a stale tool-count fact. Report audits/architect-review-2026-10-06-100.md (570 lines).
</log>