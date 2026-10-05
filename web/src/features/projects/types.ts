export interface ProjectResponse {
  id: string;
  name: string;
  diskPath: string;
  description: string;
  manifest: string | null;
  createdAt: string;
}

export interface ProjectFormValues {
  name: string;
  diskPath: string;
  description: string;
}

export type ProjectUpdateValues = ProjectFormValues & {
  manifest?: string | null;
  setManifest?: boolean;
};

export interface ProjectManifestImportResponse {
  sourceFile: string;
  manifest: string;
}
