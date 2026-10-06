---
feature: 100
task: 03
slug: tech-debt-revalidation
status: review
review: light
created: 2026-10-06
depends_on: 01
brief: web/src FSD boundary sweep + solution-wide outlier scan (sizes, naming drift, duplication); architect-review on opus, read-only, only allowed write = audits/architect-review-2026-10-06-100.md
---

# Task 03 — web FSD sweep + outlier scan (plan steps 4-5)

<goal>
Frontend FSD boundary sweep plus solution-wide outlier scan.
</goal>

<scope>
- web/src FSD cross-layer import violations across app / pages / widgets / features / shared; the absent entities/ layer judged violation vs conscious omission, with rationale.
- RSC/client boundary drift as structural signal; BFF seam shape.
- Outlier scan across src/ and web/src: file/class size distribution, multi-responsibility classes, naming drift, duplication hotspots.
</scope>

<anti-scope>
No source edits, no board mutations — findings feed task 04 classification only.
</anti-scope>

<constraints>
Executor: architect-review on opus. Read-only except one file: audits/architect-review-2026-10-06-100.md. Runs CONCURRENTLY with the sprint's coding lanes — safe because read-only.
</constraints>

<acceptance-criteria>
- [ ] FSD boundary verdict — violations with file:line or explicit clean.
- [ ] The entities/-layer question answered with rationale.
- [ ] Outlier list with counts; duplication hotspots named.
</acceptance-criteria>

<refs>
- spec 2086fff7-a874-46b0-8169-a3411449a677, plan f40abff9-612c-4d0c-9500-a91ac2efed9c (this feature) — procedure steps 4-5.
</refs>

<obstacles>
None at breakdown time.
</obstacles>

<log>
- 2026-10-06 [agent] — task created by feature-orchestrator (task breakdown after consolidated validation sitting).
- 2026-10-06 [agent] — audit task 03 → review: §8 web/FSD sweep appended (FSD direction clean on all five slices; barrel bypasses ×2 + deep imports ×2 = S-microbatch; RSC/BFF clean, one useRouter shim watch; React Query clean; entities/ DECLINED with documented return trigger; outliers: feature-workspace KEEP, project-tree EXTRACT-on-touch). 3/3 AC covered. Report audits/architect-review-2026-10-06-100.md:305-406.
</log>