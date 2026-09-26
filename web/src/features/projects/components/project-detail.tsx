"use client";

import { FolderOpen } from "lucide-react";
import { Badge } from "@/shared/ui/badge";
import { formatCalendarDate } from "@/shared/lib/relative-time";
import type { ProjectResponse } from "@/features/projects/types";

export function ProjectDetail({ project }: { project: ProjectResponse }) {
  return (
    <div className="space-y-3">
      <div className="flex items-center gap-2">
        <FolderOpen size={16} aria-hidden className="shrink-0 text-primary" />
        <Badge className="min-w-0 font-mono normal-case">
          <span className="truncate">{project.diskPath}</span>
        </Badge>
      </div>
      {project.description ? (
        <p className="text-sm text-foreground">{project.description}</p>
      ) : null}
      <p className="text-xs text-muted">
        Created {formatCalendarDate(project.createdAt)}
      </p>
      <div>
        <a
          href={`/dashboard?project=${project.id}`}
          className="text-sm font-medium text-primary underline-offset-2 hover:underline focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary/40"
        >
          Open dashboard →
        </a>
      </div>
    </div>
  );
}
