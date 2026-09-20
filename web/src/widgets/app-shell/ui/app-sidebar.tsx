"use client";

import { useEffect, useState } from "react";
import { usePathname } from "next/navigation";
import {
  ChevronDown,
  ChevronRight,
  Folder,
  Package,
  Search,
  Settings,
  type LucideIcon,
} from "lucide-react";
import logoMark from "@/shared/assets/brand/logo-mark.png";
import kerrigan1 from "@/shared/assets/brand/kerrigan/kerrigan-1.webp";
import kerrigan2 from "@/shared/assets/brand/kerrigan/kerrigan-2.webp";
import kerrigan3 from "@/shared/assets/brand/kerrigan/kerrigan-3.webp";
import kerrigan4 from "@/shared/assets/brand/kerrigan/kerrigan-4.webp";
import kerrigan5 from "@/shared/assets/brand/kerrigan/kerrigan-5.webp";
import kerrigan6 from "@/shared/assets/brand/kerrigan/kerrigan-6.webp";
import kerrigan7 from "@/shared/assets/brand/kerrigan/kerrigan-7.webp";
import { cn } from "@/shared/lib/cn";
import { Modal } from "@/shared/ui/modal";
import { ProjectForm, ProjectTree, useProjectMutations } from "@/features/projects";
import {
  isProjectsSectionOpen,
  saveProjectsSectionOpen,
} from "@/features/projects/lib/tree-session";
import { SidebarQuote } from "@/widgets/app-shell/ui/sidebar-quote";

const KERRIGAN_VARIANTS = [kerrigan1, kerrigan2, kerrigan3, kerrigan4, kerrigan5, kerrigan6, kerrigan7];

interface NavItem {
  href: string;
  label: string;
  icon: LucideIcon;
}

const NAV_ITEMS: NavItem[] = [
  { href: "/", label: "Projects", icon: Folder },
  { href: "/kits", label: "Kits", icon: Package },
  { href: "/search", label: "Search", icon: Search },
  { href: "/settings", label: "Settings", icon: Settings },
];

export function AppSidebar() {
  const pathname = usePathname() ?? "/";
  const [kerrigan, setKerrigan] = useState<string | null>(null);
  const [projectsOpen, setProjectsOpen] = useState(false);
  const [registerPath, setRegisterPath] = useState<string | null>(null);
  const { create } = useProjectMutations();

  useEffect(() => {
    const frame = requestAnimationFrame(() => {
      setKerrigan(KERRIGAN_VARIANTS[Math.floor(Math.random() * KERRIGAN_VARIANTS.length)].src);
      setProjectsOpen(isProjectsSectionOpen());
    });
    return () => cancelAnimationFrame(frame);
  }, []);

  const handleProjectsToggle = () => {
    setProjectsOpen((value) => {
      const next = !value;
      saveProjectsSectionOpen(next);
      return next;
    });
  };

  return (
    <aside className="fixed inset-y-0 left-0 z-30 flex w-60 flex-col border-r border-border bg-surface shadow-2xl max-lg:hidden">
      {kerrigan !== null && (
        <img
          src={kerrigan}
          alt=""
          aria-hidden
          className="pointer-events-none absolute bottom-0 left-0 h-[68%] w-44 object-cover object-top opacity-35 [mask-image:linear-gradient(to_right,white_30%,var(--color-surface)_98%)] [mask-mode:luminance]"
        />
      )}
      <div className="relative flex items-center gap-2.5 px-4 pt-5">
        <img src={logoMark.src} alt="Overmind logo" className="h-8 w-auto" />
        <div>
          <p className="text-sm font-bold tracking-[0.3em] text-foreground">OVERMIND</p>
          <p className="text-[10px] uppercase tracking-widest text-muted">One context. Many minds.</p>
        </div>
      </div>
      <nav className="relative mt-8 flex min-h-0 flex-1 flex-col gap-1 overflow-y-auto px-3 pb-4" aria-label="Main navigation">
        {NAV_ITEMS.map((item) => {
          const active = item.href === "/" ? pathname === "/" : pathname.startsWith(item.href);
          const Icon = item.icon;

          if (item.href === "/") {
            return (
              <div key={item.href}>
                <div
                  className={cn(
                    "flex items-center gap-1 rounded-md border-l-2 pr-3 transition-colors",
                    active
                      ? "border-primary bg-primary/10 text-primary"
                      : "border-transparent text-muted hover:bg-surface-raised hover:text-foreground",
                  )}
                >
                  <button
                    type="button"
                    onClick={handleProjectsToggle}
                    aria-expanded={projectsOpen}
                    aria-label={projectsOpen ? "Collapse projects tree" : "Expand projects tree"}
                    className="shrink-0 rounded p-1 transition-colors hover:text-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary/40"
                  >
                    {projectsOpen ? <ChevronDown size={14} aria-hidden /> : <ChevronRight size={14} aria-hidden />}
                  </button>
                  <a
                    href="/"
                    aria-current={active ? "page" : undefined}
                    className={cn(
                      "flex min-w-0 flex-1 items-center gap-2.5 rounded-md py-2 text-sm font-medium transition-colors",
                      "focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary/40",
                      active ? "text-primary" : "hover:text-foreground",
                    )}
                  >
                    <Icon size={16} aria-hidden />
                    {item.label}
                  </a>
                </div>
                {projectsOpen ? (
                  <div className="mt-1 max-h-[50vh] overflow-y-auto p-1">
                    <ProjectTree onRegister={setRegisterPath} />
                  </div>
                ) : null}
              </div>
            );
          }

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

      <Modal
        title="Register project"
        open={registerPath !== null}
        onClose={() => setRegisterPath(null)}
      >
        {registerPath !== null ? (
          <ProjectForm
            initialPath={registerPath}
            pending={create.isPending}
            error={(create.error as Error | null) ?? null}
            onSubmit={(values) => create.mutate(values, { onSuccess: () => setRegisterPath(null) })}
            onCancel={() => setRegisterPath(null)}
          />
        ) : null}
      </Modal>
    </aside>
  );
}
