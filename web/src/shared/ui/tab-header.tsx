import type { ReactNode } from "react";

interface TabHeaderProps {
  description: string;
  actions?: ReactNode;
}

export function TabHeader({ description, actions }: TabHeaderProps) {
  return (
    <div className="mb-6 flex flex-wrap items-start justify-between gap-4">
      <p className="text-sm text-muted">{description}</p>
      {actions ? <div className="flex items-center gap-2">{actions}</div> : null}
    </div>
  );
}
