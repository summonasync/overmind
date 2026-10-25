import { strict as assert } from "node:assert";
import { test } from "node:test";
import { composeFeatureSlug, featureSlugPart } from "../src/shared/lib/feature-slug.ts";

test("ascii titles fold to lowercase kebab-case", () => {
  assert.equal(featureSlugPart("Hello World"), "hello-world");
  assert.equal(featureSlugPart("Wave Chat Shell Parity"), "wave-chat-shell-parity");
  assert.equal(featureSlugPart("Feature 168"), "feature-168");
});

test("cyrillic goes through the gost transliteration", () => {
  assert.equal(featureSlugPart("Привет мир"), "privet-mir");
  assert.equal(featureSlugPart("Ёлка йощ"), "yolka-yoshch");
  assert.equal(featureSlugPart("Хижина"), "khizhina");
});

test("hard and soft signs vanish without a dash", () => {
  assert.equal(featureSlugPart("Подъезд"), "podezd");
  assert.equal(featureSlugPart("Мышь"), "mysh");
});

test("non-alphanumerics collapse to single dashes with no leading or trailing dash", () => {
  assert.equal(featureSlugPart("Привет, мир! (тест)"), "privet-mir-test");
  assert.equal(featureSlugPart("!! Привет"), "privet");
  assert.equal(featureSlugPart("Привет!!!"), "privet");
  assert.equal(featureSlugPart("  spaced  out  "), "spaced-out");
});

test("an empty part falls back to feature", () => {
  assert.equal(featureSlugPart("!!!"), "feature");
  assert.equal(featureSlugPart("   "), "feature");
  assert.equal(featureSlugPart(""), "feature");
});

test("compose starts at 001 on an empty board", () => {
  assert.equal(composeFeatureSlug([], "Hello World"), "001-hello-world");
  assert.equal(composeFeatureSlug([], "!!!"), "001-feature");
});

test("compose picks the next free number and compares numerically", () => {
  assert.equal(composeFeatureSlug(["001-alpha", "002-beta"], "Привет мир"), "003-privet-mir");
  assert.equal(composeFeatureSlug(["007-web"], "Йобнешься"), "008-yobneshsya");
  assert.equal(composeFeatureSlug(["168-one", "167-two"], "Hello"), "169-hello");
});

test("compose skips slug-exact collisions beyond the max number", () => {
  assert.equal(
    composeFeatureSlug(["003-alpha", "005-hello-world"], "Hello World"),
    "006-hello-world",
  );
});

test("the cap squeezes the slug part while the number survives", () => {
  const slug = composeFeatureSlug([], "A".repeat(80));
  assert.equal(slug.length, 64);
  assert.equal(slug, `001-${"a".repeat(60)}`);
});

test("a truncation cut on a dash boundary trims the dangling dash", () => {
  const slug = composeFeatureSlug([], `${"a".repeat(59)}-${"b".repeat(10)}`);
  assert.equal(slug, `001-${"a".repeat(59)}`);
});
