import type { ComponentProps } from 'react';
import { cva } from 'class-variance-authority';
import { cn } from '../../lib/utils/cn';
import { ArrowLeft, LoaderCircle } from 'lucide-react';
import { Link, type LinkProps } from 'react-router-dom';
const styles = cva(
  'ui-button inline-flex items-center justify-center gap-2 px-4 text-sm font-semibold disabled:cursor-not-allowed disabled:opacity-50',
  {
    variants: {
      variant: {
        primary: 'ui-button-primary',
        secondary: 'bg-secondary text-secondary-foreground enabled:hover:bg-muted',
        outline:
          'border border-border bg-secondary text-secondary-foreground enabled:hover:bg-muted',
        ghost: 'enabled:hover:bg-muted',
        destructive: 'bg-destructive text-primary-foreground enabled:hover:opacity-90',
        'destructive-outline': 'ui-button-destructive-outline border bg-surface',
      },
      icon: { true: 'min-w-11 px-2', false: '' },
      density: { comfortable: 'min-h-11', compact: 'min-h-9 max-sm:min-h-11' },
    },
    defaultVariants: { variant: 'primary', icon: false },
  },
);
export type ButtonProps = ComponentProps<'button'> & {
  variant?: 'primary' | 'secondary' | 'outline' | 'ghost' | 'destructive' | 'destructive-outline';
  icon?: boolean;
  loading?: boolean;
  density?: 'comfortable' | 'compact';
};
export function Button({
  variant,
  icon,
  loading,
  density = 'comfortable',
  disabled,
  className,
  children,
  type = 'button',
  ...props
}: ButtonProps) {
  return (
    <button
      type={type}
      disabled={disabled || loading}
      aria-busy={loading || undefined}
      data-variant={variant ?? 'primary'}
      data-density={density}
      className={cn(styles({ variant, icon, density }), className)}
      {...props}
    >
      {loading && <LoaderCircle aria-hidden="true" className="size-4 animate-spin" />}
      {children}
    </button>
  );
}

/** A real router link with the same visual contract as a workflow button. */
export function LinkButton({
  variant = 'outline',
  density = 'compact',
  className,
  ...props
}: LinkProps & Pick<ButtonProps, 'variant' | 'density'>) {
  return (
    <Link
      {...props}
      data-variant={variant}
      data-density={density}
      className={cn(styles({ variant, density }), 'ui-link-button', className)}
    />
  );
}

export function BackLink({ children, ...props }: LinkProps) {
  return (
    <LinkButton {...props} variant="outline">
      <ArrowLeft aria-hidden="true" />
      {children}
    </LinkButton>
  );
}
