import { strict as assert } from "node:assert";
import { readFileSync } from "node:fs";
import { test } from "node:test";

test("attention prime waits for a settled successful sync before seeding seen marks", () => {
  const source = readFileSync(
    new URL("../src/widgets/app-shell/lib/use-attention-surfaces.ts", import.meta.url),
    "utf8",
  );
  assert.ok(source.includes("if (isPending || error)"));
  assert.ok(source.includes("primeAttentionSeen(awaitingIds)"));

  const guard = source.indexOf("if (isPending || error)");
  const prime = source.indexOf("primeAttentionSeen(awaitingIds)");
  assert.ok(guard >= 0 && prime > guard);
});
