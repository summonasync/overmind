import { Fragment, type ReactNode } from "react";
import { cn } from "@/shared/lib/cn";
import { CeremonySectionBlock, isCeremonySection, type CeremonySection } from "@/shared/lib/markdown-sections";

const INLINE_TOKEN = /(\*\*[^*]+\*\*|\*[^*]+\*|_[^_]+_|`[^`]+`|\[[^\]]+\]\([^)\s]+\))/g;
const HTML_COMMENT = /<!--[\s\S]*?-->/g;
const SECTION_OPEN = /^<([a-z][a-z0-9-]*)>(.*)$/;
const SECTION_SINGLE_LINE = /^<([a-z][a-z0-9-]*)>(.*)<\/\1>$/;
const SECTION_CLOSE = /^<\/([a-z][a-z0-9-]*)>\s*$/;

function renderInline(text: string, keyPrefix: string): ReactNode[] {
  return text.split(INLINE_TOKEN).map((part, index) => {
    const key = `${keyPrefix}-${index}`;
    if (part === "") {
      return null;
    }
    if (part.startsWith("**") && part.endsWith("**") && part.length > 4) {
      return (
        <strong key={key} className="font-semibold">
          {part.slice(2, -2)}
        </strong>
      );
    }
    if (part.startsWith("`") && part.endsWith("`") && part.length > 2) {
      return (
        <code
          key={key}
          className="rounded bg-surface-raised px-1 py-0.5 font-mono text-[0.85em] text-foreground"
        >
          {part.slice(1, -1)}
        </code>
      );
    }
    if (part.startsWith("*") && part.endsWith("*") && part.length > 2) {
      return <em key={key}>{part.slice(1, -1)}</em>;
    }
    if (part.startsWith("_") && part.endsWith("_") && part.length > 2) {
      return <em key={key}>{part.slice(1, -1)}</em>;
    }
    const link = /^\[([^\]]+)\]\(([^)]+)\)$/.exec(part);
    if (link) {
      const [, label, href] = link;
      if (/^https?:\/\//i.test(href)) {
        return (
          <a key={key} href={href} target="_blank" rel="noreferrer" className="text-primary underline underline-offset-2">
            {label}
          </a>
        );
      }
      return <Fragment key={key}>{label}</Fragment>;
    }
    return <Fragment key={key}>{part}</Fragment>;
  });
}

interface OpenSection {
  tag: CeremonySection;
  lines: string[];
}

function renderBlocks(lines: string[], keyPrefix: string): ReactNode[] {
  const blocks: ReactNode[] = [];
  let paragraph: string[] = [];
  let list: string[] = [];
  let index = 0;
  let openSection: OpenSection | null = null;
  let openSectionKey = "";

  const flushParagraph = () => {
    if (paragraph.length > 0) {
      blocks.push(
        <p key={`${keyPrefix}p-${index}`} className="my-1 leading-relaxed">
          {renderInline(paragraph.join(" "), `${keyPrefix}p-${index}`)}
        </p>,
      );
      paragraph = [];
      index += 1;
    }
  };

  const flushList = () => {
    if (list.length > 0) {
      blocks.push(
        <ul key={`${keyPrefix}ul-${index}`} className="my-1 list-disc pl-5">
          {list.map((item, itemIndex) => (
            <li key={itemIndex}>{renderInline(item, `${keyPrefix}li-${index}-${itemIndex}`)}</li>
          ))}
        </ul>,
      );
      list = [];
      index += 1;
    }
  };

  const flushSection = () => {
    if (openSection !== null) {
      blocks.push(
        <CeremonySectionBlock key={openSectionKey} tag={openSection.tag}>
          {renderBlocks(openSection.lines, `${openSectionKey}-`)}
        </CeremonySectionBlock>,
      );
      openSection = null;
      index += 1;
    }
  };

  for (let lineIndex = 0; lineIndex < lines.length; lineIndex += 1) {
    const line = lines[lineIndex];
    const trimmed = line.trim();

    if (openSection !== null) {
      const close = SECTION_CLOSE.exec(trimmed);
      if (close !== null && close[1] === openSection.tag) {
        flushSection();
      } else {
        openSection.lines.push(line);
      }
      continue;
    }

    const singleLine = SECTION_SINGLE_LINE.exec(trimmed);
    if (singleLine !== null && isCeremonySection(singleLine[1])) {
      flushParagraph();
      flushList();
      const body = singleLine[2].trim() === "" ? [] : [singleLine[2]];
      const sectionKey = `${keyPrefix}sec-${index}`;
      blocks.push(
        <CeremonySectionBlock key={sectionKey} tag={singleLine[1]}>
          {renderBlocks(body, `${sectionKey}-`)}
        </CeremonySectionBlock>,
      );
      index += 1;
      continue;
    }

    const open = SECTION_OPEN.exec(trimmed);
    if (open !== null && isCeremonySection(open[1])) {
      flushParagraph();
      flushList();
      openSection = { tag: open[1], lines: open[2].trim() === "" ? [] : [open[2]] };
      openSectionKey = `${keyPrefix}sec-${index}`;
      continue;
    }

    if (line.trim().startsWith("```")) {
      flushParagraph();
      flushList();
      const codeLines: string[] = [];
      lineIndex += 1;
      while (lineIndex < lines.length && !lines[lineIndex].trim().startsWith("```")) {
        codeLines.push(lines[lineIndex]);
        lineIndex += 1;
      }
      blocks.push(
        <pre
          key={`${keyPrefix}code-${index}`}
          className="my-2 overflow-x-auto rounded-md border border-border bg-background p-3 font-mono text-sm text-foreground"
        >
          <code>{codeLines.join("\n")}</code>
        </pre>,
      );
      index += 1;
      continue;
    }

    const heading = /^(#{1,4})\s+(.*)$/.exec(line);
    if (heading) {
      flushParagraph();
      flushList();
      const level = heading[1].length as 1 | 2 | 3 | 4;
      const text = heading[2];
      const headingClasses: Record<1 | 2 | 3 | 4, string> = {
        1: "mt-3 mb-1 text-xl font-bold",
        2: "mt-3 mb-1 text-lg font-semibold",
        3: "mt-2 mb-1 text-base font-semibold",
        4: "mt-2 mb-1 text-sm font-semibold",
      };
      const Tag = `h${level}` as "h1" | "h2" | "h3" | "h4";
      blocks.push(
        <Tag key={`${keyPrefix}h-${index}`} className={headingClasses[level]}>
          {renderInline(text, `${keyPrefix}h-${index}`)}
        </Tag>,
      );
      index += 1;
      continue;
    }

    const listItem = /^\s*[-*]\s+(.*)$/.exec(line);
    if (listItem) {
      flushParagraph();
      list.push(listItem[1]);
      continue;
    }

    if (trimmed === "") {
      flushParagraph();
      flushList();
      continue;
    }

    flushList();
    paragraph.push(line);
  }

  flushParagraph();
  flushList();
  flushSection();

  return blocks;
}

interface MarkdownProps {
  content: string;
  className?: string;
}

export function Markdown({ content, className }: MarkdownProps) {
  const lines = content.replace(HTML_COMMENT, "").split(/\r?\n/);
  return <div className={cn("text-sm text-foreground", className)}>{renderBlocks(lines, "")}</div>;
}
