# Ozgun Form Flow

## Purpose

This document tracks Ozgun Saz's ownership area: Form Design and Form Run Flow. The goal is to make form definitions data-driven, editable and reusable, then render those definitions dynamically when a user starts a process.

## Scope Boundary

This work is frontend-focused. It uses the existing backend form definition and `RequiredWhen` validation model without changing backend process rules.

The scope includes:

- Form designer behavior.
- Form definition JSON shape.
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
- Added form value helpers, including stronger number conversion before submit.
- Improved the form designer so existing fields can be edited.
- Added select/checkbox option management in the form designer.
- Added move up/down ordering for form fields.
- Added designer-side dependent `RequiredWhen` validation UI.
- Kept backend, login/session, dashboard, app shell, process/task and audit behavior out of scope.

## Current Form Designer Capabilities

- Admin users can create a form definition model.
- Form name and description are editable.
- Fields can be added, removed and reordered with move up/down controls.
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
- The JSON preview reflects the same form model that is sent to the API.

## Current Form Runner Capabilities

- Saved form definitions can be loaded through the frontend API client.
- The selected form is rendered dynamically with the shared field renderer.
- User-entered form data is shown as JSON output.
- Submit keeps the existing loading, success and error states.
- Validated form data is sent to `POST /api/processes/start` through the existing API client.
- Number values are converted before submit so they do not stay as plain strings when sent to the backend.

## Validation Coverage

Frontend validation currently covers:

- Required fields.
- Email format.
- Number values.
- Date values.
- Select option membership.
- Checkbox boolean values.
- Dependent `RequiredWhen` rules.

Backend validation remains the final source of truth when a process is started.

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

The runner reads the same `validationRules` array and blocks submit before calling the API when a dependent validation rule fails.

## Files Changed

Frontend form-flow files:

- `apps/web/src/features/form-designer/FormDesignerDraft.tsx`
- `apps/web/src/features/form-runner/FormRunnerDraft.tsx`
- `apps/web/src/features/forms/fieldTypes.ts`
- `apps/web/src/features/forms/fieldRenderer.tsx`
- `apps/web/src/features/forms/formValidation.ts`
- `apps/web/src/features/forms/formValues.ts`
- `apps/web/src/app/globals.css`

Documentation files:

- `docs/01-agent-notes.md`
- `docs/09-ozgun-form-flow.md`

## Out of Scope

These areas were intentionally not changed:

- Backend code.
- Login/session flow.
- Dashboard.
- App shell behavior.
- Process state machine rules.
- Task approve/reject business logic.
- Audit log generation.
- Package files such as `package.json` and `package-lock.json`.
- Drag/drop field ordering.
- Renaming draft feature files to final production names.

## Remaining Work

- Strengthen the Form Runner experience and submit/payload flow.
- Add UI/UX polish after the main behavior is stable.
- Prepare a demo scenario if needed, for example: if request type is purchase, approval note becomes required.
- Do final documentation and final test verification.

## Verification

The frontend checks passed after the form foundation, designer editing and dependent validation work:

```bash
cd apps/web
npm run lint
npm run build
```
