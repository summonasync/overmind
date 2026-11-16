#!/usr/bin/env python3
"""Cross-check counter: sum [Description("...")] string literals per tool in the adapter source.

Walks src/Overmind.Mcp/**.cs, attributes each [Description] literal to the enclosing
[McpServerTool] method (the first one before `public static` is the tool description,
the rest are parameter descriptions), decodes C# escape sequences and counts code points.

This is the method the spec figure (78,984) came from. It is NOT ground truth:
measure_wire.py is. Deltas are findings — e.g. literals the SDK lifts into nested
inputSchema descriptions, or interpolations this parser cannot see.

Usage: measure_literals.py --src src/Overmind.Mcp [--out JSON] [--md MD]
"""

import argparse
import json
import pathlib
import re
import sys

TOOL_RE = re.compile(r"\[McpServerTool\(")
NAME_RE = re.compile(r'Name\s*=\s*"((?:[^"\\]|\\.)*)"')
DESC_RE = re.compile(r'\[Description\(\s*"')
METHOD_RE = re.compile(r"\bpublic\s+(?:static\s+)?(?:async\s+)?[\w<>?,\.\[\] ]+? (\w+)\s*\(")

ESCAPES = {"0": "\0", "a": "\a", "b": "\b", "f": "\f", "n": "\n", "r": "\r",
           "t": "\t", "v": "\v", "\\": "\\", '"': '"', "'": "'"}

SKIPPED = []
GLOBAL_CONSTS = {}


def decode_literal(text):
    out = []
    i = 0
    while i < len(text):
        ch = text[i]
        if ch != "\\":
            out.append(ch)
            i += 1
            continue
        i += 1
        if i >= len(text):
            break
        esc = text[i]
        if esc in ESCAPES:
            out.append(ESCAPES[esc])
            i += 1
        elif esc == "u":
            out.append(chr(int(text[i + 1:i + 5], 16)))
            i += 5
        elif esc == "x":
            digits = ""
            i += 1
            while i < len(text) and len(digits) < 4 and text[i] in "0123456789abcdefABCDEF":
                digits += text[i]
                i += 1
            out.append(chr(int(digits, 16)))
        else:
            out.append(esc)
            i += 1
    return "".join(out)


def read_string_literal(text, start):
    """start points at the opening quote; returns (decoded, index after closing quote)."""
    i = start + 1
    buf = []
    while i < len(text):
        ch = text[i]
        if ch == "\\":
            buf.append(ch)
            if i + 1 < len(text):
                buf.append(text[i + 1])
            i += 2
            continue
        if ch == '"':
            return decode_literal("".join(buf)), i + 1
        buf.append(ch)
        i += 1
    raise ValueError("unterminated string literal")


def collect_string_chain(text, pos, consts=None):
    """Sum lengths of a mixed `"a" + CONST + "b"` concatenation starting at pos
    (first non-space). consts maps identifier names to char counts (local first,
    then GLOBAL_CONSTS). Returns (total_or_None, pos); None means an unresolvable
    segment was hit."""
    local = consts or {}
    total = 0
    while True:
        while pos < len(text) and text[pos] in " \t\r\n":
            pos += 1
        if pos >= len(text):
            return total, pos
        ch = text[pos]
        if ch == '"':
            decoded, pos = read_string_literal(text, pos)
            total += len(decoded)
        elif ch.isalpha() or ch == "_":
            m = re.match(r"[\w.]+", text[pos:])
            name = m.group(0)
            head = name.split(".")[-1]
            chars = local.get(name, local.get(head, GLOBAL_CONSTS.get(head)))
            if chars is None:
                return None, pos
            total += chars
            pos += m.end()
        else:
            return total, pos
        nxt = pos
        while nxt < len(text) and text[nxt] in " \t\r\n":
            nxt += 1
        if nxt < len(text) and text[nxt] == "+":
            pos = nxt + 1
            continue
        return total, pos


CONST_RE = re.compile(r"const string (\w+)\s*=")


def parse_consts(text):
    consts = {}
    pending = [(m.group(1), m.end()) for m in CONST_RE.finditer(text)]
    for _ in range(4):
        remaining = []
        for name, end in pending:
            chars, _ = collect_string_chain(text, end, consts)
            if chars is None:
                remaining.append((name, end))
            else:
                consts[name] = chars
        if not remaining or remaining == pending:
            pending = remaining
            break
        pending = remaining
    return consts


def collect_literals(text, pos, consts=None):
    return collect_string_chain(text, pos, consts)


def parse_file(path):
    text = path.read_text(encoding="utf-8")
    consts = parse_consts(text)
    GLOBAL_CONSTS.update(consts)
    tools = []
    current = None
    seen_method_line = False
    for m in re.finditer(r"\[McpServerTool\(|\[Description\(\s*|\n\s{4}public |\Z", text):
        tok = m.group(0)
        start = m.start()
        if tok.startswith("[McpServerTool("):
            if current is not None:
                tools.append(current)
            end = text.index(")]", start)
            name_m = NAME_RE.search(text[start:end])
            header_m = METHOD_RE.search(text[end:end + 4000])
            current = {"tool": name_m.group(1) if name_m else
                       (header_m.group(1) if header_m else f"unnamed@{path.name}:{text[:start].count(chr(10)) + 1}"),
                       "tool_desc_chars": 0, "param_desc_chars": 0,
                       "file": str(path), "line": text[:start].count("\n") + 1}
            seen_method_line = False
        elif tok.startswith("[Description("):
            arg_start = start + len(tok)
            chars, after = collect_literals(text, arg_start, consts)
            if chars is None:
                probe = arg_start
                while probe < len(text) and text[probe] in " \t\r\n":
                    probe += 1
                SKIPPED.append((str(path), text[:start].count("\n") + 1, text[probe:probe + 20]))
                continue
            if current is not None:
                if not seen_method_line:
                    current["tool_desc_chars"] += chars
                else:
                    current["param_desc_chars"] += chars
        elif tok.startswith("public ") or tok.lstrip().startswith("public "):
            seen_method_line = True
    if current is not None:
        tools.append(current)
    return tools


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--src", required=True)
    ap.add_argument("--out")
    ap.add_argument("--md")
    args = ap.parse_args()

    root = pathlib.Path(args.src)
    tools = []
    cs_files = [p for p in sorted(root.rglob("*.cs"))]
    GLOBAL_CONSTS.clear()
    for path in cs_files:
        text = path.read_text(encoding="utf-8")
        if "const string" in text:
            GLOBAL_CONSTS.update(parse_consts(text))
    for path in cs_files:
        if "[McpServerTool(" not in path.read_text(encoding="utf-8"):
            continue
        tools.extend(parse_file(path))

    tools.sort(key=lambda t: -(t["tool_desc_chars"] + t["param_desc_chars"]))
    grand_tool = sum(t["tool_desc_chars"] for t in tools)
    grand_param = sum(t["param_desc_chars"] for t in tools)
    grand = grand_tool + grand_param

    print(f"tools={len(tools)} tool_desc_chars={grand_tool} param_desc_chars={grand_param} total={grand}")
    for path, line, sample in SKIPPED:
        print(f"SKIPPED_NON_LITERAL_DESCRIPTION\t{path}:{line}\t{sample!r}", file=sys.stderr)
    if SKIPPED:
        print(f"skipped_non_literal_descriptions={len(SKIPPED)}", file=sys.stderr)
    for t in tools:
        print(f"{t['tool']}\t{t['tool_desc_chars']}\t{t['param_desc_chars']}\t{t['tool_desc_chars'] + t['param_desc_chars']}")

    if args.out:
        pathlib.Path(args.out).write_text(json.dumps(
            {"method": "literal parse of [Description] in adapter source", "tool_count": len(tools),
             "tool_desc_chars": grand_tool, "param_desc_chars": grand_param,
             "total_chars": grand, "rows": tools}, indent=1), encoding="utf-8")
    if args.md:
        lines = ["| # | Tool | Desc chars | Param chars | Total chars |",
                 "|---|------|-----------:|------------:|------------:|"]
        for i, t in enumerate(tools, 1):
            lines.append(f"| {i} | {t['tool']} | {t['tool_desc_chars']:,} | {t['param_desc_chars']:,} | {t['tool_desc_chars'] + t['param_desc_chars']:,} |")
        lines.append(f"| — | **TOTAL ({len(tools)} tools)** | **{grand_tool:,}** | **{grand_param:,}** | **{grand:,}** |")
        pathlib.Path(args.md).write_text("\n".join(lines) + "\n", encoding="utf-8")


if __name__ == "__main__":
    main()
