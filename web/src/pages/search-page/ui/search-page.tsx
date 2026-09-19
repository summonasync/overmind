"use client";

import { useState, type FormEvent } from "react";
import { Search as SearchIcon } from "lucide-react";
import { Badge } from "@/shared/ui/badge";
import { Button } from "@/shared/ui/button";
import { EmptyState } from "@/shared/ui/empty-state";
import { ErrorText } from "@/shared/ui/error-text";
import { Input } from "@/shared/ui/input";
import { LoadingState } from "@/shared/ui/loading-state";
import { PageHeader } from "@/shared/ui/page-header";
import { Select } from "@/shared/ui/select";
import { useInfoPanel } from "@/widgets/app-shell";
import { useHealth } from "@/features/health";
import { useProjectList } from "@/features/projects";
import { useSearch } from "@/features/search";
import { ENTRY_TYPE_META } from "@/shared/lib/entry-type";
import type { SearchMode } from "@/features/search";
import { SearchInfoPanel } from "@/pages/search-page/ui/search-info-panel";

export function SearchPage() {
  const [input, setInput] = useState("");
  const [submitted, setSubmitted] = useState("");
  const [mode, setMode] = useState<SearchMode>("fulltext");
  const [projectId, setProjectId] = useState("");

  const { vectorSearchDown } = useHealth();
  const { data: projects } = useProjectList();
  const { data, loading, error } = useSearch(submitted, mode, projectId);

  useInfoPanel(<SearchInfoPanel />);

  const semanticDisabled = vectorSearchDown;

  const handleSubmit = (event: FormEvent) => {
    event.preventDefault();
    setSubmitted(input);
  };

  return (
    <div className="space-y-4">
      <PageHeader title="Search" description="Find knowledge across your projects, documentation, and decisions." />

      <form onSubmit={handleSubmit} className="flex flex-wrap items-center gap-2">
        <Input
          value={input}
          onChange={(event) => setInput(event.target.value)}
          placeholder="Search entries…"
          className="max-w-md"
          aria-label="Search query"
        />
        <Select
          aria-label="Search mode"
          value={mode}
          onChange={(event) => setMode(event.target.value as SearchMode)}
          disabled={semanticDisabled && mode === "semantic"}
        >
          <option value="fulltext">Full-text</option>
          <option value="semantic" disabled={semanticDisabled}>
            Semantic
          </option>
        </Select>
        <Select aria-label="Project scope" value={projectId} onChange={(event) => setProjectId(event.target.value)}>
          <option value="">All projects</option>
          {projects.map((project) => (
            <option key={project.id} value={project.id}>
              {project.name}
            </option>
          ))}
        </Select>
        <Button type="submit" disabled={!input.trim() || loading}>
          <SearchIcon size={14} aria-hidden />
          {loading ? "Searching…" : "Search"}
        </Button>
      </form>

      {semanticDisabled ? (
        <p className="text-xs text-warning" title="The vector store (Qdrant) is not reachable, so semantic search is disabled.">
          Semantic search is disabled — the vector store is unreachable. Full-text search still works.
        </p>
      ) : null}

      {error ? (
        vectorSearchDown && mode === "semantic" ? (
          <p className="text-sm text-danger" role="alert">
            Semantic search is unavailable right now — the vector store is down. Switch to full-text mode.
          </p>
        ) : (
          <ErrorText error={new Error("Search failed")} />
        )
      ) : null}

      {loading ? <LoadingState label="Searching…" /> : null}

      {data && !loading ? (
        data.hits.length === 0 ? (
          <EmptyState title="No results" hint={`Nothing found for "${data.query}".`} />
        ) : (
          <>
            <p className="text-xs text-muted">
              {data.hits.length} result{data.hits.length === 1 ? "" : "s"} for &quot;{data.query}&quot;
            </p>
            <ul className="divide-y divide-border overflow-hidden rounded-lg border border-border bg-surface">
              {data.hits.map((hit) => {
                const meta = ENTRY_TYPE_META[hit.type];
                const Icon = meta.icon;
                return (
                  <li key={hit.entryId} className="px-4 py-3 transition-colors hover:bg-surface-raised">
                    <div className="flex items-center gap-2">
                      <Icon size={15} aria-hidden className="shrink-0" style={{ color: `var(--color-type-${hit.type})` }} />
                      <Badge tone={meta.tone}>{hit.type}</Badge>
                      <span className="truncate text-sm font-semibold text-foreground">{hit.title}</span>
                    </div>
                    <p className="mt-1 text-xs text-muted">{hit.snippet}</p>
                  </li>
                );
              })}
            </ul>
          </>
        )
      ) : submitted && !error && !loading ? (
        <p className="text-sm text-muted">No search performed yet.</p>
      ) : null}
    </div>
  );
}
