# SIAMIS design system

F1 establishes a professional light theme: burgundy identity, restrained warm gold, white surfaces and a warm neutral canvas. Borders, spacing and readable hierarchy provide structure. There are no decorative gradients, glass effects or page animations.

## Tokens

The authority is [tokens.css](../../frontend/src/app/styles/tokens.css), mapped to Tailwind semantic utilities. Components do not carry scattered literal colors.

| Token family | Use |
|---|---|
| primary / primary-foreground | Burgundy action and white text |
| secondary / secondary-foreground | Subordinate action |
| accent / accent-foreground | Restrained gold highlight |
| background / foreground | Canvas and body text |
| surface / surface-muted | Content and grouped regions |
| border / input / ring | Separators, control boundaries and visible focus |
| muted / muted-foreground | Supporting surfaces and readable secondary text |
| success / warning / destructive / info | Semantic visual intents, with supporting tinted surfaces |
| sidebar / sidebar-foreground / sidebar-active | Light navigation surfaces and restrained active destination |

Spacing follows a 4px unit and the Tailwind scale. Control/card radius is 6px; badges may be pills. Shadows are reserved for overlays. The local sans-serif stack is Segoe UI, Noto Sans Thai if available, Tahoma, Arial, sans-serif. No external font service is required. A final licensed/self-hosted font and official logo can be approved later.

Titles are 24–30px, sections 18px, card headings 16–20px, body 16px, labels/table content 14px and helper text 12px. Thai fallback and numeral readability are demonstrated. Do not make helper text the primary content.

## Responsive behavior

Tailwind breakpoints: sm 640px, md 768px, lg 1024px, xl 1280px, 2xl 1536px. Page actions wrap/stack; grids reduce columns; forms use one column on narrow screens; overlays remain within the viewport. The showcase's minimal navigation remains separate from the F2 application sidebar.

F2 uses the existing tokens without palette, typography or spacing changes. The light application sidebar expands to 248px or collapses to a 72px module icon rail with labelled tooltips. Compact 44px rows, shallow child indentation and small planned indicators communicate the whole school platform. Below md it becomes a modal drawer. Active destinations combine accent border, surface, font weight and `aria-current`; the active HR parent is also labelled. Main content is fluid up to 100rem. Compact empty dashboard cards use F1 borders and radius, without chart libraries, heavy shadows or fabricated metrics. Mobile breadcrumbs show the current destination; primary actions remain visible while secondary actions can move into an overflow menu. Account names retain full accessible text even when their visible header label truncates. No official logo is invented; the Brand component contains the approved text treatment.

Tables use semantic desktop markup and an explicit mobile card/list pattern below md. Native table primitives remain available for future priority-column/scroll/detail patterns; each feature must choose intentionally. No generic grid engine or TanStack Table is needed for F1. Sample sorting/pagination are local showcase interactions, not business API behavior.

## Components and accessibility

Primitives cover Button, Input, Textarea, Label, Select, Checkbox, RadioGroup, Switch, Badge, Card, Separator, Tabs, Tooltip, Popover, DropdownMenu, Dialog, AlertDialog, Sheet, notification Toast, Skeleton, Spinner, Alert, Avatar, Breadcrumb, Pagination, Table and EmptyState. Shared Page components and FormField prevent duplicated spacing/error wiring.

Buttons have primary, secondary, outline, ghost and destructive variants; icon buttons need an accessible name. Interactive targets are at least 44px where these controls are used. Focus remains visible, controls have associated labels, validation errors use aria-describedby/aria-invalid, dialogs manage focus and Escape, and loading/notifications have accessible announcements. Reduced motion removes animation. StatusBadge accepts a visual intent and does not know business statuses or rules.

The showcase is review evidence for a baseline, not a claim of complete accessibility certification. Future feature content needs keyboard, contrast, screen-reader and responsive verification of its own. See [F1 results](F1-REPORT.md).
