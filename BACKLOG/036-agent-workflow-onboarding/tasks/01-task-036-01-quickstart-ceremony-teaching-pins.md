---
feature: 036
task: 01
slug: agent-workflow-onboarding
status: todo
review: light
created: 2026-10-06
brief: Quickstart ceremony: rework the numbered section of OnboardQuickstart.cs into the gated ceremony (tools per step, proof-flow gates, execution contracts, result+done tap); 032 blocks byte-identical. New WorkflowTeachingContractTests.cs: ceremony pins + tool-name anti-drift guard vs live ListTools + 032 regression mirror.
---

# Task 036/01 — Quickstart ceremony + teaching pins

<goal>
The onboard quickstart (OnboardQuickstart.Text) teaches the gated ceremony: create (planning_create_feature) → spec (planning_upsert_artifact kind=spec) → human validation with the proof flow → plan (kind=plan) → same gate → tasks (planning_upsert_tasks; each an execution contract; a sub-agent receives ONLY its own task artifact via planning_read_artifact) → work loop (planning_current_task, implement, verify, planning_set_status with a logNote, review before a task closes) → result (kind=result) → human done tap. A skill-less agent reading ONLY this payload can recite the order and each step's tool. Pins hold it there.
</goal>

<scope>
- src/Overmind.Host/Api/OnboardQuickstart.cs — rework the numbered section ONLY: steps 1–3 unchanged (onboard; board/queue+claim; current_task+STOP), step 4 becomes the ceremony, step 5 becomes close-out (result artifact + human done tap). ~10 added lines, no tables. Every 032 block below the numbered list stays byte-identical (lifecycle arrow line, review invariant, auto-transitions, demote rule, per-kind status listing, claims block, edge rules, import lines, marker legend).
- tests/Overmind.Mcp.Tests/WorkflowTeachingContractTests.cs — new: (a) one ShouldContain pin per stage+tool mapping and per gate phrase; (b) the anti-drift guard — regex-extract tool-like tokens ((overmind|planning)_[a-z_]+, plus list_projects, list_entries, read_entry, issues_[a-z_]+) from OnboardQuickstart.Text AND the session_bootstrap text (both branches), assert each exists in the live ListToolsAsync name set; (c) a regression mirror of 032's key pinned sentences.
</scope>

<anti-scope>
- No behavior change: no new tools, refusals, STOP paths, payload fields. No overmind://workflow resource. No server-instructions changes (Program.cs). No lifecycle/model changes. No git/commit teaching. No web UI. Descriptions other than create_feature untouched (task 036/03 owns that one).
</anti-scope>

<constraints>
- Gate wording is mode-honest: the proof flow is taught as notify → wait for the human's reply → planning_validate (true in both humanGate modes); nothing promises a STOP the current mode does not deliver.
- Reuse the canonical phrases verbatim: "overmind_notify(kind=approval, featureSlug)", wait for the human's reply, planning_validate; "every edit voids the reply".
- The per-kind status listing sentence keeps its parseable shape (ListedStatusNames cuts at ';'/'.' — none may appear inside that sentence).
- English, terse; dotnet-coder rules apply (no comments, etc.).
</constraints>

<acceptance-criteria>
- [ ] The numbered section names every ceremony stage with its tool (create/spec/plan/tasks/loop/result) and both validation gates with the proof flow; the done tap is named.
- [ ] WorkflowTeachingContractTests pins the ceremony phrases and passes.
- [ ] The anti-drift guard passes against the live tools list for quickstart AND both bootstrap branches.
- [ ] All 032 StatusModelContractTests pins stay green without edits to those blocks.
- [ ] dotnet build Overmind.sln green; dotnet test Overmind.sln --filter "WorkflowTeachingContractTests|StatusModelContractTests" green.
</acceptance-criteria>

<refs>
- Spec: 036/S (a8d38f0d-37c9-4513-948e-30a227c1a566, validated). Plan: 036/P (66a76b70-ca36-46a8-ada4-6527042ad4ab, validated) — slice 1.
- 032-status-model-self-documentation — the pinned blocks and pin file shared with this surface.
</refs>

<obstacles>
- none
</obstacles>

<log>
- 2026-10-06 [agent] — task created from validated plan 66a76b70 (slice 1).
</log>