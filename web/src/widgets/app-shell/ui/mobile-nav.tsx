"use client";

import { usePathname } from "next/navigation";
import { FileText, Folder, ListChecks, Package, Search, Settings, type LucideIcon } from "lucide-react";
import { cn } from "@/shared/lib/cn";

interface NavItem {
  href: string;
  label: string;
  icon: LucideIcon;
}

const NAV_ITEMS: NavItem[] = [
  { href: "/", label: "Projects", icon: Folder },
  { href: "/entries", label: "Entries", icon: FileText },
  { href: "/planning", label: "Planning", icon: ListChecks },
  { href: "/kits", label: "Kits", icon: Package },
  { href: "/search", label: "Search", icon: Search },
  { href: "/settings", label: "Settings", icon: Settings },
];

export function MobileNav() {
  const pathname = usePathname() ?? "/";

  return (
    <nav className="flex items-center gap-1 overflow-x-auto border-b border-border/60 bg-background/80 px-2 py-1.5 backdrop-blur lg:hidden" aria-label="Main navigation">
      {NAV_ITEMS.map((item) => {
        const active = item.href === "/" ? pathname === "/" : pathname.startsWith(item.href);
        const Icon = item.icon;
        return (
          <a
            key={item.href}
            href={item.href}
            aria-current={active ? "page" : undefined}
            className={cn(
              "flex items-center gap-1.5 rounded-md px-3 py-1.5 text-sm font-medium whitespace-nowrap",
              "focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary/40",
              active ? "bg-primary/10 text-primary" : "text-muted hover:bg-surface-raised hover:text-foreground",
            )}
          >
            <Icon size={15} aria-hidden />
            {item.label}
          </a>
        );
      })}
    </nav>
  );
}
