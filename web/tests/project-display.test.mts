import { strict as assert } from "node:assert";
import { test } from "node:test";
import {
  PROJECT_NAME_FALLBACK,
  projectDisplayName,
} from "../src/shared/lib/project-display.ts";

const OVERMIND_ID = "11111111-1111-1111-1111-111111111111";
const BLANK_NAME_ID = "22222222-2222-2222-2222-222222222222";

const projects = [
  { id: OVERMIND_ID, name: "Overmind" },
  { id: BLANK_NAME_ID, name: "   " },
];

test("known id resolves to the project name", () => {
  assert.equal(projectDisplayName(projects, OVERMIND_ID), "Overmind");
});

test("unknown id falls back to the truncated raw id", () => {
  assert.equal(projectDisplayName(projects, "99999999-aaaa-bbbb-cccc-dddddddddddd"), "99999999");
});

test("blank id falls back to the generic label", () => {
  assert.equal(projectDisplayName(projects, ""), PROJECT_NAME_FALLBACK);
  assert.equal(projectDisplayName(projects, "   "), PROJECT_NAME_FALLBACK);
});

test("padded unknown id trims into the truncated-id fallback", () => {
  assert.equal(projectDisplayName(projects, "  99999999-aaaa-bbbb-cccc-dddddddddddd  "), "99999999");
});

test("padded known id still resolves to the project name", () => {
  assert.equal(projectDisplayName(projects, `  ${OVERMIND_ID}  `), "Overmind");
});

test("known id with a blank stored name falls back to the truncated id", () => {
  assert.equal(projectDisplayName(projects, BLANK_NAME_ID), "22222222");
});

test("fallback never renders empty, even with an empty project list", () => {
  assert.notEqual(projectDisplayName([], OVERMIND_ID), "");
  assert.notEqual(projectDisplayName([], ""), "");
});

test("fallback keeps whitespace trimmed around a known name", () => {
  assert.equal(
    projectDisplayName([{ id: "abc", name: "  Mindlash Studio " }], "abc"),
    "Mindlash Studio",
  );
});
