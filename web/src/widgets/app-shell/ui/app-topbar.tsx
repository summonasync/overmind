"use client";

import { useEffect } from "react";
import { usePathname, useRouter } from "next/navigation";
import { BellRing, Folder, Search, Settings, type LucideIcon } from "lucide-react";
import logoMark from "@/shared/assets/brand/logo-mark.png";
import kerriganAvatar from "@/shared/assets/brand/kerrigan-avatar.png";
import { cn } from "@/shared/lib/cn";
import { NotificationBell } from "@/widgets/app-shell/ui/notification-bell";
import { StatusChip } from "@/widgets/app-shell/ui/status-chip";

interface NavItem {
  href: string;
  label: string;
  icon: LucideIcon;
}

const NAV_ITEMS: NavItem[] = [
  { href: "/", label: "Projects", icon: Folder },
  { href: "/attention", label: "Attention", icon: BellRing },
  { href: "/search", label: "Search", icon: Search },
  { href: "/settings", label: "Settings", icon: Settings },
];

export function AppTopbar() {
  const router = useRouter();
  const pathname = usePathname() ?? "/";

  useEffect(() => {
    const handleKeyDown = (event: KeyboardEvent) => {
      if ((event.metaKey || event.ctrlKey) && event.key.toLowerCase() === "k") {
        event.preventDefault();
        router.push("/search");
      }
    };
    window.addEventListener("keydown", handleKeyDown);
    return () => window.removeEventListener("keydown", handleKeyDown);
  }, [router]);

  return (
    <header className="sticky top-0 z-20 flex h-14 items-center gap-6 border-b border-border/60 bg-background/80 px-4 backdrop-blur">
      <div className="flex min-w-0 items-center gap-5">
        <a
          href="/"
          aria-label="Overmind home"
          className="flex shrink-0 items-center gap-2.5 rounded-md focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary/40"
        >
          <img src={logoMark.src} alt="" className="h-7 w-auto" />
          <span className="text-sm font-bold tracking-[0.3em] text-foreground">OVERMIND</span>
        </a>
        <nav className="hidden items-center gap-1 lg:flex" aria-label="Main navigation">
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
      </div>
      <div className="ml-auto flex shrink-0 items-center gap-3">
        <StatusChip />
        <NotificationBell />
        <div className="flex items-center gap-2 rounded-full border border-border bg-surface py-1 pl-1 pr-3">
          <img src={kerriganAvatar.src} alt="Kerrigan avatar" className="h-7 w-7 rounded-full border border-primary/40 object-cover" />
          <div className="leading-tight">
            <p className="text-xs font-medium text-foreground">Humanity Evolves</p>
            <p className="text-[10px] text-muted">Overmind</p>
          </div>
        </div>
      </div>
    </header>
  );
}
