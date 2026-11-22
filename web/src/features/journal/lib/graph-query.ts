import type { GraphViewFilters } from "../types.ts";

// One builder for every graph read the surface makes, and the parameter names are the
// endpoint's own — `GraphApi.ReadGraph` binds `type, project, focus, depth, orphansOnly,
// search, includeDone`, and the drift pin in `web/tests/journal-graph.test.mts` reads that
// signature off disk so a rename on either side goes red here rather than silently widening or
// emptying the field.
//
// An unset filter is absent, not empty. The Host tolerates both, but a URL carrying `type=`
// reads as "something is narrowing this view" to the next reader of the address bar, and
// nothing is.
export function graphQueryParams(filters: GraphViewFilters): URLSearchParams {
  const params = new URLSearchParams();

  if (filters.types.length > 0) {
    params.set("type", filters.types.join(","));
  }

  if (filters.project.length > 0) {
    params.set("project", filters.project);
  }

  if (filters.focus.length > 0) {
    params.set("focus", filters.focus);
    if (filters.depth.length > 0) {
      params.set("depth", filters.depth);
    }
  }

  if (filters.orphansOnly) {
    params.set("orphansOnly", "true");
  }

  if (filters.includeDone) {
    params.set("includeDone", "true");
  }

  if (filters.search.trim().length > 0) {
    params.set("search", filters.search.trim());
  }

  return params;
}

export function graphPath(filters: GraphViewFilters): string {
  const query = graphQueryParams(filters).toString();
  return query.length > 0 ? `/graph?${query}` : "/graph";
}
