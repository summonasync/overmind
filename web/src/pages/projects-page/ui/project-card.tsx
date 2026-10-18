"use client";

import { Brain } from "lucide-react";
import { Badge } from "@/shared/ui/badge";
import { cn } from "@/shared/lib/cn";
import { isKitLibraryProject } from "@/features/projects/lib/kit-library";
import { saveLastActiveProjectId } from "@/features/projects/lib/tree-session";
import { ProjectFavoriteToggle } from "@/features/projects";
import { ProjectCardStats } from "./project-card-stats";
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
        <Brain size={16} aria-hidden className="shrink-0 text-primary" />
        <span className="min-w-0 truncate text-sm font-semibold text-foreground">
          {project.name}
        </span>
        {isKitLibraryProject(project) ? (
          <span title="System project that stores entries of imported kits." className="shrink-0">
            <Badge tone="info">library</Badge>
          </span>
        ) : null}
        <ProjectFavoriteToggle project={project} className="ml-auto -mr-1" />
      </span>
      {project.description ? (
        <span className="line-clamp-2 text-sm leading-snug text-muted">{project.description}</span>
      ) : null}
      <ProjectCardStats projectId={project.id} />
    </a>
  );
}
