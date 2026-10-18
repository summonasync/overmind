"use client";

import { Folder } from "lucide-react";
import { Badge } from "@/shared/ui/badge";
import { cn } from "@/shared/lib/cn";
import { isKitLibraryProject } from "@/features/projects/lib/kit-library";
import { saveLastActiveProjectId } from "@/features/projects/lib/tree-session";
import type { ProjectResponse } from "@/features/projects";

interface ProjectCardProps {
  project: ProjectResponse;
}

export function ProjectCard({ project }: ProjectCardProps) {
  return (
    <a
      href={`/dashboard?project=${project.id}`}
      title={project.diskPath}
      onClick={() => saveLastActiveProjectId(project.id)}
      className={cn(
        "flex min-w-0 flex-col gap-1.5 rounded-lg border border-border bg-surface p-4 transition-colors",
        "hover:border-border-strong hover:bg-surface-raised",
        "focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary/40 focus-visible:ring-offset-2 focus-visible:ring-offset-background",
      )}
    >
      <span className="flex min-w-0 items-center gap-2">
        <Folder size={16} aria-hidden className="shrink-0 text-primary" />
        <span className="min-w-0 truncate text-sm font-semibold text-foreground">
          {project.name}
        </span>
        {isKitLibraryProject(project) ? (
          <span title="System project that stores entries of imported kits." className="shrink-0">
            <Badge tone="info">library</Badge>
          </span>
        ) : null}
      </span>
      <span className="truncate font-mono text-xs text-muted">{project.diskPath}</span>
      {project.description ? (
        <span className="truncate text-xs text-muted">{project.description}</span>
      ) : null}
    </a>
  );
}
