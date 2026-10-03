"use client";

import { cn } from "@/shared/lib/cn";
import { Badge } from "@/shared/ui/badge";
import { useProjectName } from "@/features/projects/lib/use-project-name";

interface ProjectNameBadgeProps {
  projectId: string;
  className?: string;
}

export function ProjectNameBadge({ projectId, className }: ProjectNameBadgeProps) {
  const name = useProjectName(projectId);

  return (
    <span title={`Project ${projectId}`} className="inline-flex">
      <Badge tone="info" className={cn("normal-case", className)}>
        {name}
      </Badge>
    </span>
  );
}
