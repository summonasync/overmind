import { Select } from "@/shared/ui/select";
import { IssueKindFilter } from "@/features/issues/components/issue-kind-filter";
import { ISSUE_STATUSES } from "@/features/issues/types";
import type { IssueListFilter, IssueStatus } from "@/features/issues/types";

interface IssueFiltersProps {
  filter: IssueListFilter;
  projects: { id: string; name: string }[];
  onChange: (filter: IssueListFilter) => void;
}

export function IssueFilters({ filter, projects, onChange }: IssueFiltersProps) {
  return (
    <div className="flex flex-wrap items-center gap-3">
      <label className="flex items-center gap-2 text-xs text-muted">
        Status
        <Select
          aria-label="Filter by status"
          value={filter.status ?? ""}
          onChange={(event) =>
            onChange({
              ...filter,
              status: event.target.value.length > 0 ? (event.target.value as IssueStatus) : null,
            })
          }
        >
          <option value="">All</option>
          {ISSUE_STATUSES.map((status) => (
            <option key={status} value={status}>
              {status}
            </option>
          ))}
        </Select>
      </label>
      <IssueKindFilter kind={filter.kind} onChange={(kind) => onChange({ ...filter, kind })} />
      <label className="flex items-center gap-2 text-xs text-muted">
        Author project
        <Select
          aria-label="Filter by author project"
          value={filter.authorProjectId ?? ""}
          onChange={(event) =>
            onChange({ ...filter, authorProjectId: event.target.value || null })
          }
        >
          <option value="">All</option>
          {projects.map((project) => (
            <option key={project.id} value={project.id}>
              {project.name}
            </option>
          ))}
        </Select>
      </label>
    </div>
  );
}
