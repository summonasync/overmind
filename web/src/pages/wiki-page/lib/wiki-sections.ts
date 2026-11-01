export interface WikiSectionMeta {
  id: string;
  title: string;
}

export const WIKI_SECTIONS: readonly WikiSectionMeta[] = [
  { id: "projects-and-numbering", title: "Projects & numbering" },
  { id: "planning-ceremony", title: "Planning ceremony" },
  { id: "lanes-and-claims", title: "Lanes & claims" },
  { id: "wave-protocol", title: "Wave protocol" },
  { id: "attention", title: "Attention" },
  { id: "tombstones-and-board-states", title: "Tombstones & board states" },
  { id: "system-health", title: "System health" },
  { id: "cli-and-mcp-pointers", title: "CLI & MCP pointers" },
];
