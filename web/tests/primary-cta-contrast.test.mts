import { strict as assert } from "node:assert";
import { readFileSync, readdirSync } from "node:fs";
import { test } from "node:test";

// The primary CTA shipped `bg-primary text-primary-foreground` with one static label colour,
// and no single colour survives both of its fills: dark #14141c measures 4.6290:1 on
// --primary but 3.4034:1 on --primary-hover, white measures 5.3815:1 on the hover fill but
// 3.9567:1 at rest. Owner ruling 2026-11-18, verbatim: «Тёмный в покое, белый на hover
// (рекомендую)» — so the button carries a label token PER STATE and this file pins both.
// Every ratio below is recomputed from globals.css plus the class strings on disk, so
// changing either side of a pair — the hex, or which utility a consumer wears — reds it.
//
// Deliberate limits:
// - Disabled states are out. `disabled:opacity-50` composites label and fill over the panel
//   (~2.05:1 dark over --surface) and WCAG 1.4.3 exempts inactive UI components.
// - Text size is not exempted anywhere either: both consumers render text-sm/text-xs, so the
//   4.5:1 text floor is the one that applies. Raising the label size to dodge the ratio is
//   banned by the card's anti-scope.

const AA_TEXT = 4.5;
const REST_LABEL = "primary-foreground";
const HOVER_LABEL = "primary-hover-foreground";
const SIZE_UTILS = /^(?:xs|sm|base|lg|xl|2xs|2xl|3xl|4xl|5xl|9xl)$/;

const globals = readFileSync(new URL("../src/app/globals.css", import.meta.url), "utf8");

const tokenHex = (name: string) => {
  const match = new RegExp(`--color-${name}:\\s*(#[0-9a-fA-F]{6})`).exec(globals);
  assert.ok(match, `--color-${name} is not a literal hex in globals.css — resolve or exempt it here`);
  return match[1].toLowerCase();
};

// A consumer's label/fill utility may name something that is not a theme colour at all
// (`text-white`, an arbitrary `text-[#fff]`); say which utility, not just that a token is missing.
const resolve = (kind: "label" | "fill", name: string) => {
  const match = new RegExp(`--color-${name}:\\s*(#[0-9a-fA-F]{6})`).exec(globals);
  assert.ok(
    match,
    `the ${kind} utility "${(kind === "label" ? "text-" : "bg-") + name}" names no --color-${name} token in globals.css`,
  );
  return match[1].toLowerCase();
};

const linear = (byte: number) => {
  const s = byte / 255;
  return s <= 0.03928 ? s / 12.92 : ((s + 0.055) / 1.055) ** 2.4;
};
const luminance = (hex: string) => {
  const n = parseInt(hex.slice(1), 16);
  return 0.2126 * linear((n >> 16) & 255) + 0.7152 * linear((n >> 8) & 255) + 0.0722 * linear(n & 255);
};
const contrast = (a: string, b: string) => {
  const [x, y] = [luminance(a), luminance(b)];
  return (Math.max(x, y) + 0.05) / (Math.min(x, y) + 0.05);
};

const sources: string[] = [];
const collect = (dir: URL) => {
  for (const entry of readdirSync(dir, { withFileTypes: true })) {
    if (entry.isDirectory()) collect(new URL(`${entry.name}/`, dir));
    else if (/\.(ts|tsx)$/.test(entry.name)) sources.push(new URL(entry.name, dir).pathname);
  }
};
collect(new URL("../src/", import.meta.url));

// One class group per source line: every CTA here declares both states on one line. A
// consumer that reflows across lines drops out of the counts and the denominator tests go
// red, so the grouping cannot rot silently.
const UTILITIES = /(?:^|[\s"'`])(?:(hover|focus|active|disabled):)?(bg|text)-([a-z][a-z0-9-]*)(?![\w/])/g;

interface State {
  label: string;
  fill: string;
}
interface Consumer {
  at: string;
  rest: State;
  hover?: State & { wired: boolean };
}

const consumers: Consumer[] = [];
const hardcodedOnPrimary: string[] = [];

for (const file of sources) {
  const body = readFileSync(file, "utf8");
  body.split("\n").forEach((line, index) => {
    const at = `${file.slice(new URL("../", import.meta.url).pathname.length)}:${index + 1}`;
    const fills: Record<string, string[]> = { rest: [], hover: [] };
    const labels: Record<string, string[]> = { rest: [], hover: [] };
    for (const [, variant, kind, name] of line.matchAll(UTILITIES)) {
      if (kind === "bg") fills[variant === "hover" ? "hover" : "rest"].push(name);
      else if (!SIZE_UTILS.test(name)) labels[variant === "hover" ? "hover" : "rest"].push(name);
    }

    if (labels.rest.includes(REST_LABEL) && fills.rest.length > 0) {
      const consumer: Consumer = { at, rest: { label: REST_LABEL, fill: fills.rest[0] } };
      if (fills.hover.length > 0) {
        // An unwired hover keeps wearing the resting label — that is exactly the 3.4034:1
        // regression this card was amended for, so record the fallback and let the ratio judge it.
        consumer.hover = {
          label: labels.hover[0] ?? REST_LABEL,
          fill: fills.hover[0],
          wired: labels.hover.includes(HOVER_LABEL),
        };
      }
      consumers.push(consumer);
    }

    // A hardcoded white on either primary fill bypasses the token entirely; §2 of the
    // brand-book requires the value to come from a token, and the hover token exists now.
    const primaryFill = [...fills.rest, ...fills.hover].some(f => f === "primary" || f === "primary-hover");
    const hardcoded = labels.rest.includes("white") || labels.hover.includes("white") || /text-\[#f{3,6}\]/.test(line);
    if (primaryFill && hardcoded) hardcodedOnPrimary.push(at);
  });
}

const restPairs = consumers.map(c => ({ at: c.at, ...c.rest }));
const hoverPairs = consumers.filter(c => c.hover).map(c => ({ at: c.at, ...c.hover! }));

test("the at-rest label token clears AA on the primary fill", () => {
  const fg = tokenHex(REST_LABEL);
  const bg = tokenHex("primary");
  const ratio = contrast(fg, bg);
  assert.ok(
    ratio >= AA_TEXT,
    `--${REST_LABEL} ${fg} on --primary ${bg} = ${ratio.toFixed(4)}:1, floor is ${AA_TEXT}:1 ` +
      `(the white label this replaced measured 3.9567:1)`,
  );
});

test("the hover label token clears AA on the hover fill", () => {
  const fg = tokenHex(HOVER_LABEL);
  const bg = tokenHex("primary-hover");
  const ratio = contrast(fg, bg);
  assert.ok(
    ratio >= AA_TEXT,
    `--${HOVER_LABEL} ${fg} on --primary-hover ${bg} = ${ratio.toFixed(4)}:1, floor is ${AA_TEXT}:1 ` +
      `(the dark label measured 3.4034:1 there — the reason this second token exists)`,
  );
});

test("every at-rest consumer pair measures AA on its own fill", () => {
  for (const p of restPairs) {
    const ratio = contrast(resolve("label", p.label), resolve("fill", p.fill));
    assert.ok(ratio >= AA_TEXT, `${p.at} rest --${p.label} on bg-${p.fill} = ${ratio.toFixed(4)}:1, floor is ${AA_TEXT}:1`);
  }
});

test("every hover consumer pair measures AA on its own fill", () => {
  for (const p of hoverPairs) {
    const ratio = contrast(resolve("label", p.label), resolve("fill", p.fill));
    assert.ok(
      ratio >= AA_TEXT,
      `${p.at} hover --${p.label} on hover:bg-${p.fill} = ${ratio.toFixed(4)}:1, floor is ${AA_TEXT}:1` +
        (p.wired ? "" : " — and the consumer wears no hover:text-* token at all, so it falls back to the resting label"),
    );
  }
});

test("no consumer hardcodes white on a primary fill", () => {
  assert.deepEqual(hardcodedOnPrimary, [], `hardcoded white label is back on a primary fill at ${hardcodedOnPrimary.join(", ")}`);
});

test("both states are still being measured", () => {
  // The vacuous-green guard: 2 rest consumers and 2 hover pairs today; if a CTA stops using
  // the token, or stops declaring a hover fill, one of these counts drops and the pin says so
  // instead of quietly measuring nothing.
  assert.ok(restPairs.length >= 2, `expected >= 2 at-rest consumers of --${REST_LABEL}, found ${restPairs.length}`);
  assert.ok(hoverPairs.length >= 2, `expected >= 2 hover pairs to measure, found ${hoverPairs.length}`);
  const unwired = hoverPairs.filter(p => !p.wired);
  assert.deepEqual(unwired.map(p => p.at), [], `hover fill without a hover label token at ${unwired.map(p => p.at).join(", ")}`);
});
