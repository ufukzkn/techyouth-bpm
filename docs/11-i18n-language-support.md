# I18n Language Support

## Purpose

The PDF lists language support as an extra quality item. In this project it is treated as part of the production-readiness story: static UI text should be switchable without changing business logic, API contracts or stored workflow data.

## Current Implementation

- Supported languages: Turkish (`tr`) and English (`en`).
- The active language is stored in the Zustand session store and persisted with the rest of the UI preferences.
- The top bar has a language icon button. The login screen also exposes the same language toggle, so users can switch language before signing in.
- The document language (`html lang`) is updated from the active language.
- Translation entries live in `apps/web/src/features/i18n/translations.ts`.
- The first translated scope covers:
  - Login screen and session-expiry dialog.
  - App shell, sidebar navigation and top bar actions.
  - Dashboard cards and BPM shortcut labels.
  - Settings screen.
  - Form designer static UI, field editor actions, option/rule editor labels and designer validation messages.
  - Form runner static UI, process-start buttons, loading/empty states and frontend validation messages.
  - Process/task board, refresh feedback, toast messages, task action dialog, status badges and audit timeline.

## Intentional Boundaries

- Form names, field labels, select options, process submission values and audit notes loaded from the database are treated as user/domain data. They are displayed as stored instead of being auto-translated.
- The form designer default sample fields and saved form labels are treated as editable form data. They are not automatically translated after the user edits or loads them from the database.
- Backend error messages are currently displayed as returned by the API. A later production version can map backend error codes to frontend translation keys.

## Extension Rules

- New static UI text should use `translate(language, key)` instead of inline strings.
- Add the same key to both `tr` and `en` dictionaries.
- Prefer stable, domain-based keys such as `process.refreshing` or `settings.languageValue`.
- Keep user-generated or database-seeded workflow content out of automatic translation unless a real localization model is added.
