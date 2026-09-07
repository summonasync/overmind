"use client";

import { useEffect, useState } from "react";
import { usePathname } from "next/navigation";
import { FileText, Folder, Package, Search, Settings, type LucideIcon } from "lucide-react";
import logoMark from "@/shared/assets/brand/logo-mark.png";
import kerrigan1 from "@/shared/assets/brand/kerrigan/kerrigan-1.webp";
import kerrigan2 from "@/shared/assets/brand/kerrigan/kerrigan-2.webp";
import kerrigan3 from "@/shared/assets/brand/kerrigan/kerrigan-3.webp";
import kerrigan4 from "@/shared/assets/brand/kerrigan/kerrigan-4.webp";
import kerrigan5 from "@/shared/assets/brand/kerrigan/kerrigan-5.webp";
import kerrigan6 from "@/shared/assets/brand/kerrigan/kerrigan-6.webp";
import kerrigan7 from "@/shared/assets/brand/kerrigan/kerrigan-7.webp";
import { cn } from "@/shared/lib/cn";
import { SidebarQuote } from "@/widgets/app-shell/ui/sidebar-quote";

const KERRIGAN_VARIANTS = [kerrigan1, kerrigan2, kerrigan3, kerrigan4, kerrigan5, kerrigan6, kerrigan7];

interface NavItem {
  href: string;
  label: string;
  icon: LucideIcon;
}

const NAV_ITEMS: NavItem[] = [
  { href: "/", label: "Projects", icon: Folder },
  { href: "/entries", label: "Entries", icon: FileText },
  { href: "/kits", label: "Kits", icon: Package },
  { href: "/search", label: "Search", icon: Search },
  { href: "/settings", label: "Settings", icon: Settings },
];

export function AppSidebar() {
  const pathname = usePathname() ?? "/";
  const [kerrigan, setKerrigan] = useState<string | null>(null);

  useEffect(() => {
    const frame = requestAnimationFrame(() => {
      setKerrigan(KERRIGAN_VARIANTS[Math.floor(Math.random() * KERRIGAN_VARIANTS.length)].src);
    });
    return () => cancelAnimationFrame(frame);
  }, []);

  return (
    <aside className="fixed inset-y-0 left-0 z-30 flex w-60 flex-col border-r border-border/60 max-lg:hidden">
      {kerrigan !== null && (
        <img
          src={kerrigan}
          alt=""
          aria-hidden
          className="pointer-events-none absolute bottom-0 left-0 h-[68%] w-44 object-cover object-top opacity-80 [mask-image:linear-gradient(to_right,black_30%,transparent_98%)]"
        />
      )}
      <div className="relative flex items-center gap-2.5 px-4 pt-5">
        <img src={logoMark.src} alt="Overmind logo" className="h-8 w-auto" />
        <div>
          <p className="text-sm font-bold tracking-[0.3em] text-foreground">OVERMIND</p>
          <p className="text-[10px] uppercase tracking-widest text-muted">One context. Many minds.</p>
        </div>
      </div>
      <nav className="relative mt-8 flex flex-col gap-1 px-3" aria-label="Main navigation">
        {NAV_ITEMS.map((item) => {
          const active = item.href === "/" ? pathname === "/" : pathname.startsWith(item.href);
          const Icon = item.icon;
          return (
            <a
              key={item.href}
              href={item.href}
              aria-current={active ? "page" : undefined}
              className={cn(
                "flex items-center gap-2.5 rounded-md border-l-2 px-3 py-2 text-sm font-medium transition-colors",
                "focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary/40",
                active
                  ? "border-primary bg-primary/10 text-primary"
                  : "border-transparent text-muted hover:bg-surface-raised hover:text-foreground",
              )}
            >
              <Icon size={16} aria-hidden />
              {item.label}
            </a>
          );
        })}
      </nav>
      <div className="relative mt-auto px-4 pb-5">
        <SidebarQuote />
      </div>
    </aside>
  );
}
