export function Brand({ collapsed = false }: { collapsed?: boolean }) {
  return (
    <div
      className="min-w-0 border-l-3 border-accent pl-3"
      aria-label="SIAMIS, Siam International School"
    >
      <p aria-hidden="true" className="text-lg font-semibold tracking-[0.13em] text-primary">
        {collapsed ? 'S' : 'SIAMIS'}
      </p>
      {!collapsed && <p className="text-xs text-muted-foreground">Siam International School</p>}
    </div>
  );
}
