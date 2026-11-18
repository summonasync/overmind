"use client";

import { cn } from "@/shared/lib/cn";
import { Badge } from "@/shared/ui/badge";
import { useProjectName } from "@/features/projects/lib/use-project-name";

interface ProjectNameBadgeProps {
  projectId: string;
  className?: string;
  plain?: boolean;
}

export function ProjectNameBadge({ projectId, className, plain }: ProjectNameBadgeProps) {
  const name = useProjectName(projectId);
  const badge = (
    <Badge tone="info" className={cn("normal-case", className)}>
      {name}
    </Badge>
  );

  if (plain) {
    return (
      <span title={`Project ${projectId}`} className="inline-flex">
        {badge}
      </span>
    );
  }

  return (
    <a
      href={`/dashboard?project=${projectId}`}
      title={`Open ${name} dashboard`}
      className="inline-flex rounded transition-shadow hover:ring-1 hover:ring-info/50 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary/40"
    >
      {badge}
    </a>
  );
}
