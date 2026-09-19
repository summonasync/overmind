---
feature: 013
task: 04
slug: mcp-agent-onboarding
status: todo
review: light
created: 2026-09-19
---

# 013/04 — Live dogfood + closeout (0.1.7)

<goal>
013 ships as version 0.1.7, deployed to ~/apps/overmind, and the acceptance dogfood passes live: a fresh agent session in a second repo onboards through the MCP tool with zero human config edits, and install --onboard lands a managed block in a fixture repo.
</goal>

<context>
Feature 013 closeout. Tasks 01–03 must be done and reviewed first. Deployment ritual and live verification are orchestrator-driven (no subagent).
</context>

<scope>
- Pack: `bash build/pack.sh --version 0.1.7 --bundle-version 2 --rids linux-x64` (invoke via bash — the +x bit flakes).
- Deploy: untar into `~/apps/overmind-0.1.7-linux-x64`, `ln -sfn` the `~/apps/overmind` symlink, restart the host headless from the install directory (nohup, log /tmp/overmind-0.1.7.log); verify `curl http://127.0.0.1:4761/api/health`.
- Live checks:
  - adapter spawned with cwd=/mnt/Ubuntu-Dev/Projects/AI-Tools/Squeezr → `overmind_onboard` returns the Squeezr project id + board summary + quickstart;
  - `overmind-mcp install claude --onboard --dry-run` in a throwaway fixture repo with a BACKLOG-prescribing CLAUDE.md → warning + plan only; real run → managed block appended, human content untouched; second run → only the block replaced;
  - a repo unknown to the host → auto-created project, created:true, visible in the UI.
- Materialize planning to disk (POST /api/projects/ff313f29-8144-488c-94e6-5ea5dde57bfd/planning/materialize), confirm drift panel clean.
- Write the result artifact for 013 (what shipped, live evidence, deferred threads), validate it, move the feature to done via the human door, unpin current.
- smoke.sh green on the installed bundle.
</scope>

<anti-scope>
- No code changes beyond what tasks 01–03 shipped (fix-loops for live findings are allowed and go through the normal task status flow).
- Do not kill other agents' sleeping overmind-mcp processes; do not touch ~/.overmind.snapshot-pre-0.6 or older installs.
</anti-scope>

<constraints>
- Host restart: `pkill -f Overmind.Host` returns a bogus exit -1 — verify via curl health, not exit code.
- Known flake: `Internal CLR error 0x80131506` on cold dotnet → `dotnet build-server shutdown`, retry.
</constraints>

<acceptance-criteria>
- [ ] 0.1.7 installed and serving; health ok; tool list includes overmind_onboard.
- [ ] All three live checks above pass with captured evidence (command output in the result artifact).
- [ ] Planning materialized, drift clean, result artifact validated, feature done, current unpinned.
- [ ] smoke.sh green.
- [ ] Changes committed and pushed (user delegated commits).
</acceptance-criteria>

<refs>
- tasks/01-onboard-endpoint.md, tasks/02-onboard-mcp-tool.md, tasks/03-install-onboard-flag.md.
- Deploy ritual: session handoff notes; build/pack.sh; build/smoke.sh.
</refs>

<obstacles>
</obstacles>

<log>
</log>