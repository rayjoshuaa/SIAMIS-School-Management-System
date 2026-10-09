import { describe, expect, it } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { DetailFacts } from '../components/shared/detail-facts';
import { BackLink, Button, LinkButton } from '../components/ui/button';
import { Select } from '../components/ui/controls';
import { FormField } from '../components/shared/form-field';

describe('V3.6 shared record presentation', () => {
  it('keeps select labeling, selected values and validation descriptions on the real trigger', () => {
    render(
      <FormField
        id="arrangement"
        label="Work arrangement"
        required
        error="Choose a supported value"
      >
        {(props) => (
          <Select
            {...props}
            label="Work arrangement"
            value="remote"
            options={[
              { value: 'remote', label: 'Remote work with a deliberately long selection label' },
            ]}
          />
        )}
      </FormField>,
    );
    const trigger = screen.getByRole('combobox', { name: 'Work arrangement' });
    expect(trigger).toHaveClass('ui-select-trigger');
    expect(trigger).toHaveAttribute('aria-invalid', 'true');
    expect(trigger).toHaveAttribute('aria-required', 'true');
    expect(trigger).toHaveAccessibleDescription('Choose a supported value');
    expect(trigger.querySelector('.ui-select-value')).toHaveTextContent('Remote work');
    expect(trigger.querySelector('.ui-select-icon svg')).toHaveAttribute('aria-hidden', 'true');
  });
  it('preserves disabled custom-select semantics and native option selection', async () => {
    render(
      <>
        <Select label="Disabled selection" disabled options={[{ value: 'one', label: 'One' }]} />
        <FormField id="native-selection" label="Native selection">
          {(props) => (
            <select {...props} className="ui-control" defaultValue="one">
              <option value="one">One</option>
              <option value="two">Two</option>
            </select>
          )}
        </FormField>
      </>,
    );
    expect(screen.getByRole('combobox', { name: 'Disabled selection' })).toBeDisabled();
    const native = screen.getByRole('combobox', { name: 'Native selection' });
    await userEvent.selectOptions(native, 'two');
    expect(native).toHaveValue('two');
    expect(screen.getByRole('option', { name: 'Two' })).toHaveProperty('selected', true);
  });
  it('preserves semantic label/value pairs, zero values and explicit missing facts', () => {
    const { container } = render(
      <DetailFacts
        items={[
          ['Employee number', 'FIXTURE-ONLY'],
          ['Minutes', 0],
          ['Location', null],
        ]}
      />,
    );
    expect(container.querySelector('dl')).toHaveClass('ui-detail-facts');
    expect(container.querySelectorAll('dt')).toHaveLength(3);
    expect(container.querySelectorAll('dd')).toHaveLength(3);
    expect(screen.getByText('0')).toBeInTheDocument();
    expect(screen.getByText('Not recorded')).toHaveClass('text-muted-foreground');
  });
  it('keeps routed actions as keyboard-accessible links using the shared button variants', async () => {
    render(
      <MemoryRouter initialEntries={['/record']}>
        <Routes>
          <Route
            path="/record"
            element={
              <div className="ui-record-actions">
                <BackLink to="/directory">Employee directory</BackLink>
                <LinkButton variant="destructive-outline" to="/end">
                  End employment
                </LinkButton>
              </div>
            }
          />
          <Route path="/directory" element={<h1>Directory destination</h1>} />
        </Routes>
      </MemoryRouter>,
    );
    const back = screen.getByRole('link', { name: 'Employee directory' });
    expect(back).toHaveAttribute('href', '/directory');
    expect(back).toHaveAttribute('data-variant', 'outline');
    expect(back.querySelector('svg')).toHaveAttribute('aria-hidden', 'true');
    expect(screen.getByRole('link', { name: 'End employment' })).toHaveAttribute(
      'data-variant',
      'destructive-outline',
    );
    await userEvent.tab();
    expect(back).toHaveFocus();
    await userEvent.keyboard('{Enter}');
    expect(screen.getByRole('heading', { name: 'Directory destination' })).toBeInTheDocument();
  });
  it('retains disabled/loading semantics for consequential commands', () => {
    render(
      <Button variant="destructive-outline" loading>
        Disable account
      </Button>,
    );
    expect(screen.getByRole('button', { name: 'Disable account' })).toBeDisabled();
    expect(screen.getByRole('button')).toHaveAttribute('aria-busy', 'true');
  });
});
