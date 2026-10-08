import { describe, expect, it, vi } from 'vitest';
import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { Notifications } from '../app/providers/notifications';
import { Providers } from '../app/providers/providers';
import { Button } from '../components/ui/button';
import { Dialog, AlertDialog } from '../components/ui/overlays';
import {
  MetricStrip,
  QueueList,
  SystemState,
  TableViewport,
  WorkspaceHeader,
} from '../components/shared/workspace';
import { DemoTable } from '../features/design-system/demo-table';
import Showcase from '../features/design-system/showcase';

describe('V2 platform foundation', () => {
  it('renders a semantic workspace title and context without inventing actions', () => {
    render(
      <WorkspaceHeader
        title="Example workspace"
        context="School / operations"
        description="Contextual description"
      />,
    );
    expect(screen.getByRole('heading', { level: 1 })).toHaveTextContent('Example workspace');
    expect(screen.queryByRole('button')).not.toBeInTheDocument();
  });
  it('keeps unavailable metrics explicit without substituting zero', () => {
    render(<MetricStrip items={[{ label: 'Records', value: '—', context: 'Not connected' }]} />);
    expect(screen.getByText('—')).toBeInTheDocument();
    expect(screen.getByText('Not connected')).toBeInTheDocument();
    expect(screen.queryByText('0')).not.toBeInTheDocument();
  });
  it.each(['empty', 'search', 'filtered', 'configuration', 'disconnected', 'permission'] as const)(
    'keeps %s separate from an error',
    (kind) => {
      render(
        <SystemState kind={kind} title={`${kind} specimen`}>
          Meaningful guidance
        </SystemState>,
      );
      expect(screen.getByRole('heading')).toHaveTextContent(`${kind} specimen`);
      expect(screen.queryByRole('alert')).not.toBeInTheDocument();
    },
  );
  it('announces errors with guidance while preserving a retry action', () => {
    render(
      <SystemState kind="error" title="Service unavailable" action={<Button>Retry</Button>}>
        Keep context.
      </SystemState>,
    );
    expect(screen.getByRole('alert')).toHaveTextContent('Keep context.');
    expect(screen.getByRole('button', { name: 'Retry' })).toBeEnabled();
  });
  it('provides a named keyboard-reachable table scrolling region', () => {
    render(
      <TableViewport label="Example records">
        <table>
          <caption>Specimens</caption>
          <tbody>
            <tr>
              <td>Example</td>
            </tr>
          </tbody>
        </table>
      </TableViewport>,
    );
    expect(screen.getByRole('region', { name: 'Example records' })).toHaveAttribute(
      'tabindex',
      '0',
    );
    expect(screen.getByRole('table')).toHaveAccessibleName('Specimens');
  });
  it('does not imply a queue item is clickable without an action', () => {
    render(
      <QueueList
        label="Example queue"
        items={[{ id: '1', title: 'Static specimen', metadata: 'Context' }]}
      />,
    );
    expect(screen.getByRole('list')).toHaveAccessibleName('Example queue');
    expect(screen.queryByRole('button')).not.toBeInTheDocument();
  });
  it('filters and resets the local directory and announces no matches', async () => {
    render(
      <Notifications>
        <DemoTable />
      </Notifications>,
    );
    await userEvent.type(screen.getByLabelText('Search specimens'), 'no matching specimen');
    expect(screen.getByRole('heading', { name: 'No matching specimens' })).toBeInTheDocument();
    await userEvent.click(screen.getByRole('button', { name: /Clear filters/ }));
    expect(screen.getByRole('table')).toBeInTheDocument();
    expect(screen.getByLabelText('Search specimens')).toHaveValue('');
  });
  it('supports semantic sorting, local selection and pagination', async () => {
    render(
      <Notifications>
        <DemoTable />
      </Notifications>,
    );
    const sort = screen.getByRole('button', { name: 'Specimen' });
    expect(sort.closest('th')).toHaveAttribute('aria-sort', 'ascending');
    await userEvent.click(screen.getByRole('checkbox', { name: 'Select Calendar specimen' }));
    expect(screen.getByText('Calendar specimen').closest('tr')).toHaveAttribute(
      'aria-selected',
      'true',
    );
    await userEvent.click(sort);
    expect(sort.closest('th')).toHaveAttribute('aria-sort', 'descending');
    await userEvent.click(screen.getByRole('button', { name: 'Next' }));
    expect(screen.getByText('Page 2 of 2')).toBeInTheDocument();
  });
  it('preserves dialog focus containment and returns to its trigger', async () => {
    render(
      <Dialog
        trigger={<Button>Open example</Button>}
        title="Example dialog"
        description="Focus test"
      >
        <Button>Inside action</Button>
      </Dialog>,
    );
    const trigger = screen.getByRole('button', { name: 'Open example' });
    await userEvent.click(trigger);
    const dialog = screen.getByRole('dialog', { name: 'Example dialog' });
    expect(dialog).toContainElement(document.activeElement as HTMLElement);
    await userEvent.keyboard('{Escape}');
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
    expect(trigger).toHaveFocus();
  });
  it('cancels a destructive confirmation without invoking its action', async () => {
    const confirm = vi.fn();
    render(
      <AlertDialog
        trigger={<Button>Open confirmation</Button>}
        title="Confirm example"
        description="No data changed"
        onConfirm={confirm}
      />,
    );
    await userEvent.click(screen.getByRole('button', { name: 'Open confirmation' }));
    const dialog = screen.getByRole('alertdialog');
    expect(within(dialog).getByRole('button', { name: 'Cancel' })).toHaveFocus();
    await userEvent.click(within(dialog).getByRole('button', { name: 'Cancel' }));
    expect(confirm).not.toHaveBeenCalled();
  });
  it('shows all platform archetypes in a clearly marked development showcase', async () => {
    render(
      <Providers>
        <Showcase />
      </Providers>,
    );
    expect(screen.getByText('V3.1 · Development showcase')).toBeInTheDocument();
    expect(screen.getAllByRole('heading', { level: 1 })).toHaveLength(1);
    expect(screen.getAllByRole('tab')).toHaveLength(10);
    await userEvent.click(screen.getByRole('tab', { name: 'Academic' }));
    expect(
      screen.getByRole('heading', { name: 'Class and subject work in context' }),
    ).toBeInTheDocument();
    expect(screen.getByText(/Synthetic demonstration data only/)).toBeInTheDocument();
  });
});
