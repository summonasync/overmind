---
feature: 071
task: 01
slug: backlog-import-ui
status: todo
review: light
created: 2026-10-04
brief: API: scan (read-only, error-isolated) + import endpoints with PathGuard + tests
---

# Task 01 — Human-door API: scan + import

<goal>
The human door gets the same import machinery the agents have: scan (read-only) + import per folder.
</goal>

<scope>
- POST /api/projects/{id}/planning/import/scan (optional sub-path): list BACKLOG/ feature folders via ParseFeatureAsync — slug/title/status/artifact counts + already-imported marker by slug; per-folder error isolation (error note, not a failed scan).
- POST .../import (folder name): existing ImportFeatureAsync report shape.
- PathGuard containment identical to the agent door; read-only scan writes nothing.
- API tests: scan finds folders, isolation, containment 400, import report shape, already-imported marker.
</scope>

<acceptance-criteria>
- [ ] Scan endpoint: folders listed with states, one broken folder does not fail the scan — pinned.
- [ ] Containment 400 pinned; import returns the established report shape.
- [ ] Scan writes nothing — pinned.
</acceptance-criteria>

<refs>
- Spec d46a2a7c; plan 2859ccfb (task 1); agent door ImportFeature as the shape reference.
</refs>