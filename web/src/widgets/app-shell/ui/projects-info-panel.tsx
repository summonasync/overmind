"use client";

import { Clock, Folder } from "lucide-react";
import { InfoPanelCollapsibleSection } from "@/widgets/app-shell/ui/info-panel-collapsible-section";
import { InfoPanelSection } from "@/widgets/app-shell/ui/info-panel-section";
import { InfoPanelStat } from "@/widgets/app-shell/ui/info-panel-stat";
import { useProjectList } from "@/features/projects";
import { formatCalendarDate } from "@/shared/lib/relative-time";

export function ProjectsInfoPanel() {
  const { data: projects, loading } = useProjectList();

  if (loading) {
    return null;
  }

  const recent = [...projects]
    .sort((a, b) => b.createdAt.localeCompare(a.createdAt))
    .slice(0, 5);

  return (
    <div className="space-y-6">
      <InfoPanelCollapsibleSection
        page="projects"
        section="stats"
        title="Project Overview"
        icon={Folder}
      >
        <div className="grid grid-cols-2 gap-2">
          <InfoPanelStat value={projects.length} label="Total Projects" />
          <InfoPanelStat
            value={projects.filter((project) => project.description.trim().length > 0).length}
            label="With Description"
          />
        </div>
      </InfoPanelCollapsibleSection>

      <InfoPanelSection title="Recent Projects" icon={Clock}>
        {recent.length === 0 ? (
          <p className="text-xs text-muted">No projects yet.</p>
        ) : (
          <ul className="space-y-1">
            {recent.map((project) => (
              <li
                key={project.id}
                className="flex items-center justify-between gap-2 rounded-md bg-surface px-2.5 py-1.5"
              >
                <a
                  href={`/dashboard?project=${project.id}`}
                  title="Open project dashboard"
                  className="truncate text-xs text-foreground transition-colors hover:text-primary hover:underline focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary/40"
                >
                  {project.name}
                </a>
                <span className="shrink-0 font-mono text-[10px] text-muted">
                  {formatCalendarDate(project.createdAt)}
                </span>
              </li>
            ))}
          </ul>
        )}
      </InfoPanelSection>
    </div>
  );
}
