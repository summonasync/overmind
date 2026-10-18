"use client";

import { PanelRightClose, PanelRightOpen } from "lucide-react";
import { useInfoPanelContent } from "@/widgets/app-shell/lib/info-panel-context";
import { InfoPanelArt } from "@/widgets/app-shell/ui/info-panel-art";
import { SidebarQuote } from "@/widgets/app-shell/ui/sidebar-quote";
import { Button } from "@/shared/ui/button";

interface InfoPanelProps {
  collapsed: boolean;
  onToggle: () => void;
}

export function InfoPanel({ collapsed, onToggle }: InfoPanelProps) {
  const content = useInfoPanelContent();

  if (!content) {
    return null;
  }

  if (collapsed) {
    return (
      <div className="sticky top-14 hidden h-[calc(100vh-3.5rem)] w-10 shrink-0 items-start justify-center border-l border-border/60 pt-3 xl:flex">
        <Button variant="ghost" onClick={onToggle} aria-label="Expand info panel" className="px-1.5 py-1.5">
          <PanelRightOpen size={16} aria-hidden />
        </Button>
      </div>
    );
  }

  return (
    <aside className="sticky top-14 hidden h-[calc(100vh-3.5rem)] w-[300px] shrink-0 flex-col overflow-hidden border-l border-border/60 bg-surface/50 p-4 xl:flex">
      <InfoPanelArt />
      <div className="relative z-10 flex min-h-0 flex-1 flex-col">
        <div className="mb-3 flex items-center justify-end">
          <Button variant="ghost" onClick={onToggle} aria-label="Collapse info panel" className="px-1.5 py-1.5">
            <PanelRightClose size={16} aria-hidden />
          </Button>
        </div>
        <div className="min-h-0 flex-1 overflow-y-auto">{content}</div>
        <div className="mt-3">
          <SidebarQuote />
        </div>
      </div>
    </aside>
  );
}
