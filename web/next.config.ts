import type { NextConfig } from "next";

const nextConfig: NextConfig = {
  output: "export",
  images: { unoptimized: true },
  pageExtensions: ["page.tsx", "page.ts", "layout.tsx", "layout.ts"],
  adapterPath: require.resolve("./build/adapter.js"),
};

export default nextConfig;
