"use client";

import { useState } from "react";
import {
  ChevronDown,
  ChevronRight,
  Folder,
  FolderOpen,
  Plus,
  RefreshCw,
} from "lucide-react";
import { cn } from "@/shared/lib/cn";
import { useDirectory, useTreeRoots } from "@/features/projects/lib/use-project-tree";
import type { DirectoryEntry, TreeRoot } from "@/shared/api/types";

interface ProjectTreeProps {
  onRegister: (path: string) => void;
}

export function ProjectTree({ onRegister }: ProjectTreeProps) {
  const { data, isPending, error, refetch } = useTreeRoots();

  if (isPending) {
    return <p className="px-3 py-1 text-xs text-muted">Scanning drives…</p>;
  }

  if (error) {
    return (
      <div className="flex items-center gap-2 px-3 py-1">
        <p className="min-w-0 flex-1 truncate text-xs text-danger">{error.message}</p>
        <button
          type="button"
          onClick={() => refetch()}
          aria-label="Retry loading drives"
          className="shrink-0 rounded p-0.5 text-muted transition-colors hover:text-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary/40"
        >
          <RefreshCw size={12} aria-hidden />
        </button>
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
        />
      ))}
    </div>
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
}

function TreeNode({ entry, depth, onRegister }: TreeNodeProps) {
  const [expanded, setExpanded] = useState(false);

  if (entry.projectId !== null) {
    return (
      <div style={{ paddingLeft: depth * 12 }} className="flex items-center">
        <a
          href={`/?project=${entry.projectId}`}
          title={entry.path}
          className={cn(
            "flex min-w-0 flex-1 items-center gap-1.5 rounded px-1 py-1 text-xs font-medium text-primary",
            "transition-colors hover:bg-primary/10 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary/40",
          )}
        >
          <FolderOpen size={14} aria-hidden className="shrink-0" />
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
          onClick={() => setExpanded((value) => !value)}
          aria-expanded={expanded}
          aria-label={expanded ? `Collapse ${entry.name}` : `Expand ${entry.name}`}
          className={cn(
            "shrink-0 rounded p-0.5 transition-colors",
            "focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary/40",
            entry.accessible ? "hover:text-foreground" : "opacity-40",
          )}
        >
          {expanded ? <ChevronDown size={13} aria-hidden /> : <ChevronRight size={13} aria-hidden />}
        </button>
        <Folder size={14} aria-hidden className="shrink-0 opacity-70" />
        <span className="min-w-0 flex-1 truncate py-1 text-xs" title={entry.path}>
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
      </div>
      {expanded ? <TreeChildren path={entry.path} depth={depth + 1} onRegister={onRegister} /> : null}
    </div>
  );
}

interface TreeChildrenProps {
  path: string;
  depth: number;
  onRegister: (path: string) => void;
}

function TreeChildren({ path, depth, onRegister }: TreeChildrenProps) {
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
        <TreeNode key={entry.path} entry={entry} depth={depth} onRegister={onRegister} />
      ))}
      {data.truncated ? (
        <p style={{ paddingLeft: depth * 12 + 21 }} className="py-1 text-xs text-muted">
          …more folders not shown
        </p>
      ) : null}
    </div>
  );
}
