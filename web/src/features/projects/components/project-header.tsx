"use client";

import type { ReactNode } from "react";
import { useProjectName } from "@/features/projects/lib/use-project-name";

interface ProjectHeaderProps {
  projectId: string;
  secondary?: ReactNode;
}

export function ProjectHeader({ projectId, secondary }: ProjectHeaderProps) {
  const name = useProjectName(projectId);

  return (
    <header className="mb-6">
      <h1 className="text-2xl font-semibold text-foreground">{name}</h1>
      {secondary ? <div className="mt-1 text-sm text-muted">{secondary}</div> : null}
    </header>
  );
}
