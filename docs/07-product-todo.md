# Product Todo

This file tracks lightweight product and presentation ideas that are not ready to become implementation tasks yet.

## Branding

- Find a stronger product name than `TechYouth BPM Wizard`.
- Explore a simple logo direction that combines form, workflow and approval concepts.
- Current temporary logo is an original SVG prototype and should be replaced before final presentation if the team chooses a better brand direction.
- Keep the visual identity professional and operational, not marketing-heavy.

## UI Polish

- Add a more deliberate empty state for dashboards, forms and tasks.
- Decide whether the dashboard BPM legend should stay as a passive explanation or become shortcut buttons to the related screens.
- Improve responsive spacing after the full feature set is wired.
- Add subtle interaction animations where they clarify state changes.
- Review dark mode contrast after all screens are implemented.

## API Demo

- Keep Swagger protected with the same Bearer token flow as the app instead of adding a separate bypass key.
- If Swagger demos feel slow later, consider a development-only helper note or demo-login shortcut, but keep it out of production-style auth paths.

## Access And Identity

- Decide whether the final BPM product should allow self-registration. For a corporate BPM flow, admin-created users or admin-approved registrations are more realistic than open public signup.
- Add email + OTP verification for registration.
- Keep newly registered users in a `PendingApproval` state until an Admin approves access.
- Add an Admin user-management panel for creating users, assigning roles, deactivating users and approving pending registrations.
- Add profile settings for updating display name, email and password.
- Consider JWT access tokens only together with a refresh-token or remember-me design; do not replace opaque sessions just for the label.

## Workflow

- Decide later whether GitHub issues are needed.
- For now, keep lightweight planning in `docs/` so the project stays easy to present.
- Convert TODO items into issues only if the backlog becomes hard to track in Markdown.
