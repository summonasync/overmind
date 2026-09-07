"use client";

import { Folder, Clock } from "lucide-react";
import { useProjectList } from "@/features/projects";

export function ProjectsInfoPanel() {
  const { data: projects, loading } = useProjectList();

  if (loading) {
    return null;
  }

  const recent = [...projects].sort((a, b) => b.createdAt.localeCompare(a.createdAt)).slice(0, 5);

  return (
    <div className="space-y-6">
      <section>
        <h2 className="mb-3 flex items-center gap-2 text-sm font-semibold text-foreground">
          <Folder size={15} aria-hidden className="text-primary" />
          Project Overview
        </h2>
        <div className="grid grid-cols-2 gap-2">
          <div className="rounded-lg border border-border bg-surface p-3">
            <p className="text-2xl font-semibold text-foreground">{projects.length}</p>
            <p className="text-xs text-muted">Total Projects</p>
          </div>
          <div className="rounded-lg border border-border bg-surface p-3">
            <p className="text-2xl font-semibold text-foreground">
              {projects.filter((project) => project.description.trim().length > 0).length}
            </p>
            <p className="text-xs text-muted">With Description</p>
          </div>
        </div>
      </section>

      <section>
        <h2 className="mb-3 flex items-center gap-2 text-sm font-semibold text-foreground">
          <Clock size={15} aria-hidden className="text-primary" />
          Recent Projects
        </h2>
        {recent.length === 0 ? (
          <p className="text-xs text-muted">No projects yet.</p>
        ) : (
          <ul className="space-y-1">
            {recent.map((project) => (
              <li
                key={project.id}
                className="flex items-center justify-between gap-2 rounded-md bg-surface px-2.5 py-1.5"
              >
                <span className="truncate text-xs text-foreground">{project.name}</span>
                <span className="shrink-0 font-mono text-[10px] text-muted">
                  {new Date(project.createdAt).toLocaleDateString()}
                </span>
              </li>
            ))}
          </ul>
        )}
      </section>
    </div>
  );
}
