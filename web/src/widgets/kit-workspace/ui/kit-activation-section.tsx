"use client";

import { useState } from "react";
import { Button } from "@/shared/ui/button";
import { ErrorText } from "@/shared/ui/error-text";
import { Select } from "@/shared/ui/select";
import { Zerling, ZERLING_SIZES } from "@/shared/ui/zerling";
import type { ProjectResponse } from "@/features/projects";
import { kitActivateErrorCopy, kitDeactivateErrorCopy, useKitMutations } from "@/features/kits";
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
    mutation.mutate({ projectId: linkProjectId, kitId: kit.id }, { onSuccess: setReport });
  };

  // Reframed copy for activation refusals, per arm; the detail panel no longer carries
  // these errors in its generic list.
  const activationError =
    kitActivateErrorCopy(mutations.activate.error) ??
    kitDeactivateErrorCopy(mutations.deactivate.error);

  return (
    <section>
      <h3 className="mb-1 text-xs font-semibold uppercase tracking-wide text-muted">
        Activation
      </h3>
      <p className="mb-2 text-xs leading-relaxed text-muted">
        Optional kits stack: a project can have several optional kits active at once (say dotnet-crew
        and js-crew on a fullstack repo), each materializing side by side into its agent files.
      </p>
      {kit.legacy ? (
        <p className="mb-2 rounded-md border border-warning/40 bg-warning/10 px-3 py-2 text-xs text-warning">
          This kit is legacy — retired by the kit split and no longer refreshed by bundle updates.
          Prefer the per-language kits.
        </p>
      ) : null}
      {report ? (
        <div className="mb-2 space-y-1">
          <p className="flex items-center gap-2 rounded-md border border-success/40 bg-success/10 px-3 py-2 text-xs text-success">
            <Zerling pose="celebrating" size={ZERLING_SIZES.banner} />
            <span>
              Last operation: {report.files.length} file(s) affected
              {report.files.length > 0 ? ` (e.g. ${report.files[0].relativePath})` : ""}.
            </span>
          </p>
          {report.warnings.length > 0 ? (
            <ul className="rounded-md border border-warning/40 bg-warning/10 px-3 py-2 text-xs text-warning">
              {report.warnings.map((warning) => (
                <li key={warning}>{warning}</li>
              ))}
            </ul>
          ) : null}
        </div>
      ) : null}
      <ErrorText error={activationError} />
      <div className="mt-2 flex flex-wrap gap-2">
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
