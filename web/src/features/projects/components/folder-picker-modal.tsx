"use client";

import { useState } from "react";
import { ChevronRight, Folder, FolderOpen, HardDrive, House, RefreshCw } from "lucide-react";
import { cn } from "@/shared/lib/cn";
import { Button } from "@/shared/ui/button";
import { ErrorText } from "@/shared/ui/error-text";
import { LoadingState } from "@/shared/ui/loading-state";
import { Modal } from "@/shared/ui/modal";
import { useBrowseRoots, useDirectory } from "@/features/projects/lib/use-project-tree";
import type { DirectoryEntry } from "@/shared/api/types";

interface FolderPickerModalProps {
  title: string;
  open: boolean;
  onClose: () => void;
  onSelect: (path: string) => void;
}

export function FolderPickerModal({ title, open, onClose, onSelect }: FolderPickerModalProps) {
  return (
    <Modal title={title} open={open} onClose={onClose}>
      <FolderPickerBody onSelect={onSelect} onClose={onClose} />
    </Modal>
  );
}

interface PickerBodyProps {
  onSelect: (path: string) => void;
  onClose: () => void;
}

function FolderPickerBody({ onSelect, onClose }: PickerBodyProps) {
  const [path, setPath] = useState<string | null>(null);
  const roots = useBrowseRoots();
  const directory = useDirectory(path);

  return (
    <div className="flex flex-col gap-3">
      <nav aria-label="Folder path" className="flex flex-wrap items-center">
        <PathCrumbs path={path} onNavigate={setPath} />
      </nav>
      <div className="max-h-72 min-h-32 overflow-y-auto rounded-md border border-border bg-surface p-1">
        {path === null ? (
          <RootsList query={roots} onOpen={setPath} />
        ) : (
          <DirectoryList query={directory} onOpen={setPath} />
        )}
      </div>
      <div className="flex justify-end gap-2">
        <Button variant="secondary" onClick={onClose}>
          Cancel
        </Button>
        <Button
          variant="accent"
          disabled={path === null}
          onClick={() => {
            if (path !== null) {
              onSelect(path);
            }
          }}
        >
          Select current folder
        </Button>
      </div>
    </div>
  );
}

interface PathCrumbsProps {
  path: string | null;
  onNavigate: (path: string | null) => void;
}

function PathCrumbs({ path, onNavigate }: PathCrumbsProps) {
  const crumbs: { label: string; target: string | null }[] = [
    { label: "Roots", target: null },
    ...(path !== null ? splitPath(path).map((segment) => ({ label: segment.label, target: segment.path })) : []),
  ];

  return (
    <>
      {crumbs.map((crumb, index) => (
        <span key={crumb.target ?? "roots"} className="flex min-w-0 items-center">
          {index > 0 ? <ChevronRight size={12} aria-hidden className="shrink-0 text-muted" /> : null}
          {index === crumbs.length - 1 ? (
            <span
              aria-current="location"
              className="min-w-0 truncate px-1 py-0.5 text-sm font-medium text-foreground"
            >
              {crumb.label}
            </span>
          ) : (
            <button
              type="button"
              onClick={() => onNavigate(crumb.target)}
              className="min-w-0 truncate rounded px-1 py-0.5 text-sm text-muted transition-colors hover:bg-surface-raised hover:text-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary/40"
            >
              {crumb.label}
            </button>
          )}
        </span>
      ))}
    </>
  );
}

function splitPath(path: string): { label: string; path: string }[] {
  const segments: { label: string; path: string }[] = [];
  let current = "";
  for (const part of path.split(/[\\/]+/).filter((part) => part.length > 0)) {
    current = current === "" ? (path.startsWith("/") ? `/${part}` : part) : `${current}/${part}`;
    segments.push({ label: part, path: current });
  }
  return segments;
}

interface RootsListProps {
  query: ReturnType<typeof useBrowseRoots>;
  onOpen: (path: string) => void;
}

function RootsList({ query, onOpen }: RootsListProps) {
  if (query.isPending) {
    return <LoadingState label="Loading folders…" />;
  }

  if (query.error) {
    return (
      <div className="space-y-2 p-2">
        <ErrorText error={query.error} />
        <Button variant="secondary" onClick={() => query.refetch()}>
          <RefreshCw size={14} aria-hidden />
          Retry
        </Button>
      </div>
    );
  }

  if (query.data.roots.length === 0) {
    return <p className="p-3 text-sm text-muted">No folders available to browse.</p>;
  }

  return (
    <div className="flex flex-col">
      {query.data.roots.map((root) => (
        <button
          key={root.path}
          type="button"
          onClick={() => onOpen(root.path)}
          title={root.path}
          className="flex items-center gap-2 rounded px-2 py-2 text-left transition-colors hover:bg-surface-raised focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary/40"
        >
          <RootIcon kind={root.kind} />
          <span className="flex min-w-0 flex-1 flex-col">
            <span className="truncate text-sm font-medium">{root.name}</span>
            <span className="truncate text-xs text-muted">{root.path}</span>
          </span>
        </button>
      ))}
    </div>
  );
}

function RootIcon({ kind }: { kind: string }) {
  if (kind === "home") {
    return <House size={16} aria-hidden className="shrink-0 opacity-70" />;
  }
  if (kind === "drive" || kind === "volume") {
    return <HardDrive size={16} aria-hidden className="shrink-0 opacity-70" />;
  }
  return <FolderOpen size={16} aria-hidden className="shrink-0 opacity-70" />;
}

interface DirectoryListProps {
  query: ReturnType<typeof useDirectory>;
  onOpen: (path: string) => void;
}

function DirectoryList({ query, onOpen }: DirectoryListProps) {
  if (query.isPending) {
    return <LoadingState label="Loading folder…" />;
  }

  if (query.error) {
    return (
      <div className="space-y-2 p-2">
        <ErrorText error={query.error} />
        <Button variant="secondary" onClick={() => query.refetch()}>
          <RefreshCw size={14} aria-hidden />
          Retry
        </Button>
      </div>
    );
  }

  const { accessible, entries, truncated } = query.data;

  return (
    <div className="flex flex-col">
      {!accessible ? (
        <p className="px-2 py-1.5 text-xs text-muted">Folder content could not be read completely.</p>
      ) : null}
      {entries.length === 0 ? (
        <p className="px-2 py-2 text-sm text-muted">Empty folder</p>
      ) : (
        entries.map((entry) => <EntryRow key={entry.path} entry={entry} onOpen={onOpen} />)
      )}
      {truncated ? <p className="px-2 py-1.5 text-xs text-muted">…more folders not shown</p> : null}
    </div>
  );
}

function EntryRow({ entry, onOpen }: { entry: DirectoryEntry; onOpen: (path: string) => void }) {
  return (
    <button
      type="button"
      disabled={!entry.accessible}
      onClick={() => onOpen(entry.path)}
      title={entry.accessible ? entry.path : `${entry.path} (not accessible)`}
      className={cn(
        "flex items-center gap-2 rounded px-2 py-2 text-left transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary/40",
        entry.accessible ? "hover:bg-surface-raised" : "cursor-not-allowed opacity-40",
      )}
    >
      <Folder size={16} aria-hidden className="shrink-0 opacity-70" />
      <span className="min-w-0 flex-1 truncate text-sm font-medium">{entry.name}</span>
      {!entry.accessible ? <span className="shrink-0 text-xs text-muted">Unavailable</span> : null}
    </button>
  );
}
