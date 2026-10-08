import { describe, expect, it, vi } from 'vitest';
import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { Button } from '../components/ui/button';
import { Label, Checkbox, Select } from '../components/ui/controls';
import { Table, TableHead, TableHeader, TableRow } from '../components/ui/table';
import { Dialog, Sheet } from '../components/ui/overlays';
import { ContentFrame, SystemState, WorkspaceHeader } from '../components/shared/workspace';
import { FormField } from '../components/shared/form-field';
import { PremiumWorkspace } from '../features/design-system/premium-workspace';

describe('V3.1 shared foundation', () => {
  it('does not retain a discard prompt after a controlled save closes the form', async () => {
    const specimen = (open: boolean) => (
      <Dialog open={open} dirty title="Controlled form" description="Local specimen">
        <input aria-label="Value" />
      </Dialog>
    );
    const { rerender } = render(specimen(true));
    await userEvent.keyboard('{Escape}');
    expect(screen.getByRole('alert')).toBeInTheDocument();
    rerender(specimen(false));
    rerender(specimen(true));
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
  });
  it.each(['sm', 'md', 'lg'] as const)('exposes the %s modal width contract', (size) => {
    render(
      <Dialog open title="Width specimen" description="Sizing contract" size={size}>
        <p>Local specimen</p>
      </Dialog>,
    );
    expect(screen.getByRole('dialog')).toHaveStyle({ maxWidth: `var(--modal-${size})` });
  });
  it.each(['md', 'lg'] as const)('exposes the %s drawer width contract', (size) => {
    render(
      <Sheet open title="Width specimen" description="Sizing contract" size={size}>
        <p>Local specimen</p>
      </Sheet>,
    );
    expect(screen.getByRole('dialog')).toHaveStyle({ maxWidth: `var(--drawer-${size})` });
  });
  it('retains baseline classes when consumers add styles', () => {
    render(
      <>
        <Label className="text-primary">Label</Label>
        <Checkbox aria-label="Select row" className="ml-2" />
        <Table>
          <TableHeader className="custom-head">
            <TableRow className="custom-row">
              <TableHead className="numeric">Total</TableHead>
            </TableRow>
          </TableHeader>
        </Table>
      </>,
    );
    expect(screen.getByText('Label')).toHaveClass('ui-label', 'text-primary');
    expect(screen.getByRole('checkbox')).toHaveClass('ui-choice', 'ml-2');
    expect(screen.getByRole('row')).toHaveClass('ui-table-row', 'custom-row');
    expect(screen.getByRole('columnheader')).toHaveClass('font-medium', 'numeric');
  });
  it('forwards select validation descriptions to the interactive trigger', () => {
    render(
      <FormField
        id="selection"
        label="Category"
        required
        hint="Choose a category"
        error="A category is required"
      >
        {(props) => (
          <Select {...props} label="Category" options={[{ value: 'school', label: 'School' }]} />
        )}
      </FormField>,
    );
    const trigger = screen.getByRole('combobox');
    expect(trigger).toHaveAttribute('id', 'selection');
    expect(trigger).toHaveAttribute('aria-invalid', 'true');
    expect(trigger).toHaveAttribute('aria-required', 'true');
    expect(trigger).toHaveAccessibleDescription('Choose a category A category is required');
  });
  it('provides one frame/header contract and valid rich feedback content', () => {
    const { container } = render(
      <ContentFrame width="reading">
        <WorkspaceHeader title="School workspace" />
        <SystemState kind="error" title="Unavailable">
          <p>Connection failed.</p>
          <p>Try again.</p>
        </SystemState>
      </ContentFrame>,
    );
    expect(container.firstChild).toHaveAttribute('data-width', 'reading');
    expect(screen.getAllByRole('heading', { level: 1 })).toHaveLength(1);
    expect(container.querySelector('p p')).toBeNull();
  });
  it('protects dirty dismissal in the same dialog and restores trigger focus after discard', async () => {
    const user = userEvent.setup();
    render(
      <Dialog
        trigger={<Button>Open form</Button>}
        title="Edit example"
        description="Local only"
        dirty
        size="lg"
        footer={<Button>Save</Button>}
      >
        <input aria-label="Title" />
      </Dialog>,
    );
    const trigger = screen.getByRole('button', { name: 'Open form' });
    await user.click(trigger);
    expect(screen.getByRole('dialog')).toHaveAttribute('data-size', 'lg');
    await user.keyboard('{Escape}');
    expect(screen.getByRole('button', { name: 'Keep editing' })).toHaveFocus();
    expect(screen.getAllByRole('dialog')).toHaveLength(1);
    await user.click(screen.getByRole('button', { name: 'Keep editing' }));
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'Close' }));
    await user.click(screen.getByRole('button', { name: 'Discard changes' }));
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
    expect(trigger).toHaveFocus();
  });
  it('blocks Escape and close during a pending submission', async () => {
    const change = vi.fn();
    render(
      <Dialog
        open
        pending
        onOpenChange={change}
        title="Saving"
        description="Pending state"
        footer={<Button loading>Saving</Button>}
      >
        <p>Entered information remains visible.</p>
      </Dialog>,
    );
    await userEvent.keyboard('{Escape}');
    expect(screen.getByRole('dialog')).toHaveAttribute('aria-busy', 'true');
    expect(screen.getByRole('button', { name: 'Close' })).toBeDisabled();
    expect(change).not.toHaveBeenCalled();
  });
  it('preserves keyboard containment and Escape dismissal for inspection drawers', async () => {
    const user = userEvent.setup();
    render(
      <Sheet
        trigger={<Button>Inspect</Button>}
        title="Record"
        description="Context"
        size="md"
        footer={<Button>Action</Button>}
      >
        <p>Record detail</p>
      </Sheet>,
    );
    await user.click(screen.getByRole('button', { name: 'Inspect' }));
    const drawer = screen.getByRole('dialog');
    await user.keyboard('{Tab}{Tab}{Tab}');
    expect(drawer).toContainElement(document.activeElement as HTMLElement);
    await user.keyboard('{Escape}');
    expect(screen.getByRole('button', { name: 'Inspect' })).toHaveFocus();
  });
  it('demonstrates filtering, validation, pending, and inspection without API calls', async () => {
    const user = userEvent.setup();
    const fetchSpy = vi.spyOn(globalThis, 'fetch');
    render(<PremiumWorkspace />);
    await user.click(screen.getByRole('button', { name: 'Inspect Academic calendar review' }));
    expect(screen.getByRole('dialog', { name: 'Request inspection' })).toBeInTheDocument();
    await user.keyboard('{Escape}');
    expect(screen.getByRole('button', { name: 'Inspect Academic calendar review' })).toHaveFocus();
    await user.type(screen.getByRole('searchbox', { name: 'Search requests' }), 'no matches');
    expect(screen.getByRole('heading', { name: 'No requests to show' })).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'Create record' }));
    expect(screen.getByRole('textbox', { name: 'Record name' })).toHaveFocus();
    await user.click(screen.getByRole('button', { name: 'Save demonstration' }));
    expect(screen.getByRole('textbox', { name: 'Record name' })).toHaveAttribute(
      'aria-invalid',
      'true',
    );
    await user.type(screen.getByRole('textbox', { name: 'Record name' }), 'Synthetic request');
    await user.click(screen.getByRole('button', { name: 'Preview pending state' }));
    await user.keyboard('{Escape}');
    expect(screen.getByRole('dialog')).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'Finish loading demonstration' }));
    await user.click(
      within(screen.getByRole('dialog')).getByRole('button', { name: 'Save demonstration' }),
    );
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Create record' })).toHaveFocus();
    expect(screen.getByText('Demonstration completed')).toBeInTheDocument();
    expect(fetchSpy).not.toHaveBeenCalled();
    fetchSpy.mockRestore();
  });
});
