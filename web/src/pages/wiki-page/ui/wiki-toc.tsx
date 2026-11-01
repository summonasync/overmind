import { WIKI_SECTIONS } from "@/pages/wiki-page/lib/wiki-sections";

export function WikiToc() {
  return (
    <nav aria-label="Wiki sections">
      <p className="mb-2 text-xs font-semibold tracking-wide text-muted uppercase">On this page</p>
      <ul className="space-y-1">
        {WIKI_SECTIONS.map((section) => (
          <li key={section.id}>
            <a
              href={`#${section.id}`}
              className="block rounded px-2 py-1 text-xs text-muted transition-colors hover:bg-surface-raised hover:text-foreground focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-primary"
            >
              {section.title}
            </a>
          </li>
        ))}
      </ul>
    </nav>
  );
}
