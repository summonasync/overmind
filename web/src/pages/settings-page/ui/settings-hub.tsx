import type { ReactNode } from "react";
import { SettingsSubNav } from "@/pages/settings-page/ui/settings-sub-nav";

interface SettingsHubProps {
  children: ReactNode;
}

export function SettingsHub({ children }: SettingsHubProps) {
  return (
    <div className="flex flex-col gap-4 lg:flex-row lg:gap-8">
      <SettingsSubNav />
      <div className="min-w-0 flex-1">{children}</div>
    </div>
  );
}
