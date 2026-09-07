"use client";

import { ArrowRight, Lightbulb } from "lucide-react";

const TIPS = [
  "Use specific keywords (e.g. \"agent memory\").",
  "Filter by project or entry type to narrow results.",
  "Full-text search matches titles, tags, and content.",
  "Semantic search understands meaning, not just exact words.",
] as const;

export function SearchInfoPanel() {
  return (
    <div className="space-y-6">
      <section>
        <h2 className="mb-3 flex items-center gap-2 text-sm font-semibold text-foreground">
          <Lightbulb size={15} aria-hidden className="text-primary" />
          Search Tips
        </h2>
        <ul className="space-y-1.5">
          {TIPS.map((tip) => (
            <li key={tip} className="flex items-start gap-1.5 text-xs leading-relaxed text-muted">
              <ArrowRight size={12} aria-hidden className="mt-0.5 shrink-0 text-primary" />
              {tip}
            </li>
          ))}
        </ul>
      </section>
    </div>
  );
}
