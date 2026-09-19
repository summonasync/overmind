import { BookOpen, Settings2, Wifi } from "lucide-react";
import { InfoPanelSection } from "@/widgets/app-shell";

export function SettingsHelpPanel() {
  return (
    <div className="space-y-6">
      <InfoPanelSection title="Setup Help" icon={Settings2}>
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
          <li>
            Planning defaults apply to projects that have not set their own workflow: materialization
            <em> inherit</em> follows the global materialization default, and the validation gate
            follows the global default while a project is <em>unset</em>.
          </li>
        </ul>
      </InfoPanelSection>

      <InfoPanelSection title="Local APIs" icon={Wifi}>
        <p className="text-xs leading-relaxed text-muted">
          Overmind talks to local services (Ollama, Qdrant) on your machine. If a provider is
          unreachable, the system status chip in the topbar shows Degraded and semantic search is
          disabled automatically.
        </p>
      </InfoPanelSection>

      <InfoPanelSection title="Need Help?" icon={BookOpen}>
        <p className="text-xs leading-relaxed text-muted">
          Check the project documentation for setup guides and troubleshooting.
        </p>
      </InfoPanelSection>
    </div>
  );
}
