"use client";

import { useMemo } from "react";
import { FolderCog } from "lucide-react";
import { EmptyState } from "@/shared/ui/empty-state";
import { ErrorText } from "@/shared/ui/error-text";
import { LoadingState } from "@/shared/ui/loading-state";
import { PageHeader } from "@/shared/ui/page-header";
import { ProjectsInfoPanel, useInfoPanel } from "@/widgets/app-shell";
import { useProjectList } from "@/features/projects";
import { ProjectCard } from "./project-card";

const manageLinkClasses =
  "inline-flex items-center justify-center gap-1.5 rounded-md bg-primary px-3 py-1.5 text-sm font-medium text-primary-foreground transition-colors hover:bg-primary-hover focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary/40 focus-visible:ring-offset-2 focus-visible:ring-offset-background";

const secondaryLinkClasses =
  "inline-flex items-center justify-center gap-1.5 rounded-md border border-border bg-surface px-3 py-1.5 text-sm font-medium text-foreground transition-colors hover:border-border-strong hover:bg-surface-raised focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary/40 focus-visible:ring-offset-2 focus-visible:ring-offset-background";

export function ProjectsPage() {
  const { data: projects, loading, error } = useProjectList();
  const sorted = useMemo(
    () => [...projects].sort((a, b) => a.name.localeCompare(b.name)),
    [projects],
  );

  useInfoPanel(<ProjectsInfoPanel />);

  return (
    <div className="space-y-4">
      <PageHeader
        title="Projects"
        description="Organize coding-agent context, from codebases to documentation, in one unified space."
        actions={
          <a href="/settings/projects" className={manageLinkClasses}>
            <FolderCog size={14} aria-hidden />
            Manage projects
          </a>
        }
      />

      <ErrorText error={error} />

      {loading ? (
        <LoadingState label="Loading projects…" />
      ) : sorted.length === 0 ? (
        <EmptyState
          title="No projects yet"
          hint="Register a project and it will show up here."
          action={
            <a href="/settings/projects" className={secondaryLinkClasses}>
              <FolderCog size={14} aria-hidden />
              Manage projects
            </a>
          }
        />
      ) : (
        <div className="grid grid-cols-1 gap-3 md:grid-cols-2 xl:grid-cols-3">
          {sorted.map((project) => (
            <ProjectCard key={project.id} project={project} />
          ))}
        </div>
      )}
    </div>
  );
}
