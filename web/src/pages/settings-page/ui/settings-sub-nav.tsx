"use client";

import { usePathname } from "next/navigation";
import { FolderCog, Package, SlidersHorizontal, type LucideIcon } from "lucide-react";
import { cn } from "@/shared/lib/cn";

interface SettingsSection {
  href: string;
  label: string;
  icon: LucideIcon;
  exact: boolean;
}

const SECTIONS: SettingsSection[] = [
  { href: "/settings", label: "Configuration", icon: SlidersHorizontal, exact: true },
  { href: "/settings/kits", label: "Kits", icon: Package, exact: false },
  { href: "/settings/projects", label: "Projects settings", icon: FolderCog, exact: false },
];

export function SettingsSubNav() {
  const pathname = usePathname() ?? "/settings";

  return (
    <nav
      aria-label="Settings sections"
      className="flex shrink-0 gap-1 overflow-x-auto lg:sticky lg:top-20 lg:w-56 lg:flex-col lg:gap-0.5 lg:self-start lg:overflow-visible"
    >
      <span className="hidden px-2 pb-1 text-[11px] font-semibold uppercase tracking-wide text-muted lg:block">
        Settings
      </span>
      {SECTIONS.map((section) => {
        const active = section.exact ? pathname === section.href : pathname.startsWith(section.href);
        const Icon = section.icon;
        return (
          <a
            key={section.href}
            href={section.href}
            aria-current={active ? "page" : undefined}
            className={cn(
              "flex shrink-0 items-center gap-2 rounded-md border-l-2 px-2 py-2 text-sm font-medium transition-colors",
              "focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary/40",
              active
                ? "border-primary bg-primary/10 text-primary"
                : "border-transparent text-muted hover:bg-surface-raised hover:text-foreground",
            )}
          >
            <Icon size={16} aria-hidden />
            {section.label}
          </a>
        );
      })}
    </nav>
  );
}
