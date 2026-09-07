import type { Metadata } from "next";
import type { ReactNode } from "react";
import "@/app/globals.css";
import { Providers } from "@/app/providers";
import { AppShell } from "@/widgets/app-shell/ui/app-shell";

export const metadata: Metadata = {
  title: "Overmind",
  description: "Context and skill management for AI agents",
};

interface RootLayoutProps {
  children: ReactNode;
}

export default function RootLayout({ children }: RootLayoutProps) {
  return (
    <html lang="en">
      <body className="font-sans antialiased">
        <Providers>
          <AppShell>{children}</AppShell>
        </Providers>
      </body>
    </html>
  );
}
