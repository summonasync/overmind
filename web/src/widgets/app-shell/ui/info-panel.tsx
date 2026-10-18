"use client";

import { PanelRightClose, PanelRightOpen } from "lucide-react";
import { useInfoPanelContent } from "@/widgets/app-shell/lib/info-panel-context";
import { InfoPanelFooter } from "@/widgets/app-shell/ui/info-panel-footer";
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
    <aside className="sticky top-14 hidden h-[calc(100vh-3.5rem)] w-[300px] shrink-0 flex-col overflow-y-auto border-l border-border/60 bg-surface/50 p-4 xl:flex">
      <div className="mb-3 flex items-center justify-end">
        <Button variant="ghost" onClick={onToggle} aria-label="Collapse info panel" className="px-1.5 py-1.5">
          <PanelRightClose size={16} aria-hidden />
        </Button>
      </div>
      {content}
      <InfoPanelFooter />
    </aside>
  );
}
