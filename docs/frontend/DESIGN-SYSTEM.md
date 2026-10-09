# Design-system authority

Current platform-wide authority: [V3.1 Premium Design System Foundation](V3.1-PREMIUM-DESIGN-SYSTEM-FOUNDATION.md), implemented in [semantic tokens](../../frontend/src/app/styles/tokens.css), shared styles and owned components. [V3.2 shell](V3.2-PREMIUM-APPLICATION-SHELL.md) and [V3.3 authentication](V3.3-PREMIUM-AUTHENTICATION.md) apply it. F1 and V2.1 reports preserve earlier decisions, not competing current libraries.

The identity uses burgundy, warm white, charcoal, restrained gold and official school assets. Typography, spacing, density, borders, focus and interaction states come from shared tokens. Reuse buttons, fields, selects, tables, feedback and overlays rather than copying style constants into features.

Layouts suit the workflow: tables, profiles, processing workspaces and dashboards share hierarchy and controls without identical composition. Maintain label/value hierarchy, compact gutters, safe select truncation and responsive filter wrapping. Static metrics/panels must not suggest interactivity.

Overlays retain focus, Escape handling, stable actions, responsive widths and pending/unsaved protections. Avoid nested overlays. Motion is fast and restrained, respects reduced motion and never delays commands or navigation. Focus and validation remain distinguishable.

The Development-only `/dev/ui` showcase uses synthetic data without database writes. Raw review screenshots require privacy review before distribution. New features and individual page redesigns require separate approval.
