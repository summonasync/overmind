"use client";

import { useProjectAutoActToggle } from "@/features/projects/lib/use-projects";
import type { ProjectResponse } from "@/features/projects/types";
import { ErrorText } from "@/shared/ui/error-text";

interface ProjectAutoActToggleProps {
  project: ProjectResponse;
}

export function ProjectAutoActToggle({ project }: ProjectAutoActToggleProps) {
  const toggle = useProjectAutoActToggle();

  return (
    <div className="space-y-2">
      <label className="flex items-start gap-3">
        <input
          type="checkbox"
          checked={project.autoActOnReplies}
          disabled={toggle.isPending}
          onChange={(event) =>
            toggle.mutate({ id: project.id, enabled: event.target.checked })
          }
          className="mt-1 h-4 w-4"
        />
        <span>
          <span className="block text-sm text-foreground">Auto-act on replies</span>
          <span className="block text-xs text-muted">
            A human reply on an approval notification validates the artifact or closes the
            feature when eligible.
          </span>
        </span>
      </label>
      <ErrorText error={toggle.error as Error | null} />
    </div>
  );
}
