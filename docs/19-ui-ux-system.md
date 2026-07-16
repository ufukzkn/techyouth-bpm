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
- `inbox.css`: inbox toolbar, notification rows, filters and responsive layout.
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
- Desktop sidebar width is `232px`; this keeps navigation readable without becoming optically dominant on 1920x1080 displays using 125% system scaling.
- General workspace content stays capped at `1400px` and targets the physical viewport center, not the area remaining beside the sidebar. Its symmetric safe width is calculated as `viewport - 2 x (sidebar + 16px)`, so browser zoom keeps equal space on both sides while the wider cap avoids an undersized center column. Mobile removes the desktop offset entirely.
- Between `1025px` and `1519px`, including 150%/175% browser zoom levels, strict viewport centering yields to a post-sidebar fit mode. Content keeps 16px workspace gutters and may extend farther right instead of clipping dense grids.
- Between `1520px` and `1599px`, the dashboard uses a scaled-desktop density preset: outer card gaps grow while card padding, minimum height, list-row height and donut size reduce slightly. Do not shrink the whole content column to compensate for 125% system scaling.
- In that same `1520px`-`1599px` scaled-desktop band, the sidebar-to-content gutter is `12px`; other desktop widths retain the standard `16px` gutter.
- Form Designer is the deliberate exception: it uses the post-sidebar workspace width with 16px outer gutters so its palette can occupy the otherwise empty right rail.
- At `1440px` and above, the field palette is the sticky third grid column. This includes a 1920x1080 display at 125% browser/OS scaling (1536 CSS pixels). It must not use viewport-fixed positioning or overlap the canvas.
- Below `1440px`, the normal palette is hidden. A draggable edge-snapping trigger opens the responsive field panel; selecting a type appends the field and returns focus to the trigger when the panel closes.
- The sheet's full 48px top strip is the dismissal target. Close animation must complete before unmount (`transitionend` plus timeout fallback); reduced-motion may close immediately.
- Mobile palette items append on tap. Existing canvas fields remain touch-sortable only from the drag handle, so normal card scrolling does not accidentally start a drag. Keep move up/down buttons as the keyboard and touch fallback.
- Dashboard counts belong to the donut legend rather than a duplicate metric-card row. Header actions contain only permission-aware form/process creation commands; recent activity is notification-backed and capped at four rows.
- Inbox and notification popover never fetch the complete history. Popover renders five records and the true unread count; inbox renders ten-record server pages with deliberate skeleton, empty and error states.
- `overflow-x: clip` is intentional: it prevents horizontal spill without creating a scroll container that breaks sticky positioning.

## Verification Checklist

- Run frontend lint and production build after shared style changes.
- Check light/dark and TR/EN states.
- Check 1920, 1536 (1920 at 125% scale), 1440, 1024 and 390 CSS-pixel widths.
- Verify palette drag/drop at the beginning, middle and end of the field list.
- Verify mobile field reordering from the handle, normal page scrolling outside the handle, edge-snap trigger movement and visible sheet close/snap-back motion.
- Verify notification search, read/category filters, direct page jump, read/unread toggles and navigation targets without loading all records.
- Verify sticky behavior while scrolling and confirm there is no horizontal overflow.
- Confirm dashboard chart behavior after any dashboard or shared SVG rule change.
