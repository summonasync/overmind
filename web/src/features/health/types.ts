export interface HealthResponse {
  status: string;
  vectorSearch: "up" | "down";
  version: string;
}
