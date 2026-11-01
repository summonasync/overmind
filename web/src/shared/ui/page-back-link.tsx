interface PageBackLinkProps {
  href: string;
  label: string;
}

export function PageBackLink({ href, label }: PageBackLinkProps) {
  return (
    <a
      href={href}
      className="inline-block text-xs text-muted underline-offset-2 hover:text-foreground hover:underline focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary/40 focus-visible:ring-offset-2 focus-visible:ring-offset-background"
    >
      ← {label}
    </a>
  );
}
