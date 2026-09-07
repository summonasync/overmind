import type { EntryType } from "@/shared/api/types";

export type SearchMode = "fulltext" | "semantic";

export interface SearchHitResponse {
  entryId: string;
  title: string;
  type: EntryType;
  snippet: string;
  rank: number;
}

export interface SearchResponse {
  query: string;
  hits: SearchHitResponse[];
}
