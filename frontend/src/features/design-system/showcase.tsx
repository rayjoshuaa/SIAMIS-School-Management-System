import { useState } from 'react';
import { ArrowRight, Check, Settings2 } from 'lucide-react';
import { Button } from '../../components/ui/button';
import { Checkbox, Input, Label, RadioGroup, Select, Switch } from '../../components/ui/controls';
import { Alert, Badge, Skeleton, Spinner } from '../../components/ui/feedback';
import { AlertDialog, Dialog, Tooltip } from '../../components/ui/overlays';
import { Tabs } from '../../components/ui/navigation';
import { QueueList, RecordSummary, SystemState } from '../../components/shared/workspace';
import { FormField } from '../../components/shared/form-field';
import { DemoForm } from './demo-form';
import { DemoTable } from './demo-table';
import { PremiumWorkspace } from './premium-workspace';
import { useNotify } from '../../app/providers/notifications';
import './showcase.css';

const sections = [
  ['overview', 'Overview'],
  ['language', 'Visual language'],
  ['controls', 'Controls'],
  ['forms', 'Forms'],
  ['tables', 'Tables & queues'],
  ['states', 'Feedback'],
  ['patterns', 'Page patterns'],
] as const;
const archetypes = [
  {
    value: 'dashboard',
    label: 'Dashboard',
    title: 'Overview without decoration',
    description:
      'Factual metrics, attention queues and contextual actions. Use aligned strips and lists before separate metric cards.',
    parts: ['Metrics', 'Attention', 'Operational overview'],
  },
  {
    value: 'directory',
    label: 'Directory',
    title: 'Find and compare records',
    description:
      'Search, visible frequent filters, a table or compact list, and pagination. Students, employees, guardians and applicants share these primitives.',
    parts: ['Search & filters', 'Identity + status + metadata', 'Pagination'],
  },
  {
    value: 'record',
    label: 'Record / profile',
    title: 'Identity with structured context',
    description:
      'Record summary, justified tabs, structured sections, related records and an audit timeline. Place actions beside the context they affect.',
    parts: ['Identity & contextual actions', 'Details / related records', 'History'],
  },
  {
    value: 'queue',
    label: 'Queue',
    title: 'Review and take the next action',
    description:
      'Compact actionable records, meaningful priority and review state. Suitable for admissions, leave, documents and academic exceptions.',
    parts: ['Filters & workload context', 'Review records', 'Next action'],
  },
  {
    value: 'processing',
    label: 'Processing',
    title: 'A controlled processing workspace',
    description:
      'Period/process state, prerequisites, exceptions and explicit progression. Payroll and finance do not inherit a dashboard layout.',
    parts: ['Process / period state', 'Exceptions & validation', 'Progression actions'],
  },
  {
    value: 'documents',
    label: 'Documents',
    title: 'Metadata before decoration',
    description:
      'Categories, document records, version metadata and explicit authorized actions. Never expose confidential actions from inferred roles.',
    parts: ['Categories & filters', 'Document metadata', 'Version / lifecycle actions'],
  },
  {
    value: 'administration',
    label: 'Administration',
    title: 'Dense, precise configuration',
    description:
      'Accounts, capabilities and configuration use clear tables and grouped forms. Permission limitations remain distinct from empty data.',
    parts: ['Configuration context', 'Accounts / permissions', 'Audit information'],
  },
  {
    value: 'calendar',
    label: 'Calendar',
    title: 'Time and resource context',
    description:
      'Date navigation, schedules/resources and conflicts need a calendar or timeline workspace. Do not replace them with a card grid.',
    parts: ['Date & resource controls', 'Schedule workspace', 'Conflicts & detail disclosure'],
  },
  {
    value: 'academic',
    label: 'Academic',
    title: 'Class and subject work in context',
    description:
      'Class/subject selection, dense student assessment controls and validation. Preserve row/column context in grading work.',
    parts: [
      'Class / subject / assessment',
      'Student grading workspace',
      'Validation & publication state',
    ],
  },
  {
    value: 'pipeline',
    label: 'Pipeline / CRM',
    title: 'Stages with next actions',
    description:
      'Stages, applicant records and explicit next actions. Stage lists may scroll locally on mobile; maintain a clear path to record detail.',
    parts: ['Stage context', 'Applicant / entity records', 'Next actions'],
  },
];
export default function Showcase() {
  const notify = useNotify();
  const [checked, setChecked] = useState(false);
  return (
    <div className="v2-showcase">
      <a href="#v2-main" className="v2-skip">
        Skip to content
      </a>
      <header className="v2-masthead">
        <div className="v2-masthead-inner">
          <a href="#overview" className="v2-wordmark">
            SIAMIS <span>Siam International School</span>
          </a>
          <span className="v2-development">V3.1 · Development showcase</span>
        </div>
      </header>
      <div className="v2-layout">
        <aside className="v2-index">
          <p className="v2-eyebrow">Master foundation</p>
          <nav aria-label="Design system sections">
            {sections.map(([id, label], index) => (
              <a href={`#${id}`} key={id}>
                <span aria-hidden="true">0{index + 1}</span>
                {label}
              </a>
            ))}
          </nav>
          <p className="v2-index-note">
            One SIAMIS ecosystem.
            <br />
            Different workflows.
          </p>
        </aside>
        <main id="v2-main" className="v2-main">
          <section id="overview">
            <PremiumWorkspace />
          </section>
          <section id="language" className="v2-section">
            <div className="v2-section-heading">
              <h2>Visual language</h2>
              <p>Warm neutrals. Burgundy interactions. Restrained institutional gold.</p>
            </div>
            <div className="v2-palette">
              {[
                ['brand', 'Burgundy', 'Identity & primary action', '--primary'],
                ['gold', 'School gold', 'Accent, never default action', '--accent'],
                ['canvas', 'Canvas', 'Application background', '--background'],
                ['surface', 'Surface', 'Reading & working area', '--surface'],
                ['muted', 'Grouped surface', 'Secondary context', '--surface-muted'],
              ].map(([kind, name, role, token]) => (
                <div key={kind}>
                  <div className={`v2-swatch v2-swatch-${kind}`} aria-hidden="true" />
                  <h3>{name}</h3>
                  <p>{role}</p>
                  <code>{token}</code>
                </div>
              ))}
            </div>
            <div className="v2-split v2-type-surface">
              <div>
                <p className="v2-eyebrow">Typography specimen</p>
                <p className="v2-page-type">Page title · 24 / 31</p>
                <h3 className="v2-section-type">Section heading · 18 / 27</h3>
                <p>Body & labels · 14 / 21</p>
                <p className="v2-meta-type">Metadata & table headers · 13 / 20</p>
                <p lang="th">ระบบบริหารจัดการโรงเรียน · 0123456789</p>
              </div>
              <div className="v2-surface-example">
                <h3>Grouped context</h3>
                <p>
                  Use a subtle surface for related secondary information. Ordinary content does not
                  need its own shadow.
                </p>
                <div className="v2-spacing" aria-label="Spacing scale">
                  <span>4</span>
                  <span>8</span>
                  <span>12</span>
                  <span>16</span>
                  <span>24</span>
                  <span>32</span>
                  <span>48</span>
                </div>
                <p className="v2-meta-type">
                  4px rhythm · 4px controls · 6px panels · elevation for overlays
                </p>
              </div>
            </div>
          </section>
          <section id="controls" className="v2-section">
            <div className="v2-section-heading">
              <h2>Actions & controls</h2>
              <p>A deliberate hierarchy with visible, distinct interaction states.</p>
            </div>
            <div className="v2-action-samples">
              <Button>
                <Check className="size-4" aria-hidden="true" />
                Primary action
              </Button>
              <Button variant="secondary">Secondary</Button>
              <Button variant="outline">Outline</Button>
              <Button variant="ghost">Quiet action</Button>
              <Button variant="destructive">Destructive</Button>
              <Button disabled>Disabled</Button>
              <Button loading>Submitting</Button>
              <Tooltip label="Display settings">
                <Button icon variant="outline" aria-label="Display settings">
                  <Settings2 className="size-4" aria-hidden="true" />
                </Button>
              </Tooltip>
            </div>
            <div className="v2-controls-grid">
              <div className="flex items-center gap-2">
                <Checkbox
                  id="v2-checkbox"
                  checked={checked}
                  onCheckedChange={(value) => setChecked(value === true)}
                />
                <Label htmlFor="v2-checkbox">Select specimen</Label>
              </div>
              <div className="flex items-center gap-2">
                <Switch id="v2-switch" />
                <Label htmlFor="v2-switch">Enable example</Label>
              </div>
              <div>
                <Label htmlFor="v2-select">Display density</Label>
                <div className="mt-2">
                  <Select
                    id="v2-select"
                    label="Display density"
                    defaultValue="standard"
                    options={[
                      { value: 'standard', label: 'Standard' },
                      { value: 'compact', label: 'Compact' },
                    ]}
                  />
                </div>
              </div>
              <div>
                <p id="v2-radio-label" className="text-sm font-semibold">
                  Sample preference
                </p>
                <RadioGroup
                  aria-labelledby="v2-radio-label"
                  defaultValue="standard"
                  options={[
                    { value: 'standard', label: 'Standard' },
                    { value: 'alternate', label: 'Alternate' },
                  ]}
                />
              </div>
            </div>
            <div className="v2-statuses">
              <span className="v2-meta-type">Semantic meaning</span>
              <Badge>Neutral</Badge>
              <Badge intent="info">Information</Badge>
              <Badge intent="success">Complete</Badge>
              <Badge intent="warning">Requires review</Badge>
              <Badge intent="danger">Failed</Badge>
            </div>
          </section>
          <section id="forms" className="v2-section">
            <div className="v2-section-heading">
              <h2>Forms & validation</h2>
              <p>
                Visible labels, local errors and efficient grouping. Focus is blue; errors are red.
              </p>
            </div>
            <div className="v2-split">
              <div className="v2-primary-surface">
                <DemoForm />
              </div>
              <div className="v2-form-context">
                <FormField
                  id="v2-password"
                  label="Password specimen"
                  hint="Browser autocomplete retained; no account operation."
                >
                  {(props) => <Input {...props} type="password" autoComplete="new-password" />}
                </FormField>
                <FormField id="v2-date" label="Date specimen">
                  {(props) => <Input {...props} type="date" />}
                </FormField>
                <FormField
                  id="v2-invalid"
                  label="Invalid specimen"
                  error="Review this demonstration value."
                >
                  {(props) => <Input {...props} defaultValue="Example" />}
                </FormField>
                <p className="v2-meta-type">
                  Use sections for long forms, dialogs for short contextual work, and full pages for
                  sustained workflows.
                </p>
              </div>
            </div>
          </section>
          <section id="tables" className="v2-section">
            <div className="v2-section-heading">
              <h2>Operational data</h2>
              <p>Identity, status, selection, numeric alignment and restrained actions.</p>
            </div>
            <div className="v2-operational">
              <div className="v2-primary-surface">
                <h3 className="v2-subheading">Directory specimens</h3>
                <DemoTable />
              </div>
              <div className="v2-queue-panel">
                <h3 className="v2-subheading">Review queue</h3>
                <p className="v2-meta-type">Fictional component examples</p>
                <QueueList
                  label="Specimen review queue"
                  items={[
                    {
                      id: 'q1',
                      title: 'Example submission',
                      metadata: 'Component specimen · needs review',
                      status: <Badge intent="warning">Review</Badge>,
                      action: (
                        <Button
                          icon
                          variant="ghost"
                          aria-label="Inspect example submission"
                          onClick={() => notify('Specimen only. No operational action.')}
                        >
                          <ArrowRight className="size-4" aria-hidden="true" />
                        </Button>
                      ),
                    },
                    {
                      id: 'q2',
                      title: 'Example configuration',
                      metadata: 'Component specimen · ready',
                      status: <Badge intent="success">Ready</Badge>,
                    },
                    {
                      id: 'q3',
                      title: 'Example document',
                      metadata: 'Component specimen · draft',
                      status: <Badge>Draft</Badge>,
                    },
                  ]}
                />
                <p className="v2-meta-type mt-4">
                  Only actionable items have an action affordance. Status never invents a business
                  consequence.
                </p>
              </div>
            </div>
          </section>
          <section id="states" className="v2-section">
            <div className="v2-section-heading">
              <h2>Feedback & system states</h2>
              <p>Communicate the cause, then offer the relevant next step.</p>
            </div>
            <div className="v2-feedback-grid">
              <Alert intent="success" title="Example saved">
                A completed action needs concise confirmation.
              </Alert>
              <Alert intent="warning" title="Review required">
                This state does not imply misconduct or a payroll deduction.
              </Alert>
              <Alert intent="danger" title="Example request failed">
                Keep entered information visible; offer retry.
              </Alert>
              <Alert title="Information">Context belongs near the task it informs.</Alert>
            </div>
            <div className="v2-state-grid">
              {(
                [
                  {
                    kind: 'empty',
                    title: 'No records yet',
                    text: 'Explain what belongs here; offer an authorized creation action where appropriate.',
                  },
                  {
                    kind: 'search',
                    title: 'No search results',
                    text: 'Change the search terms. Do not report a connection failure.',
                  },
                  {
                    kind: 'filtered',
                    title: 'No records match these filters',
                    text: 'Clear or adjust filters without discarding context.',
                  },
                  {
                    kind: 'configuration',
                    title: 'Configuration required',
                    text: 'A prerequisite has not been configured. This is not a failed request.',
                  },
                  {
                    kind: 'permission',
                    title: 'Capability required',
                    text: 'Hide inaccessible actions. Backend authorization remains authoritative.',
                  },
                  {
                    kind: 'disconnected',
                    title: 'Not connected',
                    text: 'This feature has no connected data source. Do not display invented values.',
                  },
                  {
                    kind: 'error',
                    title: 'Service unavailable',
                    text: 'The request failed. Retain context and provide a meaningful retry.',
                  },
                ] as const
              ).map((state) => (
                <SystemState key={state.kind} kind={state.kind} title={state.title}>
                  {state.text}
                </SystemState>
              ))}
            </div>
            <div className="v2-loading">
              <div>
                <h3 className="v2-subheading">Section loading</h3>
                <div role="status" aria-label="Loading specimen records" className="space-y-3">
                  <Skeleton className="h-5 w-1/3" />
                  <Skeleton className="h-9 w-full" />
                  <Skeleton className="h-9 w-4/5" />
                </div>
              </div>
              <div>
                <h3 className="v2-subheading">Inline progress</h3>
                <Spinner label="Preparing example" />
                <p className="v2-meta-type mt-3">
                  Match skeletons to expected content. Avoid page spinners for small updates.
                  Reduced motion is respected.
                </p>
              </div>
            </div>
            <div className="v2-action-samples">
              <Dialog
                trigger={<Button variant="outline">Open specimen dialog</Button>}
                title="Display preferences"
                description="Demonstration only. Focus stays within the dialog and returns to its trigger."
              >
                <FormField id="v2-dialog-name" label="Example label">
                  {(props) => <Input {...props} />}
                </FormField>
              </Dialog>
              <AlertDialog
                trigger={<Button variant="outline">Open confirmation</Button>}
                title="Confirm demonstration"
                description="This demonstrates a destructive confirmation. No data is deleted."
                onConfirm={() => notify('Confirmation specimen only. No data deleted.')}
              />
            </div>
          </section>
          <section id="patterns" className="v2-section">
            <div className="v2-section-heading">
              <h2>Different workflows. One product.</h2>
              <p>
                Archetypes compose the same primitives; they do not prescribe one dashboard layout.
              </p>
            </div>
            <Tabs
              label="Platform page archetypes"
              overflow="scroll"
              tabs={archetypes.map((pattern) => ({
                value: pattern.value,
                label: pattern.label,
                content: (
                  <div className="v2-pattern">
                    <h3>{pattern.title}</h3>
                    <p>{pattern.description}</p>
                    <ol>
                      {pattern.parts.map((part, index) => (
                        <li key={part}>
                          <span>0{index + 1}</span>
                          {part}
                        </li>
                      ))}
                    </ol>
                  </div>
                ),
              }))}
            />
            <RecordSummary
              title="Record identity specimen"
              metadata="EXAMPLE-ONLY · no school entity connected"
              status={<Badge>Draft specimen</Badge>}
              actions={
                <Button
                  variant="outline"
                  onClick={() => notify('Contextual action specimen only.')}
                >
                  Contextual action
                </Button>
              }
            />
            <div className="v2-record-sections">
              <div>
                <h3 className="v2-subheading">Structured details</h3>
                <dl>
                  <dt>Record context</dt>
                  <dd>Provided by the relevant backend</dd>
                  <dt>Related information</dt>
                  <dd>Separate sections where justified</dd>
                </dl>
              </div>
              <div>
                <h3 className="v2-subheading">History foundation</h3>
                <ol className="v2-history">
                  <li>
                    <strong>Historical event specimen</strong>
                    <p>Actor, timestamp and meaningful change; never inferred.</p>
                  </li>
                  <li>
                    <strong>Earlier event specimen</strong>
                    <p>Preserve historical facts and provenance.</p>
                  </li>
                </ol>
              </div>
            </div>
          </section>
          <footer className="v2-footer">
            SIAMIS V3.1 · Foundation · Awaiting product-owner visual review{' '}
            <a href="#overview">Back to top</a>
          </footer>
        </main>
      </div>
    </div>
  );
}
