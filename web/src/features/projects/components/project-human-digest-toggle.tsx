"use client";

import { useProjectHumanDigestToggle } from "@/features/projects/lib/use-projects";
import type { ProjectResponse } from "@/features/projects/types";
import { ErrorText } from "@/shared/ui/error-text";

interface ProjectHumanDigestToggleProps {
  project: ProjectResponse;
}

export function ProjectHumanDigestToggle({ project }: ProjectHumanDigestToggleProps) {
  const toggle = useProjectHumanDigestToggle();

  return (
    <div className="space-y-2">
      <label className="flex items-start gap-3">
        <input
          type="checkbox"
          checked={project.humanDigestEnabled}
          disabled={toggle.isPending}
          onChange={(event) =>
            toggle.mutate({ id: project.id, enabled: event.target.checked })
          }
          className="mt-1 h-4 w-4"
        />
        <span>
          <span className="block text-sm text-foreground">Daily human digest</span>
          <span className="block text-xs text-muted">
            Bundles this project&apos;s human-inbox backlog into one daily notification. The
            global digest switch in settings must also be on.
          </span>
        </span>
      </label>
      <ErrorText error={toggle.error as Error | null} />
    </div>
  );
}
