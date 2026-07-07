# Ozgun Form Flow

## Purpose

This document tracks Ozgun Saz's ownership area: Form Design and Form Run Flow. The goal is to make form definitions data-driven, editable and reusable, then render those definitions dynamically when a user starts a process.

## Scope Boundary

This work is mostly form-flow focused. It uses the existing backend form definition and `RequiredWhen` validation model without changing backend process rules.

The scope includes:

- Form designer behavior.
- Form definition JSON shape.
- Form definition create/update API behavior.
- Field type metadata.
- Shared field rendering.
- Frontend validation helpers.
- Dynamic form runner behavior.
- Process-start payload preparation from submitted form data.

## Completed Work

- Created the form foundation branch: `feature/ozgun-form-foundation`.
- Added shared field type helpers for supported field types and default field creation.
- Added a reusable field renderer for dynamic form runner inputs.
- Added reusable frontend validation helpers for required, type-based and dependent validation.
- Added form value helpers, including number conversion before submit and boolean preservation for checkbox values.
- Improved the form designer so existing fields can be edited.
- Added select/checkbox option management in the form designer.
- Added move up/down ordering for form fields.
- Added drag/drop field ordering with the existing dnd-kit dependency set.
- Added designer-side dependent `RequiredWhen` validation UI.
- Strengthened form runner loading, empty, error, submitting, success and backend-error states.
- Made the process-start payload easier to inspect as `formDefinitionId` plus `formData`.
- Added presentation-oriented UI copy and scoped layout polish for the designer and runner.
- Ran a manual demo check through the local API for login, form creation, dependent validation, email validation and successful process start.
- Added `PUT /api/forms/{id}` so saved form definitions can be edited after creation.
- Added form designer loading for saved forms, with create/update behavior behind the same save control.
- Kept backend, login/session, dashboard, app shell, process/task and audit behavior out of scope.

## Current Form Designer Capabilities

- Admin users can create a form definition model.
- Admin users can load and update an existing form definition from the saved form selector.
- Form name and description are editable.
- Fields can be added, removed and reordered with drag/drop or move up/down controls.
- Existing field `key`, `label`, `type` and `required` values can be edited.
- Supported field types are managed from a shared frontend list:
  - `Text`
  - `Number`
  - `Email`
  - `Select`
  - `Checkbox`
  - `Date`
- Select and checkbox fields support option add, remove and edit behavior.
- Designer validation prevents empty field keys, empty labels, duplicate keys and empty option sets for option-based fields.
- Dependent `RequiredWhen` rules can be configured per field without changing backend models.
- The JSON preview reflects the same form model that is sent to the API, including ordering, options and validation rules.
- UI guidance now shows the demo path: add fields, edit properties, manage options/rules, reorder, inspect JSON and save.
- When a saved form is selected, save calls `PUT /api/forms/{id}`; otherwise save calls `POST /api/forms`.
- The designer layout uses two control panels at the top, then a full-width field list on desktop. Field editors spread key, label, type and required controls across the available width, then collapse to fewer columns on smaller screens.
- Checkbox controls are styled separately from text inputs so required toggles stay compact while still using the app accent color.

## Current Form Runner Capabilities

- Saved form definitions can be loaded through the frontend API client.
- Loading, empty and error states are visible while the form list is being resolved.
- The selected form is rendered dynamically with the shared field renderer.
- User-entered form data is shown as JSON output.
- The process-start payload is displayed as `formDefinitionId` plus prepared `formData`.
- Submit blocks before the API call when frontend validation finds field errors.
- Submit shows clear submitting, success and error feedback.
- Successful process-start responses can show process summary data such as id, status and started date.
- Validated form data is sent to `POST /api/processes/start` through the existing API client.
- Number values are converted before submit so they do not stay as plain strings when sent to the backend.
- Checkbox values stay boolean in the submitted payload.
- The form runner keeps the latest loaded form definitions in a lightweight client cache and uses skeleton rows on first load, preventing form fields from flashing empty during quick navigation.

## Validation Coverage

Frontend validation currently covers:

- Required fields.
- Email format.
- Number values.
- Date values.
- Select option membership.
- Checkbox boolean values.
- Dependent `RequiredWhen` rules.

The manual API check confirmed:

- `RequiredWhen` rejects a purchase request when approval note is empty.
- Invalid email values are rejected.
- A valid payload starts a process and returns `InProgress` process data.

Backend validation remains the final source of truth when a process is started. Backend form definition validation also protects create/update requests by requiring a name, at least one field, unique field keys and option values for option-based fields.

## Dependent Validation Behavior

The designer can define this rule shape:

```json
{
  "ruleType": "RequiredWhen",
  "dependsOnFieldKey": "requestType",
  "expectedValue": "Satinalma",
  "message": "Satinalma taleplerinde onay aciklamasi zorunludur."
}
```

In plain language: if another field has the expected value, the current field becomes required.

The designer prevents a field from depending on itself, requires a dependent field selection and requires a non-empty expected value. For select dependencies, the expected value can be selected from the dependent field's options. For checkbox dependencies, the expected value is `true` or `false`. Other field types use manual expected value input.

The runner reads the same `validationRules` array and blocks submit before calling the API when a dependent validation rule fails. Field-level errors remain visible next to the related dynamic field.

## Drag And Drop / Ordering

- Drag/drop ordering uses the existing `@dnd-kit/core`, `@dnd-kit/sortable` and `@dnd-kit/utilities` dependencies.
- No new dependency was added for ordering.
- Move up/down controls remain available as a fallback and accessibility-friendly ordering path.
- After drag/drop, move up/down or remove operations, the field list is normalized so `sortOrder` stays sequential.
- JSON preview and save payload are generated from the current field order, so persisted form definitions follow the visible order.

## UI/UX Notes

- Designer cards were widened and the field list now uses the available layout more effectively.
- Checkbox controls were adjusted to stay normal-sized and aligned with other inputs.
- Dependent validation and option areas were made more readable with scoped layout polish.
- Drag handles are visibly labeled so ordering is easier to discover during a demo.
- Runner states now explain loading, empty list, validation-blocked submit, success and backend-error outcomes.
- The process-start payload panel is labeled for demo review and keeps submitted JSON visible.
- CSS changes were scoped to form designer/form runner classes and did not intentionally change global app behavior.

## Files Changed

Frontend form-flow files:

- `apps/web/src/features/form-designer/FormDesignerDraft.tsx`
- `apps/web/src/features/form-runner/FormRunnerDraft.tsx`
- `apps/web/src/features/forms/fieldTypes.ts`
- `apps/web/src/features/forms/fieldRenderer.tsx`
- `apps/web/src/features/forms/formValidation.ts`
- `apps/web/src/features/forms/formValues.ts`
- `apps/web/src/app/globals.css`

Backend form-flow files:

- `apps/api/src/TechYouthBpm.Api/Controllers/FormsController.cs`
- `apps/api/src/TechYouthBpm.Application/Services/IFormService.cs`
- `apps/api/src/TechYouthBpm.Infrastructure/Services/FormService.cs`
- `apps/api/tests/TechYouthBpm.Tests/Forms/FormServiceTests.cs`

Documentation files:

- `docs/01-agent-notes.md`
- `docs/09-ozgun-form-flow.md`

## Out of Scope

These areas were intentionally not changed:

- Login/session flow.
- Dashboard.
- App shell behavior.
- Process state machine rules.
- Task approve/reject business logic.
- Audit log generation.
- Package files such as `package.json` and `package-lock.json`.
- Renaming draft feature files to final production names.

## Remaining Work

- Make small UI fixes if the team review finds presentation issues.
- Address any PR review feedback.
- Run a short smoke test before final merge.
- Check integration with the process/task/audit flow after the teammate-owned process/task/audit work is complete.
- Decide whether form definitions should become immutable once processes are started. The current demo behavior updates the form definition in place so form editing is easy to demonstrate.
- Prepare a demo scenario if needed, for example: if request type is purchase, approval note becomes required.
- Do final documentation and final test verification.

## Verification

The frontend checks passed after the form foundation, designer editing, dependent validation, saved-form update, runner state, drag/drop ordering and UI polish work:

```bash
cd apps/web
npm run lint
npm run build
```

Manual local checks were also run against the API for:

- Login with the demo admin user.
- Creating a form definition with select, text, number, email, date and checkbox fields.
- `RequiredWhen` validation failure when the dependent field condition is met.
- Email validation failure.
- Successful process start with number and boolean payload values preserved.

The backend checks also passed after adding form update tests:

```bash
dotnet test apps/api/TechYouthBpm.slnx
```
