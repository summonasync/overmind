"use client";

import { useState } from "react";
import { Badge } from "@/shared/ui/badge";
import { Button } from "@/shared/ui/button";
import { Select } from "@/shared/ui/select";
import { useDrift } from "@/features/kits";
import type { ProjectResponse } from "@/features/projects";

interface KitDriftSectionProps {
  projects: ProjectResponse[];
}

export function KitDriftSection({ projects }: KitDriftSectionProps) {
  const [driftProjectId, setDriftProjectId] = useState("");
  const [driftEnabled, setDriftEnabled] = useState(false);

  const { data: drift, loading: driftLoading } = useDrift(driftProjectId, driftEnabled);

  return (
    <section>
      <h3 className="mb-1 text-xs font-semibold uppercase tracking-wide text-muted">Drift</h3>
      <div className="flex flex-wrap gap-2">
        <Select
          aria-label="Project for drift"
          value={driftProjectId}
          onChange={(event) => {
            setDriftProjectId(event.target.value);
            setDriftEnabled(false);
          }}
        >
          <option value="">Select project…</option>
          {projects.map((project) => (
            <option key={project.id} value={project.id}>
              {project.name}
            </option>
          ))}
        </Select>
        <Button
          variant="secondary"
          onClick={() => setDriftEnabled(true)}
          disabled={!driftProjectId || driftLoading}
        >
          {driftLoading ? "Checking…" : "Check drift"}
        </Button>
      </div>
      {driftEnabled && drift ? (
        drift.length === 0 ? (
          <p className="mt-2 text-xs text-success">No drift detected.</p>
        ) : (
          <ul className="mt-2 space-y-1">
            {drift.map((item, index) => (
              <li
                key={`${item.platform}-${item.relativePath}-${index}`}
                className="flex items-center gap-2 rounded-md bg-warning/10 px-2 py-1 text-xs"
              >
                <Badge tone={item.kind === "missing" ? "warning" : "info"}>{item.kind}</Badge>
                <span className="font-mono text-foreground">{item.relativePath}</span>
                <Badge>{item.platform}</Badge>
              </li>
            ))}
          </ul>
        )
      ) : null}
    </section>
  );
}
