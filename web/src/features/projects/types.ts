export interface ProjectResponse {
  id: string;
  name: string;
  diskPath: string;
  description: string;
  createdAt: string;
}

export interface ProjectFormValues {
  name: string;
  diskPath: string;
  description: string;
}
