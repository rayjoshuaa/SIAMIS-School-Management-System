import { AppShell, ShellSession } from './app-shell';
export default function ShellEntry() {
  return (
    <ShellSession>
      <AppShell />
    </ShellSession>
  );
}
