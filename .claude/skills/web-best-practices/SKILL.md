---
name: web-best-practices
description: |
  Codified web engineering rules (BP-IDs) consumed by web-coder during implementation
  and by web-code-reviewer as the review checklist. Single source of enforceable code-quality
  rules for the web lane: TypeScript strictness, RSC/client boundary, BFF pattern,
  React Query usage, accessibility.
type: prompt
whenToUse: |
  During any web implementation (coder loads it with the task file) and any web code review
  (reviewer cites violations by BP-ID).
---

# Web Best Practices — Codified Rules

Each rule has a stable BP-ID. Reviewer cites violations as `BP-NN (severity)`.
Severity: **[B]** BLOCKER, **[W]** WARNING, **[S]** SUGGESTION.
IDs are scoped to this catalog; when citing across lanes, name the catalog first (`node-best-practices BP-12`).

## TypeScript Strictness
- **BP-01 [B]** `"strict": true` everywhere; no `any`. Type-check suppressions (`@ts-ignore`, `@ts-expect-error`, `as any`, `as unknown as T`) = BLOCKER.
- **BP-02 [B]** Data crossing a boundary (HTTP response, storage, deep link) is `unknown` + narrowed — no non-null `!`, no trusting a hand-written interface alone.
- **BP-03 [W]** zod at runtime boundaries; types derived via `z.infer`. A hand-written interface duplicating a schema rots — WARNING.
- **BP-04 [W]** Discriminated unions over boolean flag combinations: `{ status: "idle" | "loading" | "ready" | "error" }`, not `{ isLoading, isLoaded, isError }`.
- **BP-05 [S]** `satisfies` for literal config objects; a chain of `as` casts to make a shape fit is a smell.

## RSC / Client Boundary
- **BP-10 [B]** RSC by default; `'use client'` only for interactivity, browser APIs, or React Query hooks. A whole page tree marked `'use client'` for one button = BLOCKER.
- **BP-11 [B]** Server-only code (secrets, db clients, service tokens) never imported from a client module; `server-only` guard on modules that must not leak into the bundle.
- **BP-12 [W]** Server components fetch via direct service/db calls — not an HTTP fetch to their own route handlers (self-call = double hop and an absolute-URL headache).
- **BP-13 [W]** Props crossing the RSC→client boundary are serializable; functions, class instances, or Symbols crossing it crash at runtime.
- **BP-14 [S]** Pass fetched data down as props instead of lifting it into client stores "just in case".

## BFF & Server Actions
- **BP-20 [B]** `'use client'` components never call external or internal backends directly — every request is proxied through the Next.js backend (Server Actions or route handlers).
- **BP-21 [B]** API keys and service credentials live server-side only. Nothing secret in client-reachable modules or `NEXT_PUBLIC_*` env — that prefix is a public contract.
- **BP-22 [W]** Server Actions validate input with zod before use and return typed result shapes — raw thrown errors as the client-facing API = WARNING.
- **BP-23 [W]** Route handlers serve non-browser consumers (webhooks, integrations); one error envelope across them; no stack traces or internals leaking to responses.
- **BP-24 [S]** Browser mutations go through Server Actions; a REST route handler for the app's own UI is redundant surface.

## React Query
- **BP-30 [B]** No `useEffect` data fetching — every server read goes through `useQuery`.
- **BP-31 [B]** Query keys centralized in `features/<domain>/queries.ts` as a factory. Inline string keys scattered across components break invalidation = BLOCKER.
- **BP-32 [W]** Mutations: Server Action + `queryClient.invalidateQueries()`. Manual `setQueryData` patching only for real optimistic UX.
- **BP-33 [W]** Read hooks expose `{ data, isPending, error, refetch }`; components render pending and error states — no assumed-away loading.
- **BP-34 [S]** `staleTime` tuned per domain; default-0 refetch storms on every window focus = tune it.

## Accessibility
- **BP-40 [B]** Semantic HTML first: `button` for actions, `a` for navigation, real headings. A `div`/`span` with `onClick` acting as a control = BLOCKER.
- **BP-41 [B]** Every `img` has meaningful `alt` (decorative → `alt=""`); icon-only buttons carry an accessible name.
- **BP-42 [W]** Form controls labeled (`<label htmlFor>`); validation errors announced (`aria-live` / `role="alert"`); focus moves to the first error.
- **BP-43 [W]** Keyboard operable: visible focus, no traps, overlays close on Esc and return focus to the trigger.
- **BP-44 [S]** Text contrast ≥ 4.5:1; dark-mode parity checked, not assumed.

## Usage
- **Coder:** load with the task file; rules are constraints, not aspirations.
- **Reviewer:** checklist = this file + task `<acceptance-criteria>` + spec-compliance. Cite as `BP-NN [B/W/S] — file:line — what's wrong — fix diff`.
- New recurring rule discovered during work → propose adding it here (reflect loop), don't patch it silently into one review.

---

## Output Contract (MANDATORY)
End with the fixed closing block, then STOP:
1. **Done** — summary + artifact paths (skill-prefixed per AGENTS.md provenance rule).
2. **Next step** — return control to the calling skill (`web-coder` / `web-code-reviewer`).
3. **Alternatives** — propose a new BP-ID to the user when a recurring rule is discovered; stop — reference skill, never runs standalone.
Suggest, never auto-proceed. The user picks the next step.
