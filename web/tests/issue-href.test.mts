import { strict as assert } from "node:assert";
import { test } from "node:test";
import { issueHref } from "../src/features/issues/lib/issue-href.ts";

test("the link opens the issue detail on that project's board", () => {
  assert.equal(issueHref("board-1", "issue-9"), "/issues?project=board-1&issue=issue-9");
});

test("ids are URL-encoded into the deep link", () => {
  assert.equal(
    issueHref("proj/one", "issue&9 x"),
    "/issues?project=proj%2Fone&issue=issue%269%20x",
  );
});
