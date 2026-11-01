"use client";

import { ChevronDown } from "lucide-react";
import type { LucideIcon } from "lucide-react";
import type { ReactNode } from "react";
import { cn } from "@/shared/lib/cn";
import { useSectionPrefs } from "@/widgets/app-shell/lib/use-section-prefs";

interface InfoPanelCollapsibleSectionProps {
  page: string;
  section: string;
  title: string;
  icon?: LucideIcon;
  children: ReactNode;
}

export function InfoPanelCollapsibleSection({
  page,
  section,
  title,
  icon: Icon,
  children,
}: InfoPanelCollapsibleSectionProps) {
  const { isCollapsed, toggle } = useSectionPrefs(page);
  const collapsed = isCollapsed(section);

  return (
    <section>
      <h2>
        <button
          type="button"
          onClick={() => toggle(section)}
          aria-expanded={!collapsed}
          className="flex w-full items-center gap-2 rounded text-sm font-semibold text-foreground transition-colors hover:text-primary focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary/40"
        >
          {Icon ? <Icon size={15} aria-hidden className="shrink-0 text-primary" /> : null}
          <span className="flex-1 text-left">{title}</span>
          <ChevronDown
            size={14}
            aria-hidden
            className={cn("shrink-0 text-muted transition-transform", collapsed && "rotate-180")}
          />
        </button>
      </h2>
      {collapsed ? null : <div className="pt-3">{children}</div>}
    </section>
  );
}
