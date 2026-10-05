---
feature: 063
task: 01
slug: worktree-feature-binding
status: done
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
- [x] Porcelain parsing + match matrix — stub-pinned.
- [x] Stale reported; multiple matches listed; honest empty — pinned.
- [x] Host status ride-along in the same answer — pinned.
</acceptance-criteria>

<refs>
- Spec 0cc52a1a; plan 947ea062 (task 1).
</refs>

<log>
- 2026-10-05 [agent] — [agent] Claimed by Van (claude), taken by orchestrator — dispatching dotnet-coder (sonnet). Runs parallel to 073/02 web coder (disjoint lanes: MCP adapter + stub tests vs web/).
- 2026-10-05 [agent] — Coding round complete (dotnet-coder, sonnet; gates re-verified by orchestrator — build 0w, Mcp 486 (+22), format clean; checkpoint 8aa947b): planning_feature_code with IGitCli seam (10s timeout, kill process tree, honest non-zero text), porcelain + for-each-ref parsers, NNN-token matching with boundaries, stale worktrees reported, worktree+branch listed without dedup (per spec), host ride-along fails independently, live-git smoke passed in this repo. Declared for reviewer: 4th private copy of ResolveProjectIdAsync (extraction candidate → microbatch); C-quoted exotic worktree paths degrade to stale=true (v1 assumption); real git never shelled in tests (StubGitCli + tools/list only). Dispatching light review.
- 2026-10-05 [agent] — [agent] Light review PASS-WITH-NITS (audits/review-063-01.md; gates re-run: build 0w, Mcp 486, format clean). W1+W2 fixed in-tree (4eac860): git killed on EVERY cancellation path (external ct used to orphan the process), branch-heads dictionary Ordinal (case-differing legal branch pairs Foo/foo threw ArgumentException). Security verified: slug never reaches git argv (matches in memory; only constants + bounded hex sha in argv), kill tree on timeout honest. N1 (single-match git-log failure kills the answer) + N2 (hint carries a hardcoded example) → microbatch; 4th ResolveProjectIdAsync copy → microbatch extraction candidate (#231-adjacent). All 3 AC ticked. Closing task.
</log>
