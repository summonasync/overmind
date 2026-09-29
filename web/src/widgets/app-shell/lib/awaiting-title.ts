export function awaitingTitle(baseTitle: string, count: number): string {
  return count > 0 ? `(${count}) ${baseTitle}` : baseTitle;
}
