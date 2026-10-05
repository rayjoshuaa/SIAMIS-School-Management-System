import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { FormField } from '../../components/shared/form-field';
import { Input, Textarea } from '../../components/ui/controls';
import { Button } from '../../components/ui/button';
import { useNotify } from '../../app/providers/notifications';
const schema = z.object({
  displayName: z
    .string()
    .trim()
    .min(1, 'Enter a display name.')
    .max(80, 'Use 80 characters or fewer.'),
  note: z.string().max(200, 'Use 200 characters or fewer.'),
});
type Values = z.infer<typeof schema>;
export function DemoForm() {
  const notify = useNotify();
  const {
    register,
    handleSubmit,
    formState: { errors, isSubmitting },
  } = useForm<Values>({
    resolver: zodResolver(schema),
    defaultValues: { displayName: '', note: '' },
  });
  return (
    <form
      noValidate
      onSubmit={handleSubmit(() => {
        notify('Demo validated locally. No API call or data was saved.');
      })}
      className="space-y-5"
    >
      <fieldset className="space-y-5">
        <legend className="mb-3 text-sm font-semibold">Display preferences — sample only</legend>
        <div className="grid grid-cols-1 gap-5 sm:grid-cols-2">
          <FormField
            id="display-name"
            label="Display name"
            required
            hint="A demonstration field, not an employee record."
            error={errors.displayName?.message}
          >
            {(props) => <Input {...props} {...register('displayName')} autoComplete="off" />}
          </FormField>
          <FormField id="read-only" label="Workspace" hint="Read-only example">
            {(props) => <Input {...props} value="Design system" readOnly />}
          </FormField>
        </div>
        <FormField id="note" label="Note" error={errors.note?.message}>
          {(props) => <Textarea {...props} {...register('note')} />}
        </FormField>
        <FormField id="disabled" label="Unavailable field">
          {(props) => <Input {...props} disabled placeholder="Disabled example" />}
        </FormField>
      </fieldset>
      <div className="flex flex-wrap items-center gap-3">
        <Button type="submit" loading={isSubmitting}>
          Validate sample
        </Button>
        <p className="text-xs text-muted-foreground">Required fields are marked with *.</p>
      </div>
    </form>
  );
}
