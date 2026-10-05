export function manifestIsAbsent(manifest: string | null): boolean {
  return manifest === null || manifest.trim().length === 0;
}

export function manifestImportedLine(sourceFile: string): string {
  return `Imported manifest from ${sourceFile}.`;
}
