---
name: next-js-coder-reviewer
description: |
  Review Next.js/TypeScript/React code for quality, FSD compliance, React Query usage,
  BFF pattern compliance, architecture rules, and senior frontend standards.
  Use when the user asks for PR review, code quality check, or frontend audit.
type: prompt
whenToUse: |
  When reviewing Next.js code, checking for BFF violations, TypeScript strictness,
  RSC misuse, FSD structure, React Query patterns, component architecture, or frontend PR review.
---

# Next.js Code Review Skill

## Context & Documentation Order
Before reviewing code, read project documentation in this strict order:
1. `context.md` — Architecture decisions, BFF rules, service roles (Russian)
2. `Backlog.md` — Current sprint context (Mixed EN/RU)
3. `STRUCTURE.md` — Frontend code style, FSD rules, component rules, Do/Don't (English)
4. `AGENTS.md` — Tech stack, frontend conventions (English)

## Review Checklist (MUST CHECK)

### Architecture & BFF
- [ ] **No direct backend calls from `'use client'` components.** All calls go through Next.js Server Actions or API routes. Any `fetch` to non-Next.js URL from client = BLOCKER.
- [ ] **Server Actions used for mutations.** No `fetch` inside event handlers in client components.
- [ ] **RSC by default.** `'use client'` used only when justified (events, browser APIs, React hooks, React Query).
- [ ] **No `useEffect` for data fetching.** Data must be fetched in RSC or via React Query `useQuery`. `useEffect` for data loading = BLOCKER.

### FSD Compliance
- [ ] **Feature-based structure.** Domain code lives in `features/<domain>/` with `api/`, `lib/`, `queries.ts`, `types.ts`, `components/`.
- [ ] **No cross-feature imports.** Feature A does not import from Feature B internals. Only public API via `index.ts`.
- [ ] **Queries centralized.** `features/<domain>/queries.ts` exists with factory-pattern query keys.
- [ ] **Shared layer clean.** `src/shared/` contains only UI primitives, utilities, providers — no business logic.

### React Query v5
- [ ] **No `useEffect` data fetching loops.** All reads use `useQuery` with options from `queries.ts`.
- [ ] **Query keys follow factory pattern.** `domainKeys.all`, `domainKeys.lists()`, `domainKeys.detail(id)`.
- [ ] **Mutations invalidate cache.** After Server Action mutation, `queryClient.invalidateQueries()` called with correct key.
- [ ] **No `useMutation` abuse.** `useMutation` only for complex async flows; simple mutations via Server Actions + invalidation.
- [ ] **Stale time configured.** Default 60s, refetch on focus disabled (unless explicitly required).

### Code Quality
- [ ] **NO comments in code.** Code is self-explanatory via naming.
- [ ] **English only.** No Russian strings, variable names, or log messages.
- [ ] **No `any` type.** TypeScript strict mode compliance.
- [ ] **No `console.log` in production code.**
- [ ] **Proper error handling.** Server Actions must catch errors and return structured responses.
- [ ] **Loading and error states present.** `loading.tsx` and `error.tsx` where applicable.

### Components & Styling
- [ ] **One component per file.** NEVER multiple components or mixed logic in one file. Always decompose to minimal reusable units.
- [ ] **No prop drilling.** Composition preferred over deep prop passing.
- [ ] **Semantic HTML.** Proper headings, lists, buttons (not `div` with `onClick`).
- [ ] **Accessibility.** `alt` on images, `aria-label`, keyboard navigation, focus management.
- [ ] **Tailwind.** No arbitrary values without reason. Use `cn()` for conditional classes.
- [ ] **Proper `key` props** in lists. No index as key if order changes.

### State & Logic
- [ ] **No global client state for server data.** Zustand/Context only for UI state.
- [ ] **No God-Components.** >5 props or >100 lines or multiple responsibilities = must be split.
- [ ] **Custom hooks extracted** for reusable client logic.

## Review Output Format
For each issue found, provide:
1. **Severity:** `BLOCKER` (BFF violation, TypeScript error, FSD violation, React Query misuse, mixed logic in one file) / `WARNING` (style, a11y) / `SUGGESTION` (refactoring)
2. **Location:** File and line number
3. **Problem:** What violates the rules
4. **Fix:** Concrete code snippet or diff

## Communication
- **Speak Russian with the user.** Review comments and summary in Russian.
- Be direct and concise. No "good job" fluff unless truly exceptional.
- Group issues by severity. Blockers first.
