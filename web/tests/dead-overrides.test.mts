import { strict as assert } from "node:assert";
import { readdirSync, readFileSync, writeFileSync } from "node:fs";
import { join, relative } from "node:path";
import { test } from "node:test";
import { fileURLToPath } from "node:url";

const webDir = fileURLToPath(new URL("..", import.meta.url));
const repoDir = fileURLToPath(new URL("../..", import.meta.url));
const srcDir = join(webDir, "src");
const uiDir = join(srcDir, "shared", "ui");
const cssChunkDir = join(webDir, "out", "_next", "static", "chunks");
const reportPath = join(webDir, "tests", "dead-overrides.current.txt");

const PRIMITIVE_FILES = {
  badge: "badge.tsx",
  button: "button.tsx",
  select: "select.tsx",
} as const;

type Primitive = keyof typeof PRIMITIVE_FILES;

const PRIMITIVE_TAG: Record<string, Primitive> = {
  Badge: "badge",
  Button: "button",
  Select: "select",
};

const PADDING_TOKEN = /^(px|py)-\d+(?:\.\d+)?$/;
const TEXT_SIZE_TOKEN = /^text-(?:xs|sm|base|lg|xl)$/;

function densityGroup(token: string): "px" | "py" | "text" | null {
  if (PADDING_TOKEN.test(token)) return token.slice(0, 2) as "px" | "py";
  return TEXT_SIZE_TOKEN.test(token) ? "text" : null;
}

type Site = {
  path: string;
  line: number;
  primitive: Primitive;
  tokens: string[];
};

type Row = {
  path: string;
  line: number;
  token: string;
  base: string;
};

type Unmeasured = {
  path: string;
  line: number;
  token: string;
  detail: string;
};

function readQuoted(text: string, from: number, quote: string): string {
  let out = "";
  for (let i = from; i < text.length; i++) {
    const ch = text[i];
    if (ch === "\\" && i + 1 < text.length) {
      out += text[i + 1];
      i++;
      continue;
    }
    if (ch === quote) return out;
    out += ch;
  }
  return out;
}

function skipString(text: string, openQuoteIndex: number): number {
  const quote = text[openQuoteIndex];
  for (let i = openQuoteIndex + 1; i < text.length; i++) {
    if (text[i] === "\\") i++;
    else if (text[i] === quote) return i;
  }
  return text.length;
}

function braceBody(text: string, from: number): string | null {
  let depth = 1;
  for (let i = from; i < text.length; i++) {
    const ch = text[i];
    if (ch === '"' || ch === "'" || ch === "`") i = skipString(text, i);
    else if (ch === "{") depth++;
    else if (ch === "}") {
      depth--;
      if (depth === 0) return text.slice(from, i);
    }
  }
  return null;
}

function openingTagEnd(text: string, attrsFrom: number): number {
  let depth = 0;
  for (let i = attrsFrom; i < text.length; i++) {
    const ch = text[i];
    if (depth > 0) {
      if (ch === '"' || ch === "'" || ch === "`") i = skipString(text, i);
      else if (ch === "{") depth++;
      else if (ch === "}") depth--;
    } else if (ch === "{") depth = 1;
    else if (ch === ">") return i;
    else if (ch === "<") return -1;
  }
  return -1;
}

function classNameValue(tag: string): string {
  const m = /\bclassName\s*=\s*./.exec(tag);
  if (!m) return "";
  const open = tag[m.index + m[0].length - 1];
  const valueFrom = m.index + m[0].length;
  if (open === '"' || open === "'" || open === "`") {
    return readQuoted(tag, valueFrom, open);
  }
  if (open !== "{") return "";
  const body = braceBody(tag, valueFrom);
  if (body === null) return "";
  const parts: string[] = [];
  for (const lit of body.matchAll(/"([^"]*)"|'([^']*)'|`([^`]*)`/g)) {
    parts.push((lit[1] ?? lit[2] ?? lit[3] ?? "").replace(/\$\{[^}]*\}/g, " "));
  }
  return parts.join(" ");
}

function walkSources(dir: string): string[] {
  const found: string[] = [];
  for (const entry of readdirSync(dir, { withFileTypes: true })) {
    const entryPath = join(dir, entry.name);
    if (entry.isDirectory()) found.push(...walkSources(entryPath));
    else if (/\.(ts|tsx)$/.test(entry.name)) found.push(entryPath);
  }
  return found;
}

function collectSites(): Site[] {
  const primitivePaths = new Set(
    Object.values(PRIMITIVE_FILES).map((name) => join(uiDir, name)),
  );
  const files = walkSources(srcDir)
    .filter((file) => !primitivePaths.has(file))
    .sort();
  const sites: Site[] = [];
  for (const file of files) {
    const text = readFileSync(file, "utf8");
    for (const m of text.matchAll(/<(Button|Select|Badge)\b/g)) {
      const tagEnd = openingTagEnd(text, m.index + m[0].length);
      if (tagEnd < 0) continue;
      const tag = text.slice(m.index, tagEnd);
      if (!/\bclassName\s*=/m.test(tag)) continue;
      sites.push({
        path: relative(repoDir, file),
        line: text.slice(0, m.index).split("\n").length,
        primitive: PRIMITIVE_TAG[m[1]],
        tokens: classNameValue(tag).split(/\s+/).filter(Boolean),
      });
    }
  }
  return sites;
}

function firstCnLiteral(source: string, file: string): string {
  const m = /cn\(\s*"([^"]*)"/.exec(source);
  assert.ok(m, `${file}: first cn() call does not start with a string literal`);
  return m[1];
}

function sizeClassLiterals(source: string, file: string): string[] {
  const m = /SIZE_CLASSES\s*=\s*\{([^}]*)\}/.exec(source);
  assert.ok(m, `${file}: SIZE_CLASSES map not found`);
  return [...m[1].matchAll(/"([^"]*)"/g)].map((lit) => lit[1]);
}

function readPrimitiveBases(): Map<Primitive, Map<string, string[]>> {
  const bases = new Map<Primitive, Map<string, string[]>>();
  for (const [name, file] of Object.entries(PRIMITIVE_FILES) as [Primitive, string][]) {
    const source = readFileSync(join(uiDir, file), "utf8");
    const literals =
      name === "select" ? sizeClassLiterals(source, file) : [firstCnLiteral(source, file)];
    const byGroup = new Map<string, string[]>();
    for (const literal of literals) {
      for (const token of literal.split(/\s+/)) {
        const group = densityGroup(token);
        if (!group) continue;
        const bucket = byGroup.get(group) ?? [];
        bucket.push(token);
        byGroup.set(group, bucket);
      }
    }
    assert.notEqual(byGroup.size, 0, `${file}: no density tokens in its base classes`);
    bases.set(name, byGroup);
  }
  return bases;
}

function loadGeneratedCss(): { css: string; chunkCount: number } {
  let chunks: string[] = [];
  try {
    chunks = readdirSync(cssChunkDir)
      .filter((name) => name.endsWith(".css"))
      .sort();
  } catch {
    chunks = [];
  }
  if (chunks.length === 0) {
    throw new Error(
      "run npm run build first: web/out/_next/static/chunks has no *.css to measure against",
    );
  }
  return {
    css: chunks.map((name) => readFileSync(join(cssChunkDir, name), "utf8")).join("\n"),
    chunkCount: chunks.length,
  };
}

function ruleOffset(css: string, token: string): number {
  return css.lastIndexOf(`.${token.replaceAll(".", "\\.")}{`);
}

const byPathThenLine = (a: { path: string; line: number; token: string }, b: typeof a) =>
  a.path.localeCompare(b.path) || a.line - b.line || a.token.localeCompare(b.token);

test("no call-site density token loses to a shared/ui primitive base token", () => {
  const { css, chunkCount } = loadGeneratedCss();
  const bases = readPrimitiveBases();
  const sites = collectSites();

  const dead: Row[] = [];
  const unmeasured: Unmeasured[] = [];
  const live: Row[] = [];

  for (const site of sites) {
    const baseByGroup = bases.get(site.primitive)!;
    for (const token of site.tokens) {
      const group = densityGroup(token);
      if (!group) continue;
      const candidates = baseByGroup.get(group);
      if (!candidates?.length) continue;
      const tokenOffset = ruleOffset(css, token);
      const ranked = candidates
        .map((base) => ({ token: base, offset: ruleOffset(css, base) }))
        .filter((base) => base.offset >= 0)
        .sort((a, b) => b.offset - a.offset || a.token.localeCompare(b.token));
      const row = { path: site.path, line: site.line, token };
      if (tokenOffset < 0 || ranked.length === 0) {
        unmeasured.push({ ...row, detail: "rule missing from generated css" });
      } else if (ranked[0].offset > tokenOffset) {
        dead.push({ ...row, base: ranked[0].token });
      } else {
        live.push({ ...row, base: ranked[0].token });
      }
    }
  }

  dead.sort(byPathThenLine);
  unmeasured.sort(byPathThenLine);
  live.sort(byPathThenLine);

  const deadSites = new Set(dead.map((row) => `${row.path}:${row.line}`)).size;
  const format = (row: Row) => `${row.path}:${row.line}  ${row.token}  (loses to ${row.base})`;
  const liveFormat = (row: Row) => `${row.path}:${row.line}  ${row.token}  (beats ${row.base})`;
  const unmeasuredFormat = (row: Unmeasured) =>
    `${row.path}:${row.line}  ${row.token}  (${row.detail})`;

  const report = [
    "dead-override sweep — current run",
    `css chunks measured: ${chunkCount}`,
    `denominator: ${sites.length} call-site(s) with className · ${bases.size} primitive(s) with base density (${[...bases.keys()].sort().join(", ")}) · ${dead.length} dead token(s) on ${deadSites} site(s)`,
    "",
    `dead — call-site token loses to the primitive base token (${dead.length})`,
    ...dead.map(format),
    "",
    `unmeasured — token or base rule missing from generated css (${unmeasured.length})`,
    ...unmeasured.map(unmeasuredFormat),
    "",
    `live — call-site token wins (${live.length})`,
    ...live.map(liveFormat),
    "",
  ].join("\n");
  writeFileSync(reportPath, report);

  console.log(
    `sites scanned: ${sites.length}; primitives with base density: ${bases.size}; dead tokens: ${dead.length} on ${deadSites} site(s)`,
  );
  for (const row of dead) console.log(format(row));

  assert.equal(
    dead.length,
    0,
    `${dead.length} dead density override(s) across ${deadSites} site(s) — see tests/dead-overrides.current.txt`,
  );
});
