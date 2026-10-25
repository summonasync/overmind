import { Select } from "@/shared/ui/select";
import { ISSUE_KINDS } from "@/features/issues/types";
import type { IssueKind } from "@/features/issues/types";

interface IssueKindFilterProps {
  kind: IssueKind | null;
  onChange: (kind: IssueKind | null) => void;
}

export function IssueKindFilter({ kind, onChange }: IssueKindFilterProps) {
  return (
    <label className="flex items-center gap-2 text-xs text-muted">
      Kind
      <Select
        aria-label="Filter by kind"
        value={kind ?? ""}
        onChange={(event) =>
          onChange(event.target.value.length > 0 ? (event.target.value as IssueKind) : null)
        }
      >
        <option value="">All</option>
        {ISSUE_KINDS.map((item) => (
          <option key={item} value={item}>
            {item}
          </option>
        ))}
      </Select>
    </label>
  );
}
