import { strict as assert } from "node:assert";
import { test } from "node:test";
import {
  DEFAULT_NUMBERING,
  FEATURE_NUMBER_STYLES,
  featureNumber,
  formatFeatureNumber,
  isValidFeatureNumberPrefix,
} from "../src/shared/lib/feature-number.ts";

const SLUG = "066-feature-number-styles";

test("hash style keeps the historical #NNN shape", () => {
  assert.equal(formatFeatureNumber(SLUG), "#066");
  assert.equal(formatFeatureNumber(SLUG, "hash"), "#066");
});

test("plain style shows the bare number", () => {
  assert.equal(formatFeatureNumber(SLUG, "plain"), "066");
});

test("key style puts the project prefix in front", () => {
  assert.equal(formatFeatureNumber(SLUG, "key", "OM"), "OM-066");
  assert.equal(formatFeatureNumber("104-materialization-off-write-path", "key", "OVM_2"), "OVM_2-104");
});

test("key style without a usable prefix falls back to the hash shape", () => {
  assert.equal(formatFeatureNumber(SLUG, "key", null), "#066");
  assert.equal(formatFeatureNumber(SLUG, "key", ""), "#066");
  assert.equal(formatFeatureNumber(SLUG, "key", "om"), "#066");
});

test("slugs without a number format to null in every style", () => {
  for (const style of FEATURE_NUMBER_STYLES) {
    assert.equal(formatFeatureNumber("backlog-import-ui", style, "OM"), null);
    assert.equal(formatFeatureNumber("", style, "OM"), null);
  }
});

test("the number survives a letter suffix and stops at the dash", () => {
  assert.equal(featureNumber("104a-off-write"), "104a");
  assert.equal(featureNumber("66"), "66");
  assert.equal(featureNumber("x66-slug"), null);
  assert.equal(featureNumber("066-FEATURE"), "066");
});

test("prefix validation mirrors the backend contract", () => {
  assert.equal(isValidFeatureNumberPrefix("OM"), true);
  assert.equal(isValidFeatureNumberPrefix("KEY_2"), true);
  assert.equal(isValidFeatureNumberPrefix("A"), false);
  assert.equal(isValidFeatureNumberPrefix("2AB"), false);
  assert.equal(isValidFeatureNumberPrefix("om"), false);
  assert.equal(isValidFeatureNumberPrefix("OM-2"), false);
  assert.equal(isValidFeatureNumberPrefix("A".repeat(16)), true);
  assert.equal(isValidFeatureNumberPrefix("A".repeat(17)), false);
});

test("the default numbering matches the untouched hash look", () => {
  assert.deepEqual(DEFAULT_NUMBERING, { style: "hash", prefix: null });
});
