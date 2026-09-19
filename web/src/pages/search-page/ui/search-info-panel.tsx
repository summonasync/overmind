import { ArrowRight, Lightbulb } from "lucide-react";
import { InfoPanelSection } from "@/widgets/app-shell";

const TIPS = [
  "Use specific keywords (e.g. \"agent memory\").",
  "Filter by project or entry type to narrow results.",
  "Full-text search matches titles, tags, and content.",
  "Semantic search understands meaning, not just exact words.",
] as const;

export function SearchInfoPanel() {
  return (
    <div className="space-y-6">
      <InfoPanelSection title="Search Tips" icon={Lightbulb}>
        <ul className="space-y-1.5">
          {TIPS.map((tip) => (
            <li key={tip} className="flex items-start gap-1.5 text-xs leading-relaxed text-muted">
              <ArrowRight size={12} aria-hidden className="mt-0.5 shrink-0 text-primary" />
              {tip}
            </li>
          ))}
        </ul>
      </InfoPanelSection>
    </div>
  );
}
