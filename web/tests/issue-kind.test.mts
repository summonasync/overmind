import { strict as assert } from "node:assert";
import { readFileSync } from "node:fs";
import { test } from "node:test";
import { ISSUE_KINDS } from "../src/features/issues/types.ts";
import { issueKindTone } from "../src/features/issues/lib/kind-tone.ts";

const read = (path: string) =>
  readFileSync(new URL(path, import.meta.url), "utf8");

test("issues carry exactly two kinds: issue and request", () => {
  assert.deepEqual([...ISSUE_KINDS], ["issue", "request"]);
});

test("kind badges reuse shared badge tones: neutral for issues, info for requests", () => {
  assert.equal(issueKindTone("issue"), "neutral");
  assert.equal(issueKindTone("request"), "info");
});

test("list calls forward the kind filter as a query param", () => {
  const api = read("../src/features/issues/api.ts");
  assert.ok(api.includes('params.set("kind", filter.kind)'));
  assert.ok(api.includes("`/issues${query}`"));
  const queries = read("../src/features/issues/queries.ts");
  assert.ok(queries.includes("filter.status, filter.kind, filter.authorProjectId"));
  assert.ok(queries.includes('"cross-project", kind'));
});

test("project rows and the detail header show the kind badge", () => {
  const row = read("../src/features/issues/components/issue-list-row.tsx");
  assert.ok(row.includes("<IssueKindBadge kind={issue.kind} />"));
  const detail = read("../src/features/issues/components/issue-detail.tsx");
  assert.ok(detail.includes("<IssueKindBadge kind={issue.kind} />"));
});

test("the project filter bar exposes the kind select", () => {
  const filters = read("../src/features/issues/components/issue-filters.tsx");
  assert.ok(filters.includes("<IssueKindFilter kind={filter.kind}"));
  const kindFilter = read("../src/features/issues/components/issue-kind-filter.tsx");
  assert.ok(kindFilter.includes('aria-label="Filter by kind"'));
  assert.ok(kindFilter.includes("ISSUE_KINDS.map"));
});
