"use client";

import { useState } from "react";
import { Badge } from "@/shared/ui/badge";
import { Button } from "@/shared/ui/button";
import { Select } from "@/shared/ui/select";
import { ENTRY_TYPE_META } from "@/shared/lib/entry-type";
import { useEntryList } from "@/features/entries";
import type { EntryResponse } from "@/features/entries";
import type { ProjectResponse } from "@/features/projects";
import { useKitMutations } from "@/features/kits";
import type { KitResponse } from "@/features/kits";

type KitMutations = ReturnType<typeof useKitMutations>;

interface KitEntriesSectionProps {
  kit: KitResponse;
  projects: ProjectResponse[];
  entryMap: Record<string, EntryResponse>;
  mutations: KitMutations;
}

export function KitEntriesSection({ kit, projects, entryMap, mutations }: KitEntriesSectionProps) {
  const [entryProjectId, setEntryProjectId] = useState("");
  const [entryId, setEntryId] = useState("");

  const { data: candidateEntries } = useEntryList(entryProjectId, { type: "", tag: "" });

  const handleAddEntry = () => {
    if (!entryId) {
      return;
    }
    mutations.addEntry.mutate({ kitId: kit.id, entryId });
    setEntryId("");
  };

  return (
    <section>
      <h3 className="mb-1 text-xs font-semibold uppercase tracking-wide text-muted">
        Entries ({kit.entryIds.length})
      </h3>
      {kit.entryIds.length === 0 ? (
        <p className="text-xs text-muted">No entries in this kit.</p>
      ) : (
        <ul className="space-y-1">
          {kit.entryIds.map((id) => {
            const entry = entryMap[id];
            return (
              <li
                key={id}
                className="flex items-center justify-between gap-2 rounded-md bg-surface-raised px-2 py-1"
              >
                <span className="min-w-0 truncate text-xs text-foreground">
                  {entry ? entry.title : id}
                  {entry ? <Badge tone={ENTRY_TYPE_META[entry.type].tone} className="ml-1">{entry.type}</Badge> : null}
                </span>
                <Button
                  variant="ghost"
                  className="px-2 py-0.5 text-xs"
                  onClick={() => mutations.removeEntry.mutate({ kitId: kit.id, entryId: id })}
                >
                  Remove
                </Button>
              </li>
            );
          })}
        </ul>
      )}
      <div className="mt-2 flex flex-wrap gap-2">
        <Select
          aria-label="Project for entry"
          value={entryProjectId}
          onChange={(event) => {
            setEntryProjectId(event.target.value);
            setEntryId("");
          }}
        >
          <option value="">Select project…</option>
          {projects.map((project) => (
            <option key={project.id} value={project.id}>
              {project.name}
            </option>
          ))}
        </Select>
        <Select
          aria-label="Entry to add"
          value={entryId}
          onChange={(event) => setEntryId(event.target.value)}
          disabled={!entryProjectId}
        >
          <option value="">Select entry…</option>
          {candidateEntries.map((entry) => (
            <option key={entry.id} value={entry.id}>
              {entry.title}
            </option>
          ))}
        </Select>
        <Button variant="secondary" onClick={handleAddEntry} disabled={!entryId || mutations.addEntry.isPending}>
          Add entry
        </Button>
      </div>
    </section>
  );
}
