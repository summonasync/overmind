"use client";

import { useState } from "react";
import { Button } from "@/shared/ui/button";
import { Select } from "@/shared/ui/select";
import { Zerling, ZERLING_SIZES } from "@/shared/ui/zerling";
import type { ProjectResponse } from "@/features/projects";
import { useKitMutations } from "@/features/kits";
import type { KitResponse, MaterializationReportResponse } from "@/features/kits";

type KitMutations = ReturnType<typeof useKitMutations>;

interface KitActivationSectionProps {
  kit: KitResponse;
  projects: ProjectResponse[];
  mutations: KitMutations;
}

export function KitActivationSection({ kit, projects, mutations }: KitActivationSectionProps) {
  const [linkProjectId, setLinkProjectId] = useState("");
  const [report, setReport] = useState<MaterializationReportResponse | null>(null);

  const handleActivate = (activate: boolean) => {
    if (!linkProjectId) {
      return;
    }
    const mutation = activate ? mutations.activate : mutations.deactivate;
    mutation.mutate(
      { projectId: linkProjectId, kitId: kit.id },
      { onSuccess: setReport },
    );
  };

  return (
    <section>
      <h3 className="mb-1 text-xs font-semibold uppercase tracking-wide text-muted">
        Activation
      </h3>
      {report ? (
        <p className="mb-2 flex items-center gap-2 rounded-md border border-success/40 bg-success/10 px-3 py-2 text-xs text-success">
          <Zerling pose="celebrating" size={ZERLING_SIZES.banner} />
          <span>
            Last operation: {report.files.length} file(s) affected
            {report.files.length > 0 ? ` (e.g. ${report.files[0].relativePath})` : ""}.
          </span>
        </p>
      ) : null}
      <div className="flex flex-wrap gap-2">
        <Select
          aria-label="Project to link"
          value={linkProjectId}
          onChange={(event) => setLinkProjectId(event.target.value)}
        >
          <option value="">Select project…</option>
          {projects.map((project) => (
            <option key={project.id} value={project.id}>
              {project.name}
            </option>
          ))}
        </Select>
        <Button
          variant="accent"
          onClick={() => handleActivate(true)}
          disabled={!linkProjectId || mutations.activate.isPending}
        >
          Activate
        </Button>
        <Button
          variant="secondary"
          onClick={() => handleActivate(false)}
          disabled={!linkProjectId || mutations.deactivate.isPending}
        >
          Deactivate
        </Button>
      </div>
    </section>
  );
}
