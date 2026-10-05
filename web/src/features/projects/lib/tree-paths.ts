function normalizeDirPath(path: string): string {
  const trimmed = path.replace(/\/+$/, "");
  return trimmed.length > 0 ? trimmed : "/";
}

function isDirPrefix(dir: string, target: string): boolean {
  return dir === "/" ? target.startsWith("/") : target.startsWith(`${dir}/`);
}

function joinSegments(dir: string, segments: string[]): string {
  const tail = segments.join("/");
  return dir === "/" ? `/${tail}` : `${dir}/${tail}`;
}

export function projectAncestorPaths(rootPaths: readonly string[], projectPath: string): string[] {
  const target = normalizeDirPath(projectPath);
  const root = rootPaths
    .map(normalizeDirPath)
    .filter((candidate) => candidate !== target && isDirPrefix(candidate, target))
    .sort((a, b) => b.length - a.length)[0];

  if (root === undefined) {
    return [];
  }

  const segments = target.slice(root.length).split("/").filter(Boolean);
  const chain = [root];
  for (let i = 1; i < segments.length; i++) {
    chain.push(joinSegments(root, segments.slice(0, i)));
  }
  return chain;
}
