"use client";

import { ChevronDown, ChevronRight, Package, Pencil, Trash2 } from "lucide-react";
import { Badge } from "@/shared/ui/badge";
import { Button } from "@/shared/ui/button";
import { EmptyState } from "@/shared/ui/empty-state";
import { cn } from "@/shared/lib/cn";
import { KitOriginBadge } from "@/features/kits/components/kit-origin-badge";
import type { KitResponse } from "@/features/kits/types";

interface KitListProps {
  kits: KitResponse[];
  expandedKitId: string | null;
  onToggle: (kit: KitResponse) => void;
  onEdit: (kit: KitResponse) => void;
  onDelete: (kit: KitResponse) => void;
}

export function KitList({ kits, expandedKitId, onToggle, onEdit, onDelete }: KitListProps) {
  if (kits.length === 0) {
    return <EmptyState title="No kits yet" hint="Create a kit to bundle entries for agent platforms." />;
  }

  return (
    <ul className="divide-y divide-border overflow-hidden rounded-lg border border-border bg-surface">
      {kits.map((kit) => {
        const expanded = kit.id === expandedKitId;
        return (
          <li key={kit.id} className={cn("transition-colors", expanded ? "bg-primary/5" : "hover:bg-surface-raised")}>
            <div className="flex items-center justify-between gap-4 px-4 py-3">
              <button
                type="button"
                onClick={() => onToggle(kit)}
                className="flex min-w-0 items-center gap-2 text-left focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary/40"
              >
                {expanded ? (
                  <ChevronDown size={15} aria-hidden className="shrink-0 text-muted" />
                ) : (
                  <ChevronRight size={15} aria-hidden className="shrink-0 text-muted" />
                )}
                <Package size={16} aria-hidden className="shrink-0 text-primary" />
                <span className="truncate text-sm font-semibold text-foreground">{kit.name}</span>
                <Badge tone="skill">v{kit.version}</Badge>
                <KitOriginBadge kit={kit} />
                <Badge>{kit.entryIds.length} entries</Badge>
              </button>
              <div className="flex shrink-0 gap-1">
                <Button variant="secondary" onClick={() => onEdit(kit)}>
                  <Pencil size={13} aria-hidden />
                  Edit
                </Button>
                <Button variant="danger" onClick={() => onDelete(kit)}>
                  <Trash2 size={13} aria-hidden />
                  Delete
                </Button>
              </div>
            </div>
            {kit.description ? (
              <p className="px-4 pb-2 text-xs text-muted">{kit.description}</p>
            ) : null}
          </li>
        );
      })}
    </ul>
  );
}
