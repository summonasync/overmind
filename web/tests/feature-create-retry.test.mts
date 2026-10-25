import { strict as assert } from "node:assert";
import { test } from "node:test";
import { ApiError } from "../src/shared/api/client.ts";
import { createFeatureWithCollisionRetry } from "../src/features/planning/lib/feature-create-retry.ts";
import type { PlanningFeatureFormValues, PlanningFeatureResponse } from "../src/features/planning/types.ts";

const VALUES: PlanningFeatureFormValues = {
  slug: "",
  title: "Wave Chat Shell",
  body: "",
  priority: "p2",
};

const FEATURE = { slug: "169-wave-chat-shell" } as PlanningFeatureResponse;

function collisionError(next: number): ApiError {
  return new ApiError(
    409,
    `Feature number of '168-wave-chat-shell' is already taken by '168-wave-chat-shell' (status 'inprogress') — ` +
      `NNN prefixes are the board's addressing layer and compare numerically (7 = 007). ` +
      `Rename the slug with the next free number: ${next}.`,
    "NumberCollision",
  );
}

test("a generated-slug collision retries once with the suggested number", async () => {
  const calls: string[] = [];
  const create = (async (_projectId: string, values: PlanningFeatureFormValues) => {
    calls.push(values.slug);
    if (calls.length === 1) {
      throw collisionError(169);
    }
    return FEATURE;
  }) as (projectId: string, values: PlanningFeatureFormValues) => Promise<PlanningFeatureResponse>;

  const result = await createFeatureWithCollisionRetry("p1", VALUES, create);

  assert.equal(result, FEATURE);
  assert.deepEqual(calls, ["", "169-wave-chat-shell"]);
});

test("a second collision surfaces the error instead of retrying again", async () => {
  let calls = 0;
  const create = (async () => {
    calls += 1;
    throw collisionError(169);
  }) as (projectId: string, values: PlanningFeatureFormValues) => Promise<PlanningFeatureResponse>;

  await assert.rejects(
    createFeatureWithCollisionRetry("p1", VALUES, create),
    (error: unknown) => error instanceof ApiError && error.code === "NumberCollision",
  );
  assert.equal(calls, 2);
});

test("a manual slug never retries", async () => {
  let calls = 0;
  const create = (async () => {
    calls += 1;
    throw collisionError(169);
  }) as (projectId: string, values: PlanningFeatureFormValues) => Promise<PlanningFeatureResponse>;

  await assert.rejects(createFeatureWithCollisionRetry("p1", { ...VALUES, slug: "168-my-slug" }, create));
  assert.equal(calls, 1);
});

test("other failures surface without a retry", async () => {
  let calls = 0;
  const create = (async () => {
    calls += 1;
    throw new ApiError(409, "A feature with slug '168-x' already exists in this project.", "Duplicate");
  }) as (projectId: string, values: PlanningFeatureFormValues) => Promise<PlanningFeatureResponse>;

  await assert.rejects(createFeatureWithCollisionRetry("p1", VALUES, create));
  assert.equal(calls, 1);
});

test("a collision detail without a parseable number surfaces as-is", async () => {
  let calls = 0;
  const create = (async () => {
    calls += 1;
    throw new ApiError(409, "Number taken.", "NumberCollision");
  }) as (projectId: string, values: PlanningFeatureFormValues) => Promise<PlanningFeatureResponse>;

  await assert.rejects(createFeatureWithCollisionRetry("p1", VALUES, create));
  assert.equal(calls, 1);
});
