"use client";

import { useEffect, useMemo, useState, type ReactNode } from "react";
import {
  Brain,
  ChevronDown,
  ChevronRight,
  Folder,
  FolderPlus,
  Plus,
  RefreshCw,
  X,
} from "lucide-react";
import { cn } from "@/shared/lib/cn";
import { Button } from "@/shared/ui/button";
import { Modal } from "@/shared/ui/modal";
import { browseFolder } from "@/shared/api/system";
import {
  useAddTreeRoot,
  useDirectory,
  useRemoveTreeRoot,
  useTreeRoots,
} from "@/features/projects/lib/use-project-tree";
import { useProjectList } from "@/features/projects/lib/use-projects";
import {
  getExpandedPaths,
  saveExpandedPaths,
  saveLastActiveProjectId,
} from "@/features/projects/lib/tree-session";
import { projectAncestorPaths } from "@/features/projects/lib/tree-paths";
import type { DirectoryEntry, TreeRoot } from "@/shared/api/types";

interface ProjectTreeProps {
  onRegister: (path: string) => void;
}

export function ProjectTree({ onRegister }: ProjectTreeProps) {
  const { data, isPending, error, refetch } = useTreeRoots();
  const { data: projects } = useProjectList();
  const addRoot = useAddTreeRoot();
  const [addError, setAddError] = useState<string | null>(null);
  const [expandedPaths, setExpandedPaths] = useState<string[]>([]);
  const [routeCollapsed, setRouteCollapsed] = useState<string[]>([]);
  const [sessionReady, setSessionReady] = useState(false);
  const [activeProjectId, setActiveProjectId] = useState<string | null>(null);

  useEffect(() => {
    const frame = requestAnimationFrame(() => {
      setExpandedPaths(getExpandedPaths());
      setActiveProjectId(new URLSearchParams(window.location.search).get("project") || null);
      setSessionReady(true);
    });
    return () => cancelAnimationFrame(frame);
  }, []);

  useEffect(() => {
    if (sessionReady) {
      saveExpandedPaths(expandedPaths);
    }
  }, [sessionReady, expandedPaths]);

  const routeProjectPath = useMemo(() => {
    if (activeProjectId === null) {
      return null;
    }
    return projects.find((project) => project.id === activeProjectId)?.diskPath ?? null;
  }, [projects, activeProjectId]);

  const autoExpandedPaths = useMemo(() => {
    if (routeProjectPath === null) {
      return [];
    }
    return projectAncestorPaths(
      (data?.roots ?? []).map((root) => root.path),
      routeProjectPath,
    );
  }, [data, routeProjectPath]);

  const isExpanded = (path: string) => {
    if (routeCollapsed.includes(path)) {
      return false;
    }
    return expandedPaths.includes(path) || autoExpandedPaths.includes(path);
  };

  const toggleExpanded = (path: string) => {
    if (isExpanded(path)) {
      if (autoExpandedPaths.includes(path)) {
        setRouteCollapsed((paths) => [...paths, path]);
      } else {
        setExpandedPaths((paths) => paths.filter((value) => value !== path));
      }
      return;
    }
    if (routeCollapsed.includes(path)) {
      setRouteCollapsed((paths) => paths.filter((value) => value !== path));
      return;
    }
    setExpandedPaths((paths) => [...paths, path]);
  };

  const handleAddRoot = async () => {
    setAddError(null);
    const selected = await browseFolder();
    if (!selected) {
      return;
    }

    try {
      await addRoot.mutateAsync(selected.path);
    } catch (err) {
      setAddError(err instanceof Error ? err.message : "Failed to add root folder.");
    }
  };

  if (isPending) {
    return <p className="px-3 py-1 text-xs text-muted">Loading folders…</p>;
  }

  if (error) {
    return (
      <div className="flex items-center gap-2 px-3 py-1">
        <p className="min-w-0 flex-1 truncate text-xs text-danger">{error.message}</p>
        <button
          type="button"
          onClick={() => refetch()}
          aria-label="Retry loading folders"
          className="shrink-0 rounded p-0.5 text-muted transition-colors hover:text-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary/40"
        >
          <RefreshCw size={12} aria-hidden />
        </button>
      </div>
    );
  }

  if (data.roots.length === 0) {
    return (
      <div className="flex flex-col items-center gap-2 px-3 py-4 text-center">
        <p className="text-xs text-muted">No root folders yet. Add one to browse your projects.</p>
        <Button variant="accent" onClick={() => void handleAddRoot()} disabled={addRoot.isPending}>
          <FolderPlus size={14} aria-hidden />
          Add root folder
        </Button>
        {addError ? <p className="text-xs text-danger">{addError}</p> : null}
      </div>
    );
  }

  return (
    <div className="flex flex-col gap-0.5">
      {data.roots.map((root) => (
        <TreeNode
          key={root.path}
          entry={rootToEntry(root)}
          depth={0}
          onRegister={onRegister}
          onToggleExpanded={toggleExpanded}
          isExpanded={isExpanded}
          activeProjectId={activeProjectId}
          actions={<RootRemoveButton root={root} />}
        />
      ))}
    </div>
  );
}

function RootRemoveButton({ root }: { root: TreeRoot }) {
  const removeRoot = useRemoveTreeRoot();
  const [confirming, setConfirming] = useState(false);
  const [error, setError] = useState<string | null>(null);

  if (root.containsProjects) {
    return (
      <button
        type="button"
        disabled
        aria-label={`${root.name} contains registered projects`}
        title="Contains registered projects"
        className="shrink-0 rounded p-0.5 opacity-40"
      >
        <X size={13} aria-hidden />
      </button>
    );
  }

  const handleConfirm = async () => {
    setError(null);
    try {
      await removeRoot.mutateAsync(root.path);
      setConfirming(false);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to remove root folder.");
    }
  };

  return (
    <>
      <button
        type="button"
        onClick={() => {
          setError(null);
          setConfirming(true);
        }}
        aria-label={`Remove ${root.name}`}
        title="Remove root folder"
        className={cn(
          "shrink-0 rounded p-0.5 text-muted opacity-0 transition-opacity",
          "hover:text-danger hover:bg-danger/10 focus-visible:opacity-100 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary/40",
          "group-hover:opacity-100",
        )}
      >
        <X size={13} aria-hidden />
      </button>
      <Modal title="Remove root folder" open={confirming} onClose={() => setConfirming(false)}>
        <p className="text-sm text-foreground">
          Remove <span className="font-medium">{root.name}</span> from the tree?
        </p>
        <p className="mt-1 break-all text-xs text-muted">{root.path}</p>
        {error ? <p className="mt-2 text-xs text-danger">{error}</p> : null}
        <div className="mt-4 flex justify-end gap-2">
          <Button variant="secondary" onClick={() => setConfirming(false)}>
            Cancel
          </Button>
          <Button variant="danger" onClick={() => void handleConfirm()} disabled={removeRoot.isPending}>
            Remove
          </Button>
        </div>
      </Modal>
    </>
  );
}

function rootToEntry(root: TreeRoot): DirectoryEntry {
  return {
    name: root.name,
    path: root.path,
    accessible: true,
    projectId: null,
    projectName: null,
  };
}

interface TreeNodeProps {
  entry: DirectoryEntry;
  depth: number;
  onRegister: (path: string) => void;
  onToggleExpanded: (path: string) => void;
  isExpanded: (path: string) => boolean;
  activeProjectId: string | null;
  actions?: ReactNode;
}

function TreeNode({ entry, depth, onRegister, onToggleExpanded, isExpanded, activeProjectId, actions }: TreeNodeProps) {
  const expanded = isExpanded(entry.path);

  if (entry.projectId !== null) {
    const active = activeProjectId !== null && entry.projectId === activeProjectId;

    return (
      <div style={{ paddingLeft: depth * 12 }} className="flex items-center">
        <a
          href={`/dashboard?project=${entry.projectId}`}
          title={entry.path}
          aria-current={active ? "page" : undefined}
          onClick={() => saveLastActiveProjectId(entry.projectId)}
          className={cn(
            "flex min-w-0 flex-1 items-center gap-2 rounded-md border-l-2 px-2 py-2 text-sm font-medium transition-colors",
            "focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary/40",
            active
              ? "border-primary bg-primary/10 text-primary"
              : "border-transparent text-muted hover:bg-surface-raised hover:text-foreground",
          )}
        >
          <Brain size={16} aria-hidden className={cn("shrink-0", !active && "text-primary-hover")} />
          <span className="truncate">{entry.projectName ?? entry.name}</span>
        </a>
      </div>
    );
  }

  return (
    <div>
      <div
        style={{ paddingLeft: depth * 12 }}
        className="group flex min-w-0 items-center gap-0.5 rounded text-muted transition-colors hover:text-foreground"
      >
        <button
          type="button"
          disabled={!entry.accessible}
          onClick={() => onToggleExpanded(entry.path)}
          aria-expanded={expanded}
          aria-label={expanded ? `Collapse ${entry.name}` : `Expand ${entry.name}`}
          className={cn(
            "shrink-0 rounded p-0.5 transition-colors",
            "focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary/40",
            entry.accessible ? "hover:text-foreground" : "opacity-40",
          )}
        >
          {expanded ? <ChevronDown size={14} aria-hidden /> : <ChevronRight size={14} aria-hidden />}
        </button>
        <Folder size={16} aria-hidden className="shrink-0 opacity-70" />
        <span className="min-w-0 flex-1 truncate py-2 text-sm font-medium" title={entry.path}>
          {entry.name}
        </span>
        <button
          type="button"
          onClick={() => onRegister(entry.path)}
          aria-label={`Register ${entry.name} as project`}
          title="Register as project"
          className={cn(
            "shrink-0 rounded p-0.5 text-primary opacity-0 transition-opacity",
            "hover:bg-primary/10 focus-visible:opacity-100 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary/40",
            "group-hover:opacity-100",
          )}
        >
          <Plus size={13} aria-hidden />
        </button>
        {actions}
      </div>
      {expanded ? (
        <TreeChildren
          path={entry.path}
          depth={depth + 1}
          onRegister={onRegister}
          onToggleExpanded={onToggleExpanded}
          isExpanded={isExpanded}
          activeProjectId={activeProjectId}
        />
      ) : null}
    </div>
  );
}

interface TreeChildrenProps {
  path: string;
  depth: number;
  onRegister: (path: string) => void;
  onToggleExpanded: (path: string) => void;
  isExpanded: (path: string) => boolean;
  activeProjectId: string | null;
}

function TreeChildren({ path, depth, onRegister, onToggleExpanded, isExpanded, activeProjectId }: TreeChildrenProps) {
  const { data, isPending, error, refetch } = useDirectory(path);

  if (isPending) {
    return (
      <p style={{ paddingLeft: depth * 12 + 21 }} className="py-1 text-xs text-muted">
        Loading…
      </p>
    );
  }

  if (error) {
    return (
      <div style={{ paddingLeft: depth * 12 + 21 }} className="flex items-center gap-1.5 py-1">
        <p className="min-w-0 flex-1 truncate text-xs text-danger">{error.message}</p>
        <button
          type="button"
          onClick={() => refetch()}
          aria-label="Retry loading folder"
          className="shrink-0 rounded p-0.5 text-muted transition-colors hover:text-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary/40"
        >
          <RefreshCw size={12} aria-hidden />
        </button>
      </div>
    );
  }

  if (data.entries.length === 0) {
    return (
      <p style={{ paddingLeft: depth * 12 + 21 }} className="py-1 text-xs text-muted">
        Empty folder
      </p>
    );
  }

  return (
    <div className="flex flex-col gap-0.5">
      {data.entries.map((entry) => (
        <TreeNode
          key={entry.path}
          entry={entry}
          depth={depth}
          onRegister={onRegister}
          onToggleExpanded={onToggleExpanded}
          isExpanded={isExpanded}
          activeProjectId={activeProjectId}
        />
      ))}
      {data.truncated ? (
        <p style={{ paddingLeft: depth * 12 + 21 }} className="py-1 text-xs text-muted">
          …more folders not shown
        </p>
      ) : null}
    </div>
  );
}
