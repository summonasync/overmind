import { strict as assert } from "node:assert";
import { readFileSync } from "node:fs";
import { test } from "node:test";

const issuesPage = () =>
  readFileSync(new URL("../src/pages/issues-page/ui/issues-page.tsx", import.meta.url), "utf8");

const globalRow = () =>
  readFileSync(new URL("../src/pages/issues-page/ui/global-issue-row.tsx", import.meta.url), "utf8");

const globalList = () =>
  readFileSync(new URL("../src/pages/issues-page/ui/global-issue-list.tsx", import.meta.url), "utf8");

test("the global board takes over when no project is selected", () => {
  const source = issuesPage();
  assert.equal(source.includes("Select a project first"), false);
  assert.ok(source.includes("<GlobalIssueList />"));
});

test("the per-project board still owns the project branch", () => {
  const source = issuesPage();
  assert.ok(source.includes("useIssueList(projectId, filter)"));
  assert.ok(source.includes("<IssueDetail"));
});

test("global rows carry the project badge and deep link into the project board", () => {
  const source = globalRow();
  assert.ok(source.includes("<ProjectNameBadge projectId={issue.projectId}"));
  assert.ok(source.includes("issueHref(issue.projectId, issue.id)"));
  assert.ok(source.includes("<IssueKindBadge kind={issue.kind} />"));
  assert.ok(source.includes("<IssueStatusChip status={issue.status} />"));
});

test("the global board filters by kind through the query", () => {
  const source = globalList();
  assert.ok(source.includes("useCrossProjectIssues(kind)"));
  assert.ok(source.includes("<IssueKindFilter kind={kind} onChange={setKind} />"));
});
