"use client";

import { Folder, Pencil, Trash2 } from "lucide-react";
import { Badge } from "@/shared/ui/badge";
import { Button } from "@/shared/ui/button";
import { EmptyState } from "@/shared/ui/empty-state";
import { isKitLibraryProject } from "@/features/projects/lib/kit-library";
import type { ProjectResponse } from "@/features/projects/types";

interface ProjectListProps {
  projects: ProjectResponse[];
  onEdit: (project: ProjectResponse) => void;
  onDelete: (project: ProjectResponse) => void;
}

export function ProjectList({ projects, onEdit, onDelete }: ProjectListProps) {
  if (projects.length === 0) {
    return <EmptyState title="No projects yet" hint="Create your first project to get started." />;
  }

  return (
    <ul className="divide-y divide-border overflow-hidden rounded-lg border border-border bg-surface">
      {projects.map((project) => (
        <li
          key={project.id}
          className="flex items-center justify-between gap-4 px-4 py-3 transition-colors hover:bg-surface-raised"
        >
          <div className="min-w-0">
            <div className="flex items-center gap-2">
              <Folder size={16} aria-hidden className="shrink-0 text-primary" />
              <a
                href={`/entries?project=${project.id}`}
                className="truncate text-sm font-semibold text-foreground hover:text-primary focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary/40"
              >
                {project.name}
              </a>
              {isKitLibraryProject(project) ? (
                <span title="System project that stores entries of imported kits.">
                  <Badge tone="info">library</Badge>
                </span>
              ) : null}
              <Badge className="font-mono normal-case">{project.diskPath}</Badge>
            </div>
            {project.description ? (
              <p className="mt-0.5 truncate text-xs text-muted">{project.description}</p>
            ) : null}
          </div>
          <div className="flex shrink-0 gap-1">
            <Button variant="secondary" onClick={() => onEdit(project)}>
              <Pencil size={13} aria-hidden />
              Edit
            </Button>
            <Button variant="danger" onClick={() => onDelete(project)}>
              <Trash2 size={13} aria-hidden />
              Delete
            </Button>
          </div>
        </li>
      ))}
    </ul>
  );
}
