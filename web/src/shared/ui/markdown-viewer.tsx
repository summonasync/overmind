import { Fragment } from "react";
import ReactMarkdown from "react-markdown";
import remarkGfm from "remark-gfm";
import type { Components } from "react-markdown";
import { cn } from "@/shared/lib/cn";
import {
  CeremonySectionBlock,
  isCeremonySection,
  type CeremonySection,
} from "@/shared/lib/markdown-sections";

const SECTION_OPEN = /^<([a-z][a-z0-9-]*)>(.*)$/;
const SECTION_SINGLE_LINE = /^<([a-z][a-z0-9-]*)>(.*)<\/\1>$/;
const SECTION_CLOSE = /^<\/([a-z][a-z0-9-]*)>\s*$/;

interface MarkdownChunk {
  section: CeremonySection | null;
  content: string;
}

function splitCeremonySections(content: string): MarkdownChunk[] {
  const chunks: MarkdownChunk[] = [];
  let plain: string[] = [];
  let open: CeremonySection | null = null;
  let sectionLines: string[] = [];

  const flushPlain = () => {
    if (plain.length > 0) {
      chunks.push({ section: null, content: plain.join("\n") });
      plain = [];
    }
  };

  const flushSection = () => {
    if (open !== null) {
      chunks.push({ section: open, content: sectionLines.join("\n") });
      open = null;
      sectionLines = [];
    }
  };

  for (const line of content.split(/\r?\n/)) {
    const trimmed = line.trim();

    if (open !== null) {
      const close = SECTION_CLOSE.exec(trimmed);
      if (close !== null && close[1] === open) {
        flushSection();
      } else {
        sectionLines.push(line);
      }
      continue;
    }

    const singleLine = SECTION_SINGLE_LINE.exec(trimmed);
    if (singleLine !== null && isCeremonySection(singleLine[1])) {
      flushPlain();
      chunks.push({ section: singleLine[1], content: singleLine[2] });
      continue;
    }

    const opening = SECTION_OPEN.exec(trimmed);
    if (opening !== null && isCeremonySection(opening[1])) {
      flushPlain();
      open = opening[1];
      sectionLines = opening[2].trim() === "" ? [] : [opening[2]];
      continue;
    }

    plain.push(line);
  }

  flushPlain();
  flushSection();
  return chunks;
}

const MARKDOWN_COMPONENTS: Components = {
  h1: ({ children }) => <h1 className="mt-3 mb-1 text-xl font-bold">{children}</h1>,
  h2: ({ children }) => <h2 className="mt-3 mb-1 text-lg font-semibold">{children}</h2>,
  h3: ({ children }) => <h3 className="mt-2 mb-1 text-base font-semibold">{children}</h3>,
  h4: ({ children }) => <h4 className="mt-2 mb-1 text-sm font-semibold">{children}</h4>,
  p: ({ children }) => <p className="my-1 leading-relaxed">{children}</p>,
  ul: ({ children }) => <ul className="my-1 list-disc pl-5">{children}</ul>,
  ol: ({ children }) => <ol className="my-1 list-decimal pl-5">{children}</ol>,
  strong: ({ children }) => <strong className="font-semibold">{children}</strong>,
  a: ({ href, children }) => {
    if (href !== undefined && /^https?:\/\//i.test(href)) {
      return (
        <a href={href} target="_blank" rel="noreferrer" className="text-primary underline underline-offset-2">
          {children}
        </a>
      );
    }
    return (
      <a href={href} className="text-primary underline underline-offset-2">
        {children}
      </a>
    );
  },
  blockquote: ({ children }) => (
    <blockquote className="my-2 border-l-2 border-border pl-3 text-muted">{children}</blockquote>
  ),
  pre: ({ children }) => (
    <pre className="my-2 overflow-x-auto rounded-md border border-border bg-background p-3 font-mono text-sm text-foreground [&_code]:rounded-none [&_code]:bg-transparent [&_code]:px-0 [&_code]:py-0 [&_code]:text-sm">
      {children}
    </pre>
  ),
  code: ({ children }) => (
    <code className="rounded bg-surface-raised px-1 py-0.5 font-mono text-[0.85em] text-foreground">
      {children}
    </code>
  ),
  table: ({ children }) => (
    <div className="my-2 overflow-x-auto">
      <table className="w-full border-collapse text-sm">{children}</table>
    </div>
  ),
  th: ({ children }) => <th className="border border-border px-2 py-1 text-left font-semibold">{children}</th>,
  td: ({ children }) => <td className="border border-border px-2 py-1 align-top">{children}</td>,
  hr: () => <hr className="my-3 border-border" />,
  img: () => null,
};

interface MarkdownViewerProps {
  content: string;
  className?: string;
}

export function MarkdownViewer({ content, className }: MarkdownViewerProps) {
  return (
    <div className={cn("text-sm text-foreground", className)}>
      {splitCeremonySections(content).map((chunk, index) => {
        const body = (
          <ReactMarkdown remarkPlugins={[remarkGfm]} components={MARKDOWN_COMPONENTS}>
            {chunk.content}
          </ReactMarkdown>
        );
        return chunk.section === null ? (
          <Fragment key={index}>{body}</Fragment>
        ) : (
          <CeremonySectionBlock key={index} tag={chunk.section}>
            {body}
          </CeremonySectionBlock>
        );
      })}
    </div>
  );
}
