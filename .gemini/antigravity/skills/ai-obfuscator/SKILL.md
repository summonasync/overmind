---
name: ai-obfuscator
description: |
  Transform AI-generated code into code that appears human-written.
  Remove typical LLM artifacts, boilerplate patterns, and predictable structures;
  inject organic imperfections a real developer would leave.
  ALWAYS executed by the strongest reasoning model (selected in Agent Manager) — never by the model that wrote the code.
type: prompt
whenToUse: |
  In coding-challenge (test task) flow, after code review passes and before final validation.
  Also when the user explicitly asks to "humanize" or "obfuscate AI traces" in code.
---

# AI Trace Obfuscation Skill

> **Model policy:** Obfuscation is ALWAYS performed by the strongest reasoning model (selected in Agent Manager). The model that wrote the code must not obfuscate its own output — self-obfuscation leaves correlated artifacts.

## Purpose
Transform AI-generated code into code that appears human-written. Remove typical LLM artifacts, boilerplate patterns, and predictable structures. Inject organic imperfections, personal style choices, and context-specific decisions that a real developer would make.

## When to Use
- Before committing code that was generated or heavily assisted by AI
- When code review might flag "this looks AI-generated"
- When contributing to open-source or client projects where AI usage needs to be indistinguishable
- When the user explicitly asks to "humanize" or "obfuscate AI traces"

---

## 1. AI Code Signatures to Detect & Remove

### 1.1. Predictable Comment Patterns
**AI signs:**
- `// Fetch data from the API`
- `// Handle click event`
- `// This component renders the user profile`
- Comments that state the obvious
- Perfectly formatted JSDoc on every function
- Comments in English when the dev team speaks Russian

**Obfuscation:**
- Remove 70% of comments. Keep only non-obvious ones.
- If keeping comments — make them terse, slightly informal, or in team language.
- Add occasional typos or shorthand in comments: `// TODO: fix this shit later`, `// hack for safari`, `// dunno why but works`.
- Sometimes write comments in Russian even if code is English.

### 1.2. Perfect Consistency
**AI signs:**
- Every `useEffect` has the same structure
- Every handler is named `handleClick`, `handleSubmit`, `handleChange`
- Identical error handling pattern copied everywhere
- Perfect alphabetical ordering of imports
- Consistent quote style (all single or all double) across entire codebase

**Obfuscation:**
- Vary naming: `onClick` → `clickHandler`, `btnClick`, `handleBtnClick`, `clicky` (in trivial cases).
- Mix quote styles within a file (not randomly — with "mood").
- Sometimes destructure props, sometimes not — based on "what felt right at the moment".
- Inconsistent spacing: sometimes 1 blank line, sometimes 2 between blocks.
- Occasionally leave a `console.log` with a debug message.

### 1.3. Over-Engineered Patterns
**AI signs:**
- Every component is memoized (`React.memo`) regardless of need
- Every callback wrapped in `useCallback` with empty deps
- Custom hooks for everything, even 2-line logic
- Perfect separation of concerns even when unnecessary
- Abstraction for the sake of abstraction

**Obfuscation:**
- Inline small logic directly in JSX if it's readable enough.
- Skip `useCallback` for handlers that aren't passed to memoized children.
- Skip `React.memo` on leaf components that never re-render anyway.
- Sometimes write a 15-line component inline in a parent file "because it was faster".
- Use `let` instead of `const` when you "might need to reassign later" (even if you won't).

### 1.4. Redux Boilerplate Overload
**AI signs:**
- Perfect slice structure: `name`, `initialState`, `reducers`, `extraReducers` always in same order
- Every async action uses `createAsyncThunk` with identical try/catch
- Selectors are always `createSelector` even for simple property access
- Action names are perfectly namespaced: `users/fetchUsers`, `posts/updatePost`

**Obfuscation:**
- Sometimes use raw `dispatch({ type: 'users/load' })` for simple cases.
- Mix RTK and raw Redux patterns in different files.
- Write selectors as plain functions when Reselect is overkill.
- Name actions inconsistently: `loadUsers`, `getUsers`, `fetchUserData` — whatever came to mind first.
- Occasionally mutate state directly in a reducer (if using Immer — it's fine, but looks less "AI-perfect").

### 1.5. Next.js App Router AI Smell
**AI signs:**
- Every Server Component is perfectly pure with zero client hints
- Every data fetch uses the exact same `fetch` pattern with `cache: 'force-cache'`
- Perfect error.tsx and loading.tsx in every route segment
- Metadata is always exported as a perfect object

**Obfuscation:**
- Sometimes add `'use client'` even when you could have passed data as props — "was in a hurry".
- Mix `fetch` and ORM/data library calls inconsistently.
- Skip `loading.tsx` for simple routes — "didn't need it".
- Hardcode some metadata inline instead of exporting a perfect object.
- Use `async/await` in some components, `.then()` in others — based on "vibe".

### 1.6. C# / .NET AI Smell
**AI signs:**
- XML doc comments on every public member
- Every service perfectly follows the same constructor → guard clauses → fields pattern
- Identical `_logger.LogInformation("...")` templates everywhere
- Perfect `ArgumentNullException.ThrowIfNull` on every parameter of every method
- Records/primary constructors used with machine-like uniformity

**Obfuscation:**
- Drop most XML docs; keep only where the API is genuinely non-obvious.
- Vary guard-clause style: sometimes `ThrowIfNull`, sometimes manual `if`, sometimes none in internal helpers.
- Mix `_logger` usage density — not every method logs entry/exit.
- Occasionally leave an unused `using` or an old-style constructor in one class.
- Vary between expression-bodied members and full bodies without a system.

---

## 2. Humanization Techniques

### 2.1. Organic Naming
- Use domain-specific slang or team abbreviations.
- Sometimes use single-letter variables in tight scopes: `u` for user in a map callback.
- Name boolean props inconsistently: `isOpen` vs `open` vs `visible` vs `show`.
- Use Russian transliteration for internal vars: `userKarta` instead of `userCard` if the team is Russian.

### 2.2. Imperfect Refactoring Traces
- Leave a commented-out block: `// old version, keep for now`.
- Have a TODO that references a specific person or ticket: `// TODO: @sasha fix types`.
- Sometimes import something unused and leave it (comment it with `// needed?`).
- Have slightly different file structures in different features — "evolved over time".

### 2.3. Style Inconsistencies
- Mix early return and ternary in different components.
- Sometimes use `&&` for conditional rendering, sometimes ternary.
- Vary between arrow functions and function declarations.
- Sometimes destructure in params, sometimes in body.
- Use template literals even when not needed: `` `Hello ${name}` `` instead of string concatenation — just because.

### 2.4. "Mood" Based Decisions
- When tired: longer functions, more nesting, less abstraction.
- When inspired: creative variable names, experimental patterns.
- When debugging: `console.log` left behind, commented experiments.
- When rushed: copy-paste with minor tweaks instead of extracting a component.

---

## 3. Language-Specific Tactics

### React Components
```tsx
// BEFORE (AI smell)
interface UserCardProps {
  user: User;
  onSelect: (id: string) => void;
}

export const UserCard: React.FC<UserCardProps> = React.memo(({ user, onSelect }) => {
  const handleClick = useCallback(() => {
    onSelect(user.id);
  }, [onSelect, user.id]);

  return (
    <div className="user-card" onClick={handleClick}>
      <span className="user-name">{user.name}</span>
    </div>
  );
});

// AFTER (humanized)
interface Props {
  user: User;
  onSelect(id: string): void;
}

// TODO: memo? check perf later
export function UserCard({ user, onSelect }: Props) {
  return (
    <div 
      className="user-card" 
      onClick={() => onSelect(user.id)}  // inline is fine here
    >
      <span className="user-name">{user.name}</span>
    </div>
  );
}
```

### Redux Slice
```ts
// BEFORE (AI smell)
import { createSlice, createAsyncThunk } from '@reduxjs/toolkit';

export const fetchUsers = createAsyncThunk(
  'users/fetchUsers',
  async (_, { rejectWithValue }) => {
    try {
      const response = await api.getUsers();
      return response.data;
    } catch (error) {
      return rejectWithValue(error.message);
    }
  }
);

const usersSlice = createSlice({
  name: 'users',
  initialState: { data: [], loading: false, error: null },
  reducers: {},
  extraReducers: (builder) => {
    builder
      .addCase(fetchUsers.pending, (state) => {
        state.loading = true;
      })
      .addCase(fetchUsers.fulfilled, (state, action) => {
        state.loading = false;
        state.data = action.payload;
      })
      .addCase(fetchUsers.rejected, (state, action) => {
        state.loading = false;
        state.error = action.payload;
      });
  },
});

// AFTER (humanized)
import { createSlice, createAsyncThunk } from '@reduxjs/toolkit';

// simple loader, no need for extra boilerplate
export const loadUsers = createAsyncThunk('users/load', async () => {
  const res = await api.getUsers();
  return res.data; // let unwrap handle errors
});

const slice = createSlice({
  name: 'users',
  initialState: {
    list: [] as User[],
    loading: false,
    error: null as string | null,
  },
  reducers: {
    clearError(state) {
      state.error = null; // manual cleanup action
    },
  },
  extraReducers: (b) => {
    b.addCase(loadUsers.pending, (s) => {
      s.loading = true;
    });
    b.addCase(loadUsers.fulfilled, (s, a) => {
      s.list = a.payload;
      s.loading = false;
    });
    b.addCase(loadUsers.rejected, (s, a) => {
      s.loading = false;
      s.error = a.error.message || 'unknown';
    });
  },
});

export const { clearError } = slice.actions; // named export when needed
export default slice.reducer;
```

### Next.js Server Component
```tsx
// BEFORE (AI smell)
export const metadata = {
  title: 'Users',
  description: 'List of users',
};

export default async function UsersPage() {
  const users = await fetch('https://api.example.com/users', {
    cache: 'force-cache',
  }).then((res) => res.json());

  return (
    <main>
      <h1>Users</h1>
      <UserList users={users} />
    </main>
  );
}

// AFTER (humanized)
// NOTE: metadata causing hydration issues? check later
export const metadata = {
  title: 'Users',
};

export default async function UsersPage() {
  // using our internal client, not raw fetch
  const users = await api.users.list();

  if (!users.length) {
    return <div>пока никого нет</div>; // early return, no wrapper
  }

  return (
    <main>
      <h1 className="text-2xl font-bold mb-4">Users</h1>
      <UserList users={users} />
    </main>
  );
}
```

---

## 4. Obfuscation Checklist

Before marking code as "humanized", verify:

- [ ] No obvious AI comments ("This function...", "The following code...")
- [ ] At least one inconsistency in style (naming, spacing, quotes)
- [ ] At least one "organic" imperfection (TODO, console.log, commented block)
- [ ] Not every function is memoized/callback-wrapped
- [ ] Redux actions have varied naming, not perfect namespacing
- [ ] Some components are slightly over or under-engineered
- [ ] Imports are not perfectly sorted (or sorted by "vibe", not alphabetically)
- [ ] No perfect JSDoc/XML-doc on every export
- [ ] At least one Russian word or transliteration in non-public code
- [ ] Error handling varies — not identical try/catch everywhere

---

## 5. Anti-Patterns (NEVER Do)

Despite obfuscation, NEVER introduce:
- Security vulnerabilities (raw HTML injection, exposed secrets)
- Performance regressions (unnecessary re-renders, missing deps)
- Broken logic (the code must still work correctly)
- Incomprehensible variable names (`a1`, `b2` in public APIs)
- Inconsistent formatting within a single function (keep it readable)

**Rule**: Obfuscate the *origin*, not the *quality*. Code must remain production-ready.

---

## 6. Workflow

1. **Analyze**: Identify AI signatures in the provided code.
2. **Plan**: Decide which humanization techniques to apply (don't overdo — 3-4 changes per file is enough).
3. **Transform**: Apply changes while preserving functionality.
4. **Verify**: Run build, tests, lint. Fix real issues, keep "organic" ones if harmless.
5. **Review**: Check against the Obfuscation Checklist.

---

## 7. Context Awareness

- If the codebase already has a strong human style — match it, don't impose a new one.
- If the team uses Russian for internal naming — use Russian transliteration.
- If the project is legacy — add "evolution" traces (old patterns mixed with new).
- If the project is fresh — make it look like "first commit enthusiasm" with some rough edges.

## Communication
- **Speak Russian with the user.**
- After finishing: report which files were transformed, which techniques were applied per file, and ask: "Хотите сделать коммит?"
- **NEVER commit or push automatically.**
