"use client";

import { useEffect } from "react";
import { useSearchParams } from "next/navigation";
import { useProjectList } from "@/features/projects";
import { resolvedProjectName } from "@/shared/lib/project-display";
import { titleFor } from "@/widgets/app-shell/lib/tab-title";

export function useTabTitle(awaitingCount: number) {
  const searchParams = useSearchParams();
  const { data: projects } = useProjectList();
  const projectId = searchParams?.get("project") || null;
  const projectName = resolvedProjectName(projects, projectId);

  useEffect(() => {
    document.title = titleFor(projectName, awaitingCount);
  }, [projectName, awaitingCount]);
}
