import { useState } from 'react';
import { ArrowUpRight, Bell, Check, Menu, Settings2 } from 'lucide-react';
import {
  PageContainer,
  PageHeader,
  PageTitle,
  PageDescription,
  PageActions,
  Section,
  ResponsiveGrid,
} from '../../components/layout/page';
import { Button } from '../../components/ui/button';
import { Checkbox, Label, RadioGroup, Select, Switch } from '../../components/ui/controls';
import {
  Alert,
  Avatar,
  Badge,
  Card,
  EmptyState,
  Separator,
  Skeleton,
  Spinner,
} from '../../components/ui/feedback';
import {
  AlertDialog,
  Dialog,
  Sheet,
  Tooltip,
  Popover,
  DropdownMenu,
} from '../../components/ui/overlays';
import { Breadcrumb, Tabs } from '../../components/ui/navigation';
import { DemoForm } from './demo-form';
import { DemoTable } from './demo-table';
import { useNotify } from '../../app/providers/notifications';
import {
  formatDateOnly,
  formatInstant,
  formatInteger,
  formatThb,
  formatPercent,
} from '../../lib/utils/format';

const palette = [
  { name: 'Burgundy', token: '--primary', role: 'Identity & primary action' },
  { name: 'Warm gold', token: '--accent', role: 'Restrained accent' },
  { name: 'Canvas', token: '--background', role: 'A calm workspace' },
  { name: 'Surface', token: '--surface', role: 'Content & controls' },
];
export default function Showcase() {
  const notify = useNotify();
  const [checked, setChecked] = useState(false);
  return (
    <>
      <a
        href="#main"
        className="sr-only fixed top-2 left-2 z-50 rounded-md bg-surface px-4 py-3 focus:not-sr-only"
      >
        Skip to content
      </a>
      <header className="border-b border-border bg-surface">
        <div className="mx-auto flex max-w-7xl flex-wrap items-center justify-between gap-4 px-4 py-5 sm:px-6 lg:px-10">
          <div className="border-l-3 border-accent pl-3">
            <p className="text-xl font-semibold tracking-[0.16em] text-primary">SIAMIS</p>
            <p className="text-xs text-muted-foreground">Siam International School</p>
          </div>
          <div className="flex items-center gap-3">
            <Badge>Development only</Badge>
            <Sheet
              trigger={
                <Button icon variant="outline" aria-label="Open showcase navigation">
                  <Menu className="size-5" />
                </Button>
              }
              title="Foundation preview"
              description="Development navigation only. The application shell is planned for F2."
            >
              <nav aria-label="Showcase sections" className="flex flex-col gap-3">
                {['identity', 'controls', 'forms', 'tables', 'feedback'].map((item) => (
                  <a
                    key={item}
                    className="min-h-11 rounded-md px-3 py-2 capitalize hover:bg-muted"
                    href={`#${item}`}
                  >
                    {item}
                  </a>
                ))}
              </nav>
            </Sheet>
          </div>
        </div>
      </header>
      <PageContainer>
        <Breadcrumb items={[{ label: 'Development' }, { label: 'Design system' }]} />
        <PageHeader>
          <div>
            <p className="mb-3 text-xs font-semibold tracking-widest text-accent uppercase">
              SIAMIS / Interface foundation
            </p>
            <PageTitle>A considered workspace.</PageTitle>
            <PageDescription>
              A calm, readable foundation for school operations. Review the shared language before
              building feature workflows.
            </PageDescription>
          </div>
          <PageActions>
            <Button
              variant="outline"
              onClick={() => notify('F1 preview only. No API requests are made.')}
            >
              <Bell className="size-4" aria-hidden="true" />
              Preview notification
            </Button>
          </PageActions>
        </PageHeader>
        <div className="mb-10 flex flex-wrap gap-x-6 gap-y-1 border-y border-border py-3 text-xs text-muted-foreground">
          <span>F1 · Design system</span>
          <span>Light theme</span>
          <span>Static sample data</span>
          <span className="sm:ml-auto">No HR workflow or database changes</span>
        </div>
        <Section id="identity" title="01 / Identity & type">
          <ResponsiveGrid className="grid grid-cols-1 gap-5 sm:grid-cols-2 xl:grid-cols-4">
            {palette.map((color) => (
              <Card key={color.name} className="p-0 sm:p-0">
                <div
                  className="h-20 rounded-t-md border-b border-border"
                  style={{ background: `var(${color.token})` }}
                />
                <div className="p-5">
                  <h3 className="font-semibold">{color.name}</h3>
                  <p className="text-sm text-muted-foreground">{color.role}</p>
                  <code className="mt-3 block text-xs text-muted-foreground">{color.token}</code>
                </div>
              </Card>
            ))}
          </ResponsiveGrid>
          <Card>
            <div className="grid gap-6 md:grid-cols-2">
              <div>
                <h3 className="text-xl font-semibold">Clarity, across every detail.</h3>
                <p className="mt-3 text-sm text-muted-foreground">
                  Professional sans-serif, compact headings and space to read. Local system fonts
                  support English and Thai without external font requests.
                </p>
              </div>
              <div className="space-y-2">
                <p lang="th" className="text-lg">
                  ระบบบริหารจัดการโรงเรียน
                </p>
                <p className="text-sm">School operations · 0123456789</p>
                <p className="text-xs text-muted-foreground">
                  Helper text / labels / tabular information
                </p>
              </div>
            </div>
          </Card>
        </Section>
        <Section id="controls" title="02 / Actions & controls">
          <Card>
            <h3 className="mb-4 font-semibold">A clear action hierarchy</h3>
            <div className="flex flex-wrap gap-3">
              <Button>
                <Check aria-hidden="true" className="size-4" />
                Primary action
              </Button>
              <Button variant="secondary">Secondary</Button>
              <Button variant="outline">Outline</Button>
              <Button variant="ghost">Ghost</Button>
              <Button variant="destructive">Destructive</Button>
              <Button disabled>Disabled</Button>
              <Button loading>Submitting</Button>
              <Tooltip label="Configure display preferences">
                <Button icon variant="outline" aria-label="Display settings">
                  <Settings2 className="size-4" />
                </Button>
              </Tooltip>
            </div>
            <Separator />
            <div className="grid items-start gap-6 sm:grid-cols-2 lg:grid-cols-3">
              <div className="flex items-center gap-3">
                <Checkbox
                  id="sample-check"
                  checked={checked}
                  onCheckedChange={(value) => setChecked(value === true)}
                />
                <Label htmlFor="sample-check">Select example</Label>
              </div>
              <div className="flex items-center gap-3">
                <Switch id="sample-switch" />
                <Label htmlFor="sample-switch">Notifications</Label>
              </div>
              <Select
                label="Example view"
                options={[
                  { value: 'comfortable', label: 'Comfortable' },
                  { value: 'compact', label: 'Compact' },
                ]}
                defaultValue="comfortable"
              />
              <div className="sm:col-span-2">
                <p id="sample-radio-label" className="mb-2 text-sm font-semibold">
                  Sample preference
                </p>
                <RadioGroup
                  aria-labelledby="sample-radio-label"
                  defaultValue="standard"
                  options={[
                    { value: 'standard', label: 'Standard' },
                    { value: 'alternate', label: 'Alternate' },
                  ]}
                />
              </div>
            </div>
          </Card>
          <Card>
            <h3 className="mb-4 font-semibold">Status communicates, without deciding policy</h3>
            <div className="flex flex-wrap gap-3">
              <Badge intent="success">Success</Badge>
              <Badge intent="warning">Requires review</Badge>
              <Badge intent="danger">Unable to complete</Badge>
              <Badge intent="info">Information</Badge>
              <Badge>Neutral</Badge>
            </div>
          </Card>
        </Section>
        <Section id="forms" title="03 / Forms & validation">
          <div className="grid items-start gap-5 lg:grid-cols-[2fr_1fr]">
            <Card>
              <DemoForm />
            </Card>
            <Card>
              <h3 className="font-semibold">One consistent pattern</h3>
              <p className="mt-3 text-sm text-muted-foreground">
                Labels, helper text and errors stay attached to their fields. Forms become
                single-column on mobile, and commands expose submitting state.
              </p>
              <Separator />
              <p className="text-xs text-muted-foreground">
                React Hook Form + Zod. This demonstration validates in memory and saves nothing.
              </p>
            </Card>
          </div>
        </Section>
        <Section title="04 / Overlays & focus">
          <Card>
            <div className="flex flex-wrap gap-3">
              <Dialog
                trigger={<Button variant="outline">Open dialog</Button>}
                title="A focused conversation"
                description="Focus stays in this dialog until it closes. Escape returns to the trigger."
              >
                <p className="text-sm text-muted-foreground">
                  A reusable overlay for future workflows. No business action is performed.
                </p>
              </Dialog>
              <Sheet
                trigger={<Button variant="outline">Open drawer</Button>}
                title="Context, without losing your place"
                description="Responsive side panel with keyboard focus containment."
              >
                <p className="text-sm text-muted-foreground">
                  A foundation for future record details.
                </p>
              </Sheet>
              <AlertDialog
                trigger={<Button variant="outline">Confirm example</Button>}
                title="Confirm a demonstration?"
                description="This only shows a notification. No data is changed."
                onConfirm={() => notify('Example confirmation complete. No data was changed.')}
              />
              <Popover
                label="Display guidance"
                trigger={<Button variant="outline">Open popover</Button>}
              >
                <p className="text-sm">
                  Small contextual information should be accessible without moving to another page.
                </p>
              </Popover>
              <DropdownMenu
                items={[{ label: 'Example action', onSelect: () => notify('Sample action only.') }]}
              />
            </div>
          </Card>
        </Section>
        <Section id="tables" title="05 / Data presentation">
          <Card>
            <div className="mb-5 flex items-center gap-3">
              <Avatar name="Sample Workspace" />
              <div>
                <h3 className="font-semibold">Foundation examples</h3>
                <p className="text-xs text-muted-foreground">
                  Static samples · table on desktop, readable cards on mobile
                </p>
              </div>
            </div>
            <DemoTable />
          </Card>
          <ResponsiveGrid>
            <Card>
              <h3 className="mb-4 font-semibold">Loading a section</h3>
              <div role="status" aria-label="Loading table examples" className="space-y-3">
                <Skeleton className="h-8 w-2/3" />
                <Skeleton className="h-10 w-full" />
                <Skeleton className="h-10 w-full" />
              </div>
            </Card>
            <Card>
              <EmptyState title="Nothing to display">
                Empty results remain useful: explain what belongs here and suggest a relevant next
                step.
              </EmptyState>
            </Card>
            <Card>
              <h3 className="mb-3 font-semibold">Display, not calculation</h3>
              <dl className="space-y-2 text-sm">
                <div>
                  <dt className="text-muted-foreground">Date only</dt>
                  <dd>{formatDateOnly('2026-10-05')}</dd>
                </div>
                <div>
                  <dt className="text-muted-foreground">UTC instant in Bangkok</dt>
                  <dd>{formatInstant('2026-10-05T02:30:00Z')}</dd>
                </div>
                <div>
                  <dt className="text-muted-foreground">Sample display formats</dt>
                  <dd>
                    {formatInteger(1250)} · {formatPercent(0.1)} · {formatThb(30000)}
                  </dd>
                </div>
              </dl>
            </Card>
          </ResponsiveGrid>
        </Section>
        <Section id="feedback" title="06 / Feedback & recovery">
          <ResponsiveGrid>
            <Alert intent="success" title="Success">
              A completed action should give clear feedback.
            </Alert>
            <Alert intent="warning" title="Requires review">
              Review information before continuing. A visual intent does not imply a business
              violation.
            </Alert>
            <Alert intent="danger" title="Unable to reach the service">
              Check your connection and retry when you are ready.
            </Alert>
          </ResponsiveGrid>
          <Card>
            <Tabs
              tabs={[
                {
                  value: 'access',
                  label: 'Access states',
                  content: (
                    <div className="grid gap-4 md:grid-cols-2">
                      <Alert title="Sign in required">
                        An expired session requires authentication.
                      </Alert>
                      <Alert intent="warning" title="Access unavailable">
                        Ask an authorized administrator if you need this capability.
                      </Alert>
                    </div>
                  ),
                },
                {
                  value: 'recovery',
                  label: 'Recovery states',
                  content: (
                    <div className="grid gap-4 md:grid-cols-2">
                      <Alert intent="warning" title="Item changed">
                        Refresh and review the current version before trying again.
                      </Alert>
                      <Alert title="Item not found">
                        Return to the list to choose another item.
                      </Alert>
                    </div>
                  ),
                },
              ]}
            />
            <div className="mt-5">
              <Spinner label="Loading an example" />
            </div>
          </Card>
        </Section>
        <footer className="flex flex-wrap items-center justify-between gap-3 border-t border-border pt-5 text-xs text-muted-foreground">
          <span>SIAMIS · Design system foundation</span>
          <a
            href="#identity"
            className="inline-flex min-h-11 items-center gap-2 underline-offset-4 hover:underline"
          >
            Back to identity
            <ArrowUpRight className="size-3" aria-hidden="true" />
          </a>
        </footer>
      </PageContainer>
    </>
  );
}
