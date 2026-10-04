---
feature: 112
task: 01
slug: approval-bubble-roomier
status: inprogress
review: light
created: 2026-10-04
brief: Web: 800-char bubble text + wider window + test + result (Simple path)
---

# Task 01 — Text bound + window + tests + result

<goal>
The approval bubble carries up to 800 characters and stops being a 4-line teaser.
</goal>

<scope>
- approval-bubble.tsx: 800-char slice with «…» on longer texts (pure helper in lib, house precedent); line-clamp-4 → none within the bound; max-width one Tailwind step up; mobile full-width behavior unchanged.
- Web test pins the slice (≤800 + ellipsis) and the clamp removal.
- Result artifact.
</scope>

<anti-scope>
- Server text cap; notification center; 095 surfaces.
</anti-scope>

<acceptance-criteria>
- [ ] Slice ≤800 + ellipsis pinned; clamp removed within the bound — pinned.
- [ ] Gates green (lint/tsc/node test); result drafted.
</acceptance-criteria>

<refs>
- Spec fbd50db4; plan 300036dc.
</refs>

<log>
- 2026-10-04 [agent] — taken into work by the autonomous pipeline
</log>
