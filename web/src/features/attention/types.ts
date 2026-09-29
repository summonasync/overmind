export interface CeremonyFeature {
  projectId: string;
  projectName: string;
  featureSlug: string;
  number: string | null;
  title: string;
  reasons: string[];
  url: string | null;
}

export interface CeremoniesResponse {
  features: CeremonyFeature[];
}
