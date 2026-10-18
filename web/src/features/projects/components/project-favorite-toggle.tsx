"use client";

import { Star } from "lucide-react";
import { cn } from "@/shared/lib/cn";
import { useProjectFavoriteToggle } from "@/features/projects/lib/use-projects";
import type { ProjectResponse } from "@/features/projects/types";

interface ProjectFavoriteToggleProps {
  project: ProjectResponse;
  className?: string;
}

export function ProjectFavoriteToggle({ project, className }: ProjectFavoriteToggleProps) {
  const toggle = useProjectFavoriteToggle();
  const favorite = project.isFavorite;
  const label = favorite
    ? `Remove ${project.name} from favorites`
    : `Add ${project.name} to favorites`;

  return (
    <button
      type="button"
      aria-pressed={favorite}
      aria-label={label}
      title={label}
      disabled={toggle.isPending}
      onClick={(event) => {
        event.preventDefault();
        event.stopPropagation();
        toggle.mutate({ id: project.id, isFavorite: !favorite });
      }}
      className={cn(
        "shrink-0 rounded p-1 transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary/40 disabled:opacity-50",
        favorite ? "text-primary" : "text-muted hover:text-foreground",
        className,
      )}
    >
      <Star size={14} aria-hidden className={favorite ? "fill-current" : undefined} />
    </button>
  );
}
