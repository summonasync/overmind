"use client";

import { useCallback, useState } from "react";

export interface BoardSearchController {
  query: string;
  draft: string;
  isActive: boolean;
  setDraft: (value: string) => void;
  apply: (value: string) => void;
  clear: () => void;
}

export function useBoardSearch(): BoardSearchController {
  const [draft, setDraft] = useState("");
  const [query, setQuery] = useState("");

  const apply = useCallback((value: string) => {
    setDraft(value);
    setQuery(value.trim());
  }, []);

  const clear = useCallback(() => {
    setDraft("");
    setQuery("");
  }, []);

  return { query, draft, isActive: query !== "", setDraft, apply, clear };
}
