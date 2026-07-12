# UI And UX System

## Purpose

This document is the current source of truth for frontend visual and interaction rules. The product should feel like a quiet operational BPM workspace: information-dense where repeated work requires it, but spacious enough for forms, decisions and process context to remain readable.

## Style Ownership

Global styles are imported through `apps/web/src/app/globals.css` in a fixed cascade order:

- `tokens.css`: theme colors, spacing, radius, control size, motion and z-index values.
- `base.css`: reset, typography, common transitions and focus behavior.
- `auth.css`: login, registration, verification and password-reset surfaces.
- `shell.css`: persistent sidebar, topbar, session and notification controls.
- `dashboard.css`: metrics, donut chart and dashboard shortcuts.
- `components.css`: shared UI contracts, settings surfaces and reusable states.
- `management.css`: users, communities, logs, filters and pagination.
- `forms.css`: Form Designer and dynamic field editing.
- `processes.css`: process/task lists, runner, details, timeline and action dialogs.
- `responsive.css`: shared tablet/mobile overrides and reduced-motion behavior.

Do not place a new feature at the bottom of an unrelated file. Preserve the import order when adding a new stylesheet because later files may intentionally refine shared primitives.

## Core Tokens

- Use `--space-1` through `--space-6` for recurring gaps and padding.
- Cards and controls use `--radius-sm` or `--radius-md`; avoid introducing one-off large radii.
- Standard controls use `--control-height-sm` or `--control-height-md`.
- Motion uses `--motion-fast`, `--motion-normal` or `--motion-disclosure`.
- Layering uses named z-index tokens for toast, sidebar, mobile controls and dialogs.
- Light and dark colors come from the existing theme variables; feature components must not infer theme in JavaScript merely to choose colors.

## Shared Component Contracts

- `Button`: primary, secondary, success and danger variants with small/medium sizes and local loading state.
- `IconButton`: requires a visible tooltip/title and `aria-label`; danger tone is reserved for destructive actions.
- `RefreshButton`: keeps the current data visible, spins locally and reports page-level result through a toast.
- `EmptyState`: explains why content is empty and may expose one relevant next action.
- `SkeletonBlock`: first-load placeholder shaped like the final content.
- `InlineValueLoader`: compact numeric/value loading without temporary `0` or `-`.
- `ActionFeedback`: success/error/loading feedback inside the card that initiated a mutation.
- `ConfirmationDialog`: required for create, access, revoke, deactivate and delete operations when the action has meaningful impact.
- `JsonViewer`: the only raw JSON presentation surface; it contains long values and owns copy plus expand/collapse feedback.

Screen migrations to these contracts are incremental. Do not rewrite every feature merely to replace an existing stable button in one commit.

## Loading And Feedback

- First load: show a matching skeleton.
- Cached refresh: keep stale data visible and animate only the refresh control.
- Mutations: disable only the relevant controls and render feedback in the initiating card.
- Page refresh result: use the fixed workspace toast.
- Empty result: use a deliberate empty state, not a flashing zero or a generic error.
- Known API errors: map through the shared TR/EN dictionary; do not surface raw English backend text in Turkish UI.

## Motion And Accessibility

- Motion must explain a state change, not decorate every card.
- The language toggle is a fixed-width horizontal track: TR exits left when EN enters from the right; the reverse transition mirrors that direction.
- The dashboard donut keeps its approved stroke-width/opacity focus animation. Do not add SVG scale/translate transforms that move the chart.
- Disclosure, dialog and route transitions must remain short and respect `prefers-reduced-motion`.
- Icon-only controls require labels and keyboard focus. Color must not be the only status signal.

## Responsive Rules

- Desktop navigation remains fixed; tablet/mobile navigation uses the existing drawer.
- Related routes use reusable sidebar disclosures. `Formlar` owns designer/runner and `Yonetim` owns users/communities; route permissions still decide which children exist.
- General workspace content stays capped at `1180px`.
- Form Designer may expand to `1460px` on wide screens.
- At `1440px` and above, the field palette is the sticky third grid column. It must not use viewport-fixed positioning or overlap the canvas.
- Between 861px and 1439px, the field palette remains in normal flow below the canvas.
- At 860px and below, the normal palette is hidden. A draggable edge-snapping trigger opens a viewport-bottom sheet; selecting a type appends the field and returns focus to the trigger when the sheet closes.
- Dashboard work cards use two columns on desktop/tablet and one column on narrow mobile. Their list rows remain compact, show no more than four items and use an explicit empty state instead of stretching the page.
- Dashboard quick actions are permission-aware; unavailable routes must be omitted rather than rendered disabled.
- `overflow-x: clip` is intentional: it prevents horizontal spill without creating a scroll container that breaks sticky positioning.

## Verification Checklist

- Run frontend lint and production build after shared style changes.
- Check light/dark and TR/EN states.
- Check 1920, 1536 (1920 at 125% scale), 1440, 1024 and 390 CSS-pixel widths.
- Verify palette drag/drop at the beginning, middle and end of the field list.
- Verify sticky behavior while scrolling and confirm there is no horizontal overflow.
- Confirm dashboard chart behavior after any dashboard or shared SVG rule change.
