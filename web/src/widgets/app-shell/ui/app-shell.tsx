"use client";

import { Suspense, useCallback, useState, type ReactNode } from "react";
import { usePathname } from "next/navigation";
import background from "@/shared/assets/brand/background.png";
import { CaptureOpenProvider, CaptureOverlay, useCaptureHotkeys } from "@/features/journal";
import { InfoPanelProvider } from "@/widgets/app-shell/lib/info-panel-context";
import { AgentChatBubble } from "@/widgets/agent-chat-bubble";
import { ApprovalBubble } from "@/widgets/app-shell/ui/approval-bubble";
import { AttentionSurfaces } from "@/widgets/app-shell/ui/attention-surfaces";
import { AppTopbar } from "@/widgets/app-shell/ui/app-topbar";
import { MobileNav } from "@/widgets/app-shell/ui/mobile-nav";
import { ProjectsRailMount } from "@/widgets/app-shell/ui/projects-rail";
import { InfoPanel } from "@/widgets/app-shell/ui/info-panel";
import { ProjectTabs } from "@/widgets/app-shell/ui/project-tabs";
import { DegradedBanner } from "@/widgets/degraded-banner";

const COLLAPSED_STORAGE_KEY = "overmind.info-panel.collapsed";

interface AppShellProps {
  children: ReactNode;
}

export function AppShell({ children }: AppShellProps) {
  const pathname = usePathname() ?? "/";
  const [captureOpen, setCaptureOpen] = useState(false);
  const [panelCollapsed, setPanelCollapsed] = useState(
    () => typeof window !== "undefined" && window.localStorage.getItem(COLLAPSED_STORAGE_KEY) === "1",
  );

  const openCapture = useCallback(() => setCaptureOpen(true), []);
  const closeCapture = useCallback(() => setCaptureOpen(false), []);
  useCaptureHotkeys({ onOpen: openCapture, pathname });

  const togglePanel = useCallback(() => {
    setPanelCollapsed((current) => {
      const next = !current;
      window.localStorage.setItem(COLLAPSED_STORAGE_KEY, next ? "1" : "0");
      return next;
    });
  }, []);

  return (
    <InfoPanelProvider>
      <CaptureOpenProvider onOpen={openCapture}>
        <div className="min-h-screen bg-background text-foreground">
          <img
            src={background.src}
            alt=""
            aria-hidden
            className="pointer-events-none fixed inset-0 z-0 h-full w-full object-cover opacity-[0.07]"
          />
          <div className="relative z-10 flex min-h-screen flex-col">
            <AppTopbar />
            <MobileNav />
            <DegradedBanner />
            <div className="flex flex-1">
              <Suspense fallback={null}>
                <ProjectsRailMount />
              </Suspense>
              <main className="min-w-0 flex-1 p-4 lg:p-6">
                <ProjectTabs />
                {children}
              </main>
              <InfoPanel collapsed={panelCollapsed} onToggle={togglePanel} />
            </div>
          </div>
          <Suspense fallback={null}>
            <ApprovalBubble />
          </Suspense>
          <AttentionSurfaces />
          <AgentChatBubble />
          <CaptureOverlay open={captureOpen} onClose={closeCapture} />
        </div>
      </CaptureOpenProvider>
    </InfoPanelProvider>
  );
}
