import { useState } from 'react';
import { describe, expect, it } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { FilterSelect, Input } from '../components/ui/controls';
import { FormField } from '../components/shared/form-field';
import { FilterToolbar } from '../components/shared/workspace';
import { Badge } from '../components/ui/feedback';
import { Button } from '../components/ui/button';
import { activeRoute } from '../app/router/navigation';

function FilterFixture() {
  const [value, setValue] = useState('');
  return (
    <FormField id="filter" label="Department" hint="Includes inactive departments">
      {(props) => (
        <FilterSelect
          {...props}
          label="Department"
          value={value}
          onChange={setValue}
          options={[
            { value: '', label: 'All departments' },
            { value: 'teaching', label: 'Teaching' },
          ]}
        />
      )}
    </FormField>
  );
}
describe('V3.7 shared interaction contracts', () => {
  it('preserves empty filter values, labels, descriptions and option ordering', async () => {
    render(<FilterFixture />);
    const user = userEvent.setup();
    const trigger = screen.getByRole('combobox', { name: 'Department' });
    expect(trigger).toHaveAccessibleDescription('Includes inactive departments');
    await user.click(trigger);
    expect(screen.getAllByRole('option').map((option) => option.textContent)).toEqual([
      'All departments',
      'Teaching',
    ]);
    await user.click(screen.getByRole('option', { name: 'Teaching' }));
    expect(trigger).toHaveTextContent('Teaching');
    await user.click(trigger);
    await user.click(screen.getByRole('option', { name: 'All departments' }));
    expect(trigger).toHaveTextContent('All departments');
  });
  it('dismisses the menu with Escape and returns focus without changing the value', async () => {
    render(<FilterFixture />);
    const user = userEvent.setup();
    const trigger = screen.getByRole('combobox', { name: 'Department' });
    await user.tab();
    await user.keyboard('{ArrowDown}');
    expect(screen.getByRole('listbox')).toBeVisible();
    await user.keyboard('{Escape}');
    expect(screen.queryByRole('listbox')).not.toBeInTheDocument();
    expect(trigger).toHaveFocus();
    expect(trigger).toHaveTextContent('All departments');
  });
  it('keeps form submission and loading action semantics in the shared toolbar', () => {
    render(
      <FilterToolbar as="form" aria-label="Filters">
        <FormField id="search" label="Search" hint="Optional help">
          {(props) => <Input {...props} />}
        </FormField>
        <div className="ui-filter-action">
          <Button type="submit" loading>
            Refresh
          </Button>
        </div>
      </FilterToolbar>,
    );
    expect(screen.getByRole('form', { name: 'Filters' })).toHaveClass('ui-filter-toolbar');
    expect(screen.getByRole('button', { name: 'Refresh' })).toBeDisabled();
    expect(screen.getByRole('button', { name: 'Refresh' })).toHaveAttribute('aria-busy', 'true');
  });
  it('uses explicit semantic intent with visible status text', () => {
    render(
      <>
        <Badge intent="warning">Sources changed</Badge>
        <Badge intent="info">Finalized</Badge>
        <Badge intent="success">Active</Badge>
        <Badge>Inactive</Badge>
      </>,
    );
    expect(screen.getByText('Sources changed')).toHaveAttribute('data-intent', 'warning');
    expect(screen.getByText('Finalized')).toHaveAttribute('data-intent', 'info');
    expect(screen.getByText('Active')).toHaveAttribute('data-intent', 'success');
    expect(screen.getByText('Inactive')).toHaveAttribute('data-intent', 'neutral');
  });
  it('separates concise attendance navigation from its page heading and capability', () => {
    const route = activeRoute('/hr/attendance');
    expect(route?.label).toBe('Attendance');
    expect(route?.title).toBe('Attendance Management');
    expect(route?.capability).toBe('Attendance.Read');
  });
});
