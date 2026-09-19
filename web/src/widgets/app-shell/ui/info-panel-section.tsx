import type { ReactNode } from "react";
import type { LucideIcon } from "lucide-react";

interface InfoPanelSectionProps {
  title: string;
  icon?: LucideIcon;
  children: ReactNode;
}

export function InfoPanelSection({ title, icon: Icon, children }: InfoPanelSectionProps) {
  return (
    <section>
      <h2 className="mb-3 flex items-center gap-2 text-sm font-semibold text-foreground">
        {Icon ? <Icon size={15} aria-hidden className="text-primary" /> : null}
        {title}
      </h2>
      {children}
    </section>
  );
}
