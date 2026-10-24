"use client";

import { Suspense, useEffect, useState } from "react";
import { usePathname, useSearchParams } from "next/navigation";
import { Tabs, type TabsItem } from "@/shared/ui/tabs";
import { useProjectName } from "@/features/projects";
import { getLastActiveProjectId } from "@/features/projects/lib/tree-session";

const NO_PROJECT_HINT = "Select a project first";

const PROJECT_TAB_ROUTES = [
  { key: "dashboard", label: "Dashboard", pathname: "/dashboard" },
  { key: "entries", label: "Entries", pathname: "/entries" },
  { key: "issues", label: "Issues", pathname: "/issues" },
  { key: "lanes", label: "Lanes", pathname: "/lanes" },
  { key: "chat", label: "Chat", pathname: "/wave-chat" },
] as const;

type ProjectTabKey = (typeof PROJECT_TAB_ROUTES)[number]["key"];

export function ProjectTabs() {
  const pathname = usePathname() ?? "/";
  const active = PROJECT_TAB_ROUTES.find((route) => route.pathname === pathname);

  if (!active) {
    return null;
  }

  return (
    <div className="pb-4 lg:pb-6">
      <Suspense fallback={null}>
        <ProjectTabsBar activeKey={active.key} />
      </Suspense>
    </div>
  );
}

interface ProjectTabsBarProps {
  activeKey: ProjectTabKey;
}

function ProjectTabsBar({ activeKey }: ProjectTabsBarProps) {
  const searchParams = useSearchParams();
  const [lastActiveProject, setLastActiveProject] = useState<string | null>(null);

  useEffect(() => {
    const frame = requestAnimationFrame(() => {
      setLastActiveProject(getLastActiveProjectId());
    });
    return () => cancelAnimationFrame(frame);
  }, []);

  const projectParam = searchParams?.get("project") || null;
  const projectId = projectParam ?? lastActiveProject;
  const isFeatureView = Boolean(searchParams?.get("feature"));

  const items: TabsItem[] = PROJECT_TAB_ROUTES.map((route) => ({
    key: route.key,
    label: route.label,
    href: projectId !== null ? `${route.pathname}?project=${encodeURIComponent(projectId)}` : null,
    title: projectId === null ? NO_PROJECT_HINT : undefined,
  }));

  return (
    <>
      {projectId !== null ? <ProjectName projectId={projectId} asHeading={!isFeatureView} /> : null}
      <Tabs items={items} activeKey={activeKey} ariaLabel="Project sections" />
    </>
  );
}

interface ProjectNameProps {
  projectId: string;
  asHeading: boolean;
}

function ProjectName({ projectId, asHeading }: ProjectNameProps) {
  const name = useProjectName(projectId);
  const Tag = asHeading ? "h1" : "p";

  return (
    <Tag className="mb-2 truncate font-mono text-sm font-semibold tracking-wide text-primary">
      {name}
    </Tag>
  );
}
