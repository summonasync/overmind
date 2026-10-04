---
feature: 073
task: 01
slug: project-manifest
status: todo
review: light
created: 2026-10-04
brief: Backend: Manifest field + migration + read_project_manifest tool + import with precedence + tests
---

# Task 01 — Backend: manifest field + read tool + import

<goal>
Storage, agent read path, and one-way import for the manifest.
</goal>

<scope>
- Project.Manifest (nullable markdown) + migration AddProjectManifest; update-door contract (field on update request/response).
- MCP read_project_manifest (projectId | cwd-resolved; absent → honest «not set», not an error).
- Import-manifest action: {diskPath}/AGENTS.md → CLAUDE.md → QWEN.md first-found, header line records the source, PathGuard containment.
- Tests: round-trip, precedence, containment 400, honest-absent.
</scope>

<acceptance-criteria>
- [ ] Round-trip + migration green — pinned.
- [ ] Read tool honest-absent + cwd-resolve — contract-tested.
- [ ] Import precedence AGENTS→CLAUDE→QWEN + header line + containment 400 — pinned.
</acceptance-criteria>

<refs>
- Spec 722d3acd; plan c6958bdf (task 1); 041 listing discipline.
</refs>