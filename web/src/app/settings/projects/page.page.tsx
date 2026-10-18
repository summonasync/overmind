import { ProjectsSettingsPage } from "@/pages/projects-settings-page";
import { SettingsHub } from "@/pages/settings-page";

export default function SettingsProjectsRoute() {
  return (
    <SettingsHub>
      <ProjectsSettingsPage />
    </SettingsHub>
  );
}
