---
feature: 063
task: 01
slug: worktree-feature-binding
status: todo
review: light
created: 2026-10-04
brief: Adapter: planning_feature_code discovery + stub matrix tests
---

# Task 01 — Adapter tool: planning_feature_code

<goal>
One call answers «where is this feature's code».
</goal>

<scope>
- Adapter-side discovery from the workspace cwd: git worktree list --porcelain + branch listing; match by slug and NNN prefix (feat/NNN-slug convention).
- Per-match report: kind (worktree/branch), name, path, last commit short hash + date, stale flag (missing path reported, not dropped); multiple matches all listed; no match → honest empty + convention hint; combined with the host feature read (status/phase) into one answer.
- Stub tests: porcelain parsing, stale worktree, multiple matches, no match.
</scope>

<anti-scope>
- Writes to git; binding storage (v2 thought).
</anti-scope>

<acceptance-criteria>
- [ ] Porcelain parsing + match matrix — stub-pinned.
- [ ] Stale reported; multiple matches listed; honest empty — pinned.
- [ ] Host status ride-along in the same answer — pinned.
</acceptance-criteria>

<refs>
- Spec 0cc52a1a; plan 947ea062 (task 1).
</refs>