"use client";

import { useState } from "react";
import { FolderOpen } from "lucide-react";
import { Badge } from "@/shared/ui/badge";
import { Button } from "@/shared/ui/button";
import { formatCalendarDate } from "@/shared/lib/relative-time";
import { ProjectMovedDialog } from "@/features/projects/components/project-moved-dialog";
import type { ProjectResponse } from "@/features/projects/types";

export function ProjectDetail({ project }: { project: ProjectResponse }) {
  const [movedOpen, setMovedOpen] = useState(false);

  return (
    <div className="space-y-3">
      <div className="flex items-center gap-2">
        <FolderOpen size={16} aria-hidden className="shrink-0 text-primary" />
        <Badge className="min-w-0 flex-1 font-mono normal-case">
          <span className="truncate">{project.diskPath}</span>
        </Badge>
        <Button
          variant="secondary"
          size="sm"
          className="shrink-0"
          onClick={() => setMovedOpen(true)}
        >
          Project moved to…
        </Button>
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
      {movedOpen ? (
        <ProjectMovedDialog key={project.id} project={project} onClose={() => setMovedOpen(false)} />
      ) : null}
    </div>
  );
}
