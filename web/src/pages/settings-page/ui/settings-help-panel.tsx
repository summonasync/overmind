import { Settings2, Wifi, BookOpen } from "lucide-react";

export function SettingsHelpPanel() {
  return (
    <div className="space-y-6">
      <section>
        <h2 className="mb-3 flex items-center gap-2 text-sm font-semibold text-foreground">
          <Settings2 size={15} aria-hidden className="text-primary" />
          Setup Help
        </h2>
        <ul className="space-y-3 text-xs leading-relaxed text-muted">
          <li>
            The embedding provider is used for semantic search and reindexing. Switching providers
            requires a full reindex.
          </li>
          <li>
            Ollama runs locally — no API key needed. Enter the base URL of your Ollama instance.
          </li>
          <li>
            OpenAI-compatible endpoints need a base URL and an API key. The key is stored locally
            on this machine.
          </li>
        </ul>
      </section>

      <section>
        <h2 className="mb-3 flex items-center gap-2 text-sm font-semibold text-foreground">
          <Wifi size={15} aria-hidden className="text-primary" />
          Local APIs
        </h2>
        <p className="text-xs leading-relaxed text-muted">
          Overmind talks to local services (Ollama, Qdrant) on your machine. If a provider is
          unreachable, the system status chip in the topbar shows Degraded and semantic search is
          disabled automatically.
        </p>
      </section>

      <section>
        <h2 className="mb-3 flex items-center gap-2 text-sm font-semibold text-foreground">
          <BookOpen size={15} aria-hidden className="text-primary" />
          Need Help?
        </h2>
        <p className="text-xs leading-relaxed text-muted">
          Check the project documentation for setup guides and troubleshooting.
        </p>
      </section>
    </div>
  );
}
