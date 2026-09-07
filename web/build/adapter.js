const fs = require("node:fs");
const path = require("node:path");

// https://github.com/vercel/next.js/issues/85374
// On Windows the static export writes segment payloads to nested paths
// (entries/__next.entries/__PAGE__.txt) while the client router requests the
// flat dot-separated form (entries/__next.entries.__PAGE__.txt). Flatten the
// output so the requested URLs resolve.
/** @type {import("next").NextAdapter} */
const adapter = {
  name: "flatten-static-export-rsc-segments",
  async onBuildComplete({ outputs }) {
    for (const file of outputs.staticFiles) {
      const targetPath = flattenSegmentPath(file.filePath);
      if (targetPath) {
        await fs.promises.rename(file.filePath, targetPath);
        await fs.promises.rmdir(path.dirname(file.filePath)).catch(() => {});
      }
    }
  },
};

function flattenSegmentPath(filePath) {
  const parts = filePath.split(path.sep);
  const idx = parts.findIndex((part) => part.startsWith("__next."));
  if (idx === -1 || idx === parts.length - 1) {
    return null;
  }
  return [...parts.slice(0, idx), parts.slice(idx).join(".")].join(path.sep);
}

module.exports = adapter;
