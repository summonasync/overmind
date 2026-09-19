---
feature: 015
slug: inotify-watch-storm
status: validated
created: 2026-09-19
validated: 2026-09-19
---

# Result 015 — Bug: host + mcp adapter inotify-watch the whole cwd tree

<result>
<h1>015 inotify watch storm — result</h1>
<p>Shipped 2026-09-19 as pack 0.1.5 (bundle-version 2), deployed to the live dogfood install ~/apps/overmind (symlink → overmind-0.1.5-linux-x64). Host and MCP adapter now pin their content root to the install dir; the caller's cwd no longer matters.</p>
<h2>What shipped</h2>
<ul>
<li>Task 01 — content-root pin: Host (WebApplicationOptions.ContentRootPath = AppContext.BaseDirectory) and MCP adapter (HostApplicationBuilderSettings.ContentRootPath); web-root resolution now tries the pack layout ../web beside the binary, then walks up for web/out (dev checkout).</li>
<li>Task 02 — Linux watch guard build/watch-guard.sh, wired into build/smoke.sh: boots both binaries from a 300-dir cwd tree against a throwaway host; host must hold &lt; 100 inotify watches, adapter &lt; 25.</li>
<li>Task 03 — CONTEXT.md topology note (pinned content roots + guard pointer), stale SPA-routing web-root sentence corrected; 0.1.5 packed and deployed; live measurement below.</li>
</ul>
<h2>Live verification evidence (2026-09-19, live install + /proc fdinfo sampling)</h2>
<ul>
<li>Health stamp after deploy: version 1.0.0+bb3fc4f7f85b0a39ddf7c5b079859fc79309dde1 — matches HEAD bb3fc4f exactly. Boot log confirms "Content root path: /home/johan-boolman/apps/overmind-0.1.5-linux-x64/host/".</li>
<li>Host inotify watches: 0 (baseline on 0.1.4 with cwd=$HOME: 43312; budget 65536 was ~64k used — the original incident).</li>
<li>MCP adapter spawned from the Overmind repo root (cwd = 91k-file tree): 1 watch at boot, 1 after a planning tools/call (baseline: 2955). Second run reproduced 1/1.</li>
<li>Machine-wide watches across the deploy: 23562 → 23524 of 65536. No material change — remaining pressure is unrelated (VS Code ~10.4k, four stale overmind-mcp processes from other sessions ~12.5k combined); flagged to the user, not cleaned (anti-scope).</li>
</ul>
<h2>Gates</h2>
<ul>
<li>dotnet build Overmind.sln: 0 warnings, 0 errors. dotnet test: 677/677 passed (baseline kept).</li>
<li>bash build/smoke.sh against dist/overmind-0.1.5-linux-x64.tar.gz: SMOKE OK (health, bundled crew kit seeded, web UI served, clean SIGTERM shutdown) + WATCH GUARD OK (host 28 &lt; 100, adapter 1 &lt; 25).</li>
</ul>
<h2>Reviews</h2>
<p>Task 01 light review PASS-WITH-NITS (audits/review-015-01.md). Task 02's light review ran in parallel with this closeout; outcome lands in audits/ independently.</p>
</result>