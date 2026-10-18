"use client";

import { useEffect, useMemo, useState } from "react";
import { FolderCog, Star } from "lucide-react";
import { Select } from "@/shared/ui/select";
import { EmptyState } from "@/shared/ui/empty-state";
import { ErrorText } from "@/shared/ui/error-text";
import { LoadingState } from "@/shared/ui/loading-state";
import { PageHeader } from "@/shared/ui/page-header";
import { ProjectsInfoPanel, useInfoPanel } from "@/widgets/app-shell";
import { useProjectList } from "@/features/projects";
import {
  filterFavoriteProjects,
  sortProjects,
  type ProjectSortField,
} from "@/features/projects/lib/project-order";
import {
  getFavoritesOnly,
  getProjectSortField,
  saveFavoritesOnly,
  saveProjectSortField,
} from "@/features/projects/lib/tree-session";
import { ProjectCard } from "./project-card";

const manageLinkClasses =
  "inline-flex items-center justify-center gap-1.5 rounded-md bg-primary px-3 py-1.5 text-sm font-medium text-primary-foreground transition-colors hover:bg-primary-hover focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary/40 focus-visible:ring-offset-2 focus-visible:ring-offset-background";

const secondaryLinkClasses =
  "inline-flex items-center justify-center gap-1.5 rounded-md border border-border bg-surface px-3 py-1.5 text-sm font-medium text-foreground transition-colors hover:border-border-strong hover:bg-surface-raised focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary/40 focus-visible:ring-offset-2 focus-visible:ring-offset-background";

const favoritesChipClasses = (active: boolean) =>
  `inline-flex items-center gap-1 rounded-md border px-2 py-1 text-xs font-medium transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary/40 ${
    active
      ? "border-primary bg-primary/10 text-primary"
      : "border-border text-muted hover:border-border-strong hover:text-foreground"
  }`;

export function ProjectsPage() {
  const { data: projects, loading, error } = useProjectList();
  const [favoritesOnly, setFavoritesOnly] = useState(false);
  const [sortField, setSortField] = useState<ProjectSortField>("name");

  useEffect(() => {
    const frame = requestAnimationFrame(() => {
      setFavoritesOnly(getFavoritesOnly());
      setSortField(getProjectSortField());
    });
    return () => cancelAnimationFrame(frame);
  }, []);

  const visible = useMemo(
    () => sortProjects(filterFavoriteProjects(projects, favoritesOnly), sortField),
    [projects, favoritesOnly, sortField],
  );

  const handleFavoritesOnly = (next: boolean) => {
    setFavoritesOnly(next);
    saveFavoritesOnly(next);
  };

  const handleSortField = (next: ProjectSortField) => {
    setSortField(next);
    saveProjectSortField(next);
  };

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
      ) : projects.length === 0 ? (
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
        <>
          <div className="flex flex-wrap items-center gap-2">
            <div role="group" aria-label="Filter projects" className="flex items-center gap-1.5">
              <button
                type="button"
                aria-pressed={!favoritesOnly}
                onClick={() => handleFavoritesOnly(false)}
                className={favoritesChipClasses(!favoritesOnly)}
              >
                All
              </button>
              <button
                type="button"
                aria-pressed={favoritesOnly}
                onClick={() => handleFavoritesOnly(true)}
                className={favoritesChipClasses(favoritesOnly)}
              >
                <Star size={12} aria-hidden />
                Favorites
              </button>
            </div>
            <label className="ml-auto flex items-center gap-1.5 text-xs text-muted">
              Sort
              <Select
                aria-label="Sort projects"
                value={sortField}
                onChange={(event) => handleSortField(event.target.value as ProjectSortField)}
              >
                <option value="name">Name</option>
                <option value="openFeatures">Open features</option>
              </Select>
            </label>
          </div>

          {visible.length === 0 ? (
            <EmptyState
              title="No favorites yet"
              hint="Star a project to keep it at the top of the list."
            />
          ) : (
            <div className="grid grid-cols-1 gap-3 md:grid-cols-2 xl:grid-cols-3">
              {visible.map((project) => (
                <ProjectCard key={project.id} project={project} />
              ))}
            </div>
          )}
        </>
      )}
    </div>
  );
}
