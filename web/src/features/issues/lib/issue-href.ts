export function issueHref(projectId: string, issueId: string): string {
  return `/issues?project=${encodeURIComponent(projectId)}&issue=${encodeURIComponent(issueId)}`;
}
