"use client";

import { Select } from "@/shared/ui/select";
import { useProjectList } from "@/features/projects";

interface AgentProjectSelectProps {
  value: string;
  onChange: (value: string) => void;
}

export function AgentProjectSelect({ value, onChange }: AgentProjectSelectProps) {
  const { data, loading } = useProjectList();

  return (
    <div className="flex items-center justify-end gap-2">
      <label htmlFor="agent-project-context" className="text-xs text-muted">
        Project context
      </label>
      <Select
        id="agent-project-context"
        value={value}
        onChange={(event) => onChange(event.target.value)}
        className="w-60"
        disabled={loading}
      >
        <option value="">All projects</option>
        {(data ?? []).map((project) => (
          <option key={project.id} value={project.id}>
            {project.name}
          </option>
        ))}
      </Select>
    </div>
  );
}
