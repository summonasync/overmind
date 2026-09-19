"use client";

import { Clock, Folder } from "lucide-react";
import { InfoPanelSection, InfoPanelStat } from "@/widgets/app-shell";
import { useProjectList } from "@/features/projects";

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
      <InfoPanelSection title="Project Overview" icon={Folder}>
        <div className="grid grid-cols-2 gap-2">
          <InfoPanelStat value={projects.length} label="Total Projects" />
          <InfoPanelStat
            value={projects.filter((project) => project.description.trim().length > 0).length}
            label="With Description"
          />
        </div>
      </InfoPanelSection>

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
                <span className="truncate text-xs text-foreground">{project.name}</span>
                <span className="shrink-0 font-mono text-[10px] text-muted">
                  {new Date(project.createdAt).toLocaleDateString()}
                </span>
              </li>
            ))}
          </ul>
        )}
      </InfoPanelSection>
    </div>
  );
}
