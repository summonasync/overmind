import { Fragment, type ReactNode } from "react";
import { cn } from "@/shared/lib/cn";

const INLINE_TOKEN = /(\*\*[^*]+\*\*|\*[^*]+\*|_[^_]+_|`[^`]+`|\[[^\]]+\]\([^)\s]+\))/g;

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

interface MarkdownProps {
  content: string;
  className?: string;
}

export function Markdown({ content, className }: MarkdownProps) {
  const lines = content.split(/\r?\n/);
  const blocks: ReactNode[] = [];
  let paragraph: string[] = [];
  let list: string[] = [];
  let index = 0;

  const flushParagraph = () => {
    if (paragraph.length > 0) {
      blocks.push(
        <p key={`p-${index}`} className="my-1 leading-relaxed">
          {renderInline(paragraph.join(" "), `p-${index}`)}
        </p>,
      );
      paragraph = [];
      index += 1;
    }
  };

  const flushList = () => {
    if (list.length > 0) {
      blocks.push(
        <ul key={`ul-${index}`} className="my-1 list-disc pl-5">
          {list.map((item, itemIndex) => (
            <li key={itemIndex}>{renderInline(item, `li-${index}-${itemIndex}`)}</li>
          ))}
        </ul>,
      );
      list = [];
      index += 1;
    }
  };

  for (let lineIndex = 0; lineIndex < lines.length; lineIndex += 1) {
    const line = lines[lineIndex];

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
          key={`code-${index}`}
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
        <Tag key={`h-${index}`} className={headingClasses[level]}>
          {renderInline(text, `h-${index}`)}
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

    if (line.trim() === "") {
      flushParagraph();
      flushList();
      continue;
    }

    flushList();
    paragraph.push(line);
  }

  flushParagraph();
  flushList();

  return <div className={cn("text-sm text-foreground", className)}>{blocks}</div>;
}
