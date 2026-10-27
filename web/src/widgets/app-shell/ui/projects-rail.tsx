"use client";

import { useEffect, useMemo, useState } from "react";
import { usePathname, useSearchParams } from "next/navigation";
import { Brain, FolderCog, FolderPlus, FolderTree, List, RefreshCw, Star, type LucideIcon } from "lucide-react";
import { cn } from "@/shared/lib/cn";
import { Modal } from "@/shared/ui/modal";
import { Select } from "@/shared/ui/select";
import {
  FolderPickerModal,
  ProjectForm,
  ProjectTree,
  useAddTreeRoot,
  useProjectList,
  useProjectMutations,
} from "@/features/projects";
import {
  filterFavoriteProjects,
  sortProjects,
  type ProjectSortField,
} from "@/features/projects/lib/project-order";
import {
  getFavoritesOnly,
  getProjectSortField,
  getRailView,
  saveFavoritesOnly,
  saveLastActiveProjectId,
  saveProjectSortField,
  saveRailView,
  type RailView,
} from "@/features/projects/lib/tree-session";

const CONTOUR_PATHS = ["/", "/dashboard", "/entries", "/lanes", "/planning", "/wave-chat"];

interface ViewOption {
  value: RailView;
  label: string;
  icon: LucideIcon;
}

const VIEW_OPTIONS: ViewOption[] = [
  { value: "list", label: "List view", icon: List },
  { value: "tree", label: "Tree view", icon: FolderTree },
];

export function ProjectsRailMount() {
  const pathname = usePathname() ?? "/";
  const searchParams = useSearchParams();

  const onContour = CONTOUR_PATHS.includes(pathname);
  const onIssues = pathname === "/issues" && (searchParams?.has("project") ?? false);

  if (!onContour && !onIssues) {
    return null;
  }

  return <ProjectsRail />;
}

function ProjectsRail() {
  const searchParams = useSearchParams();
  const [view, setView] = useState<RailView>("tree");
  const [registerPath, setRegisterPath] = useState<string | null>(null);
  const [pickerOpen, setPickerOpen] = useState(false);
  const [addError, setAddError] = useState<string | null>(null);
  const addRoot = useAddTreeRoot();
  const { create } = useProjectMutations();

  useEffect(() => {
    const frame = requestAnimationFrame(() => {
      setView(getRailView());
    });
    return () => cancelAnimationFrame(frame);
  }, []);

  const handleViewChange = (next: RailView) => {
    setView(next);
    saveRailView(next);
  };

  const handleAddRoot = () => {
    setAddError(null);
    setPickerOpen(true);
  };

  const handleRootPicked = async (path: string) => {
    setPickerOpen(false);
    try {
      await addRoot.mutateAsync(path);
    } catch (err) {
      setAddError(err instanceof Error ? err.message : "Failed to add root folder.");
    }
  };

  const activeProjectId = searchParams?.get("project") || null;

  return (
    <aside className="hidden w-60 shrink-0 flex-col border-r border-border bg-surface lg:sticky lg:top-14 lg:flex lg:h-[calc(100vh-3.5rem)] lg:self-start">
      <div className="flex items-center gap-1 px-4 pb-1 pt-4">
        <span className="min-w-0 flex-1 truncate text-[11px] font-semibold uppercase tracking-wide text-muted">
          Mindboards
        </span>
        {view === "tree" ? (
          <button
            type="button"
            onClick={handleAddRoot}
            disabled={addRoot.isPending}
            aria-label="Add root folder"
            title="Add root folder"
            className="shrink-0 rounded p-1 text-muted transition-colors hover:text-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary/40 disabled:opacity-50"
          >
            <FolderPlus size={14} aria-hidden />
          </button>
        ) : null}
        <div
          className="flex shrink-0 items-center gap-0.5 rounded-md border border-border bg-surface-raised p-0.5"
          role="group"
          aria-label="Projects rail view"
        >
          {VIEW_OPTIONS.map((option) => {
            const Icon = option.icon;
            const selected = view === option.value;
            return (
              <button
                key={option.value}
                type="button"
                onClick={() => handleViewChange(option.value)}
                aria-label={option.label}
                aria-pressed={selected}
                title={option.label}
                className={cn(
                  "rounded p-1 transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary/40",
                  selected ? "bg-primary/15 text-primary" : "text-muted hover:text-foreground",
                )}
              >
                <Icon size={14} aria-hidden />
              </button>
            );
          })}
        </div>
      </div>
      {addError ? <p className="px-4 pb-1 text-xs text-danger">{addError}</p> : null}
      <div className="app-rail-scroll min-h-0 flex-1 overflow-y-auto px-3 py-3">
        {view === "tree" ? (
          <ProjectTree onRegister={setRegisterPath} />
        ) : (
          <ProjectListRows activeProjectId={activeProjectId} />
        )}
      </div>
      <div className="border-t border-border/60 px-3 py-2">
        <a
          href="/settings/projects"
          className="flex items-center gap-2 rounded-md px-2 py-2 text-sm font-medium text-muted transition-colors hover:bg-surface-raised hover:text-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary/40"
        >
          <FolderCog size={16} aria-hidden />
          Manage projects
        </a>
      </div>
      <Modal
        title="Register project"
        open={registerPath !== null}
        onClose={() => setRegisterPath(null)}
      >
        {registerPath !== null ? (
          <ProjectForm
            initialPath={registerPath}
            pending={create.isPending}
            error={(create.error as Error | null) ?? null}
            onSubmit={(values) => create.mutate(values, { onSuccess: () => setRegisterPath(null) })}
            onCancel={() => setRegisterPath(null)}
          />
        ) : null}
      </Modal>
      <FolderPickerModal
        title="Add root folder"
        open={pickerOpen}
        onClose={() => setPickerOpen(false)}
        onSelect={(path) => void handleRootPicked(path)}
      />
    </aside>
  );
}

interface ProjectListRowsProps {
  activeProjectId: string | null;
}

function ProjectListRows({ activeProjectId }: ProjectListRowsProps) {
  const { data: projects, loading, error, refetch } = useProjectList();
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

  if (loading) {
    return <p className="px-2 py-1 text-xs text-muted">Loading projects…</p>;
  }

  if (error) {
    return (
      <div className="flex items-center gap-2 px-3 py-1">
        <p className="min-w-0 flex-1 truncate text-xs text-danger">{error.message}</p>
        <button
          type="button"
          onClick={() => refetch()}
          aria-label="Retry loading projects"
          className="shrink-0 rounded p-0.5 text-muted transition-colors hover:text-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary/40"
        >
          <RefreshCw size={12} aria-hidden />
        </button>
      </div>
    );
  }

  if (projects.length === 0) {
    return <p className="px-2 py-1 text-xs text-muted">No registered projects yet.</p>;
  }

  return (
    <div className="flex flex-col gap-1">
      <div className="flex flex-col gap-1.5 px-2 pb-1">
        <div
          role="group"
          aria-label="Filter projects"
          className="flex items-center gap-0.5 rounded-md border border-border bg-surface-raised p-0.5"
        >
          <button
            type="button"
            aria-pressed={!favoritesOnly}
            onClick={() => handleFavoritesOnly(false)}
            title="All projects"
            className={cn(
              "flex min-w-0 flex-1 items-center justify-center gap-1 rounded px-1.5 py-0.5 text-[11px] font-medium transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary/40",
              !favoritesOnly ? "bg-primary/15 text-primary" : "text-muted hover:text-foreground",
            )}
          >
            All
          </button>
          <button
            type="button"
            aria-pressed={favoritesOnly}
            onClick={() => handleFavoritesOnly(true)}
            title="Favorite projects"
            className={cn(
              "flex min-w-0 flex-1 items-center justify-center gap-1 rounded px-1.5 py-0.5 text-[11px] font-medium transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary/40",
              favoritesOnly ? "bg-primary/15 text-primary" : "text-muted hover:text-foreground",
            )}
          >
            <Star size={11} aria-hidden />
            Favorites
          </button>
        </div>
        <Select
          aria-label="Sort projects"
          size="sm"
          value={sortField}
          onChange={(event) => handleSortField(event.target.value as ProjectSortField)}
          className="w-full"
        >
          <option value="name">Name</option>
          <option value="openFeatures">Open features</option>
        </Select>
      </div>
      {visible.length === 0 ? (
        <p className="px-2 py-1 text-xs text-muted">No favorite projects yet.</p>
      ) : (
        <div className="flex flex-col gap-0.5">
          {visible.map((project) => {
            const active = activeProjectId !== null && project.id === activeProjectId;
            return (
              <a
                key={project.id}
                href={`/dashboard?project=${project.id}`}
                title={project.diskPath}
                aria-current={active ? "page" : undefined}
                onClick={() => saveLastActiveProjectId(project.id)}
                className={cn(
                  "flex min-w-0 items-center gap-2 rounded-md border-l-2 px-2 py-2 text-sm font-medium transition-colors",
                  "focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary/40",
                  active
                    ? "border-primary bg-primary/10 text-primary"
                    : "border-transparent text-muted hover:bg-surface-raised hover:text-foreground",
                )}
              >
                <Brain size={16} aria-hidden className={cn("shrink-0", !active && "text-primary-hover")} />
                <span className="truncate">{project.name}</span>
              </a>
            );
          })}
        </div>
      )}
    </div>
  );
}
