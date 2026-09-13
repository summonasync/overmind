export function taskHeading(body: string, order: number): string {
  const heading = /^#\s+(.+)$/m.exec(body);
  return heading !== null ? heading[1].trim() : `Task ${order + 1}`;
}
