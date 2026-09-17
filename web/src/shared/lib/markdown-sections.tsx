import type { ReactNode } from "react";
import { cn } from "@/shared/lib/cn";

export const CEREMONY_SECTIONS = [
  "goal",
  "requirements",
  "non-goals",
  "context",
  "scope",
  "anti-scope",
  "constraints",
  "acceptance-criteria",
  "refs",
  "obstacles",
  "log",
  "decisions",
  "open-questions",
  "approach",
  "tasks",
  "risks",
  "verification",
  "findings",
] as const;

export type CeremonySection = (typeof CEREMONY_SECTIONS)[number];

export function isCeremonySection(tag: string): tag is CeremonySection {
  return (CEREMONY_SECTIONS as readonly string[]).includes(tag);
}

interface SectionTone {
  text: string;
  border: string;
  bg: string;
}

const CEREMONY_SECTION_TONES: Record<CeremonySection, SectionTone> = {
  goal: { text: "text-ceremony-goal", border: "border-ceremony-goal", bg: "bg-ceremony-goal/5" },
  requirements: {
    text: "text-ceremony-requirements",
    border: "border-ceremony-requirements",
    bg: "bg-ceremony-requirements/5",
  },
  "non-goals": {
    text: "text-ceremony-non-goals",
    border: "border-ceremony-non-goals",
    bg: "bg-ceremony-non-goals/5",
  },
  context: { text: "text-ceremony-context", border: "border-ceremony-context", bg: "bg-ceremony-context/5" },
  scope: { text: "text-ceremony-scope", border: "border-ceremony-scope", bg: "bg-ceremony-scope/5" },
  "anti-scope": {
    text: "text-ceremony-anti-scope",
    border: "border-ceremony-anti-scope",
    bg: "bg-ceremony-anti-scope/5",
  },
  constraints: {
    text: "text-ceremony-constraints",
    border: "border-ceremony-constraints",
    bg: "bg-ceremony-constraints/5",
  },
  "acceptance-criteria": {
    text: "text-ceremony-acceptance-criteria",
    border: "border-ceremony-acceptance-criteria",
    bg: "bg-ceremony-acceptance-criteria/5",
  },
  refs: { text: "text-ceremony-refs", border: "border-ceremony-refs", bg: "bg-ceremony-refs/5" },
  obstacles: {
    text: "text-ceremony-obstacles",
    border: "border-ceremony-obstacles",
    bg: "bg-ceremony-obstacles/5",
  },
  log: { text: "text-ceremony-log", border: "border-ceremony-log", bg: "bg-ceremony-log/5" },
  decisions: {
    text: "text-ceremony-decisions",
    border: "border-ceremony-decisions",
    bg: "bg-ceremony-decisions/5",
  },
  "open-questions": {
    text: "text-ceremony-open-questions",
    border: "border-ceremony-open-questions",
    bg: "bg-ceremony-open-questions/5",
  },
  approach: {
    text: "text-ceremony-approach",
    border: "border-ceremony-approach",
    bg: "bg-ceremony-approach/5",
  },
  tasks: { text: "text-ceremony-tasks", border: "border-ceremony-tasks", bg: "bg-ceremony-tasks/5" },
  risks: { text: "text-ceremony-risks", border: "border-ceremony-risks", bg: "bg-ceremony-risks/5" },
  verification: {
    text: "text-ceremony-verification",
    border: "border-ceremony-verification",
    bg: "bg-ceremony-verification/5",
  },
  findings: {
    text: "text-ceremony-findings",
    border: "border-ceremony-findings",
    bg: "bg-ceremony-findings/5",
  },
};

interface CeremonySectionBlockProps {
  tag: CeremonySection;
  children: ReactNode;
}

export function CeremonySectionBlock({ tag, children }: CeremonySectionBlockProps) {
  const tone = CEREMONY_SECTION_TONES[tag];
  return (
    <section
      aria-label={`${tag} section`}
      className={cn("my-2 rounded-r-md border-l-2 py-1.5 pl-3 pr-2", tone.border, tone.bg)}
    >
      <p className={cn("font-mono text-xs font-semibold", tone.text)}>{`<${tag}>`}</p>
      <div className={cn("mt-0.5", tone.text)}>{children}</div>
      <p className={cn("mt-1 font-mono text-xs font-semibold", tone.text)}>{`</${tag}>`}</p>
    </section>
  );
}
