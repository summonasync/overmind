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
<li>dotnet build Overmind.sln: 0 warnings, 0 errors. dotnet test: 677/677 passed (baseline kept).</li>
<li>bash build/smoke.sh against dist/overmind-0.1.5-linux-x64.tar.gz: SMOKE OK (health, bundled crew kit seeded, web UI served, clean SIGTERM shutdown) + WATCH GUARD OK (host 28 &lt; 100, adapter 1 &lt; 25).</li>
</ul>
<h2>Reviews</h2>
<p>Task 01 light review PASS-WITH-NITS (audits/review-015-01.md). Task 02's light review ran in parallel with this closeout; outcome lands in audits/ independently.</p>
</result>