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
- Added a right-side sticky field palette for drag/drop field creation in the designer.
- Added icon-led palette cards with Turkish field descriptions and visible insertion preview while dragging.
- Added Text Area and Radio Button support. The Radio Button type is shown to Turkish users as `Seçenek düğmesi`.
- Hardened field key generation and saved payload keys so visible Turkish labels can stay localized while technical keys remain ASCII-safe.
- Tightened designer option validation so empty option values and duplicate option values block save for option-based fields.
- Added minimum backend form-type and form-data validation support for Text Area and Radio Button without changing process/task/audit/auth/dashboard/app shell business rules.
- Added designer-side dependent `RequiredWhen` validation UI.
- Strengthened form runner loading, empty, error, submitting, success and backend-error states.
- Made the process-start payload easier to inspect as `formDefinitionId` plus `formData`.
- Added presentation-oriented UI copy and scoped layout polish for the designer and runner.
- Ran a manual demo check through the local API for login, form creation, dependent validation, email validation and successful process start.
- Added `PUT /api/forms/{id}` so saved form definitions can be edited after creation.
- Added form designer loading for saved forms, with create/update behavior behind the same save control.
- Hardened frontend Text and TextArea validation so non-empty values must be strings and type failures use the dedicated localized `form.validation.text` message instead of a required-field message.
- Changed designer empty-key validation to inspect the raw `field.key` input before normalization, preventing an empty key from silently becoming a fallback such as `field1` and being saved.
- Strengthened Select and Radio option validation on both frontend and backend: option lists must exist, values must remain non-empty after trimming, and duplicates are rejected case-insensitively.
- Added backend process-start validation requiring Date values to be JSON strings parseable in exact `yyyy-MM-dd` format, and aligned backend Text/TextArea string validation.
- Added focused backend tests for empty and duplicate Radio options, empty Select options, invalid Date format and non-string TextArea data.
- Kept login/session, dashboard, app shell, process state machine, task approve/reject, audit generation, drag/drop UI and package/dependency definitions out of scope.

## Current Form Designer Capabilities

- Admin users can create a form definition model.
- Admin users can load and update an existing form definition from the saved form selector.
- Form name and description are editable.
- Fields can be added, removed and reordered with drag/drop or move up/down controls.
- Existing field `key`, `label`, `type` and `required` values can be edited.
- Supported field types are managed from a shared frontend list:
  - `Text`
  - `TextArea` / Text Area / Uzun metin
  - `Number`
  - `Email`
  - `Select`
  - `Radio` / Radio Button / Seçenek düğmesi
  - `Checkbox`
  - `Date`
- Select and radio fields keep option add, remove and edit behavior.
- Designer validation prevents empty field keys, empty labels, duplicate keys, empty option sets, empty option values and case-insensitive duplicate option values for option-based fields.
- Empty field keys are checked from the raw input with `field.key.trim()`. Key normalization is used only to compare uniqueness and to generate the save payload; it must not turn an empty raw key into a fallback key that bypasses validation.
- Select and Radio definitions receive equivalent final option validation from the backend on both create and update requests.
- Dependent `RequiredWhen` rules can be configured per field without changing backend models.
- The JSON preview reflects the same form model that is sent to the API, including ordering, options and validation rules.
- UI guidance now shows the demo path: add fields, edit properties, manage options/rules, reorder, inspect JSON and save.
- When a saved form is selected, save calls `PUT /api/forms/{id}`; otherwise save calls `POST /api/forms`.
- The designer layout uses two control panels at the top, then a full-width field list on desktop. Field editors spread key, label, type and required controls across the available width, then collapse to fewer columns on smaller screens.
- Checkbox controls are styled separately from text inputs so required toggles stay compact while still using the app accent color.
- The dependent validation editor keeps the rule controls visible without extra explanatory copy inside every field card.

## Current Form Runner Capabilities

- Saved form definitions can be loaded through the frontend API client.
- Loading, empty and error states are visible while the form list is being resolved.
- The selected form is rendered dynamically with the shared field renderer.
- User-entered form data is shown as JSON output.
- The process-start payload is displayed as `formDefinitionId` plus prepared `formData`.
- Submit blocks before the API call when frontend validation finds field errors.
- Frontend validation gives immediate field-level feedback, while backend validation remains authoritative for both saved form definitions and process-start data.
- Submit shows clear submitting, success and error feedback.
- Successful process-start responses can show process summary data such as id, status and started date.
- Validated form data is sent to `POST /api/processes/start` through the existing API client.
- Number values are converted before submit so they do not stay as plain strings when sent to the backend.
- Checkbox values stay boolean in the submitted payload.
- The form runner keeps the latest loaded form definitions in a lightweight client cache and uses skeleton rows on first load, preventing form fields from flashing empty during quick navigation.

## Validation Coverage

Validation responsibility is intentionally split by boundary:

- Frontend validation provides early, field-level feedback before an API request. Non-empty Text and TextArea values must be strings, and type failures use `form.validation.text` rather than a required-field message.
- Designer validation checks empty field keys from the raw `field.key.trim()` input. Normalization is used only for uniqueness comparison and save-payload generation, so it cannot hide an empty key behind a fallback such as `field1`.
- Frontend Select/Radio designer checks reject missing option sets, values that are empty after trimming and case-insensitive duplicates.
- Backend validation is the final source of truth. Form definition create/update applies the same Select/Radio option constraints, even if a client bypasses the designer.
- Backend process-start validation keeps required checks separate from type checks, requires non-empty Text/TextArea values to be JSON strings, and requires Date values to be JSON strings parseable in exact `yyyy-MM-dd` format.

Frontend validation currently covers:

- Required fields.
- Non-empty Text and TextArea values as strings, with type errors separate from required errors.
- Email format.
- Number values.
- Date values.
- Select option membership.
- Checkbox boolean values.
- Dependent `RequiredWhen` rules.
- Radio Button / `Seçenek düğmesi` option membership.
- ASCII-safe technical field keys in the designer save payload.
- Raw empty keys before save-payload normalization.
- Missing option sets, empty option values after trimming and case-insensitive duplicate option values in option-based designer fields.

The manual API check confirmed:

- `RequiredWhen` rejects a purchase request when approval note is empty.
- Invalid email values are rejected.
- A valid payload starts a process and returns `InProgress` process data.

Backend validation remains the final source of truth. Form definition validation protects create/update requests by requiring a name, at least one field, unique field keys and valid Select/Radio option sets; form-data validation independently protects process start.

## Dependent Validation Behavior

The designer can define this rule shape:

```json
{
  "ruleType": "RequiredWhen",
  "dependsOnFieldKey": "requestType",
  "expectedValue": "Satın Alma",
  "message": "Satın Alma taleplerinde onay açıklaması zorunludur."
}
```

In plain language: if another field has the expected value, the current field becomes required.

The designer prevents a field from depending on itself, requires a dependent field selection and requires a non-empty expected value. For select dependencies, the expected value can be selected from the dependent field's options. For checkbox dependencies, the expected value is `true` or `false`. Other field types use manual expected value input.

The runner reads the same `validationRules` array and blocks submit before calling the API when a dependent validation rule fails. Field-level errors remain visible next to the related dynamic field.

## Field Palette / Drag and Drop

- Drag/drop behavior uses the existing `@dnd-kit/core`, `@dnd-kit/sortable` and `@dnd-kit/utilities` dependencies.
- No new dependency was added for ordering or field creation.
- The Form Designer includes a true sticky third-column field palette from 1440 CSS pixels upward. It stays below the topbar without overlaying the canvas. On narrower screens the palette falls back into normal page flow.
- Palette items use field-specific icons, localized field names and short Turkish descriptions.
- Palette items do not create fields on click. A real drag/drop gesture is required before a new field is inserted.
- While dragging from the palette, the field list shows an insertion indicator so users can see where the field will be added.
- Dropping on an existing field inserts the new field at that visible position. Dropping on the general canvas/drop zone falls back to appending the field to the end.
- Text Area and Radio Button field creation use the same default field helper as the manual fallback.
- Move up/down controls remain available as a fallback and accessibility-friendly ordering path.
- After drag/drop, move up/down or remove operations, the field list is normalized so `sortOrder` stays sequential.
- JSON preview and save payload are generated from the current field order, so persisted form definitions follow the visible order.

## Supported Field Types

- `Text`: single-line text input; non-empty values must be strings on both frontend and backend.
- `TextArea`: multi-line text input, shown as Uzun metin in Turkish UI; it uses the same string-type rule as Text and a type failure is not reported as required.
- `Number`: numeric input with number conversion before process start.
- `Email`: email input with format validation.
- `Select`: option-based dropdown, shown with corrected Turkish copy such as Açılır seçim listesi.
- `Radio`: option-based single-choice field, shown to Turkish users as Seçenek düğmesi.
- `Checkbox`: boolean input that remains a boolean in submitted payloads.
- `Date`: date input with early frontend validation and authoritative backend JSON-string validation in exact `yyyy-MM-dd` format.

Every palette card has an icon from the existing lucide-react dependency. The UI can show localized Turkish labels, while generated field keys and technical payload keys stay ASCII-safe.

Select and Radio share option validation across the designer and backend definition boundary: option sets cannot be null or empty, trimmed values cannot be empty, and duplicate values are rejected case-insensitively.

## UI/UX Notes

- Designer cards were widened and the field list now uses the available layout more effectively.
- Checkbox controls were adjusted to stay normal-sized and aligned with other inputs.
- Dependent validation and option areas were made more readable with scoped layout polish.
- Drag handles are visibly labeled so ordering is easier to discover during a demo.
- The field palette uses a 280-320px sticky right column on wide desktop layouts. Form Designer alone may widen to 1460px, while other workspace routes retain the standard 1180px content width.
- Turkish UI copy was corrected for terms such as Satın Alma, Seçenek düğmesi and Açılır seçim listesi.
- RequiredWhen helper text was removed from every field card to keep the dependent validation area quieter while preserving the rule UI.
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
- `apps/web/src/features/i18n/translations.ts`
- `apps/web/src/app/globals.css`

Backend form-flow files:

- `apps/api/src/TechYouthBpm.Api/Controllers/FormsController.cs`
- `apps/api/src/TechYouthBpm.Application/Services/IFormService.cs`
- `apps/api/src/TechYouthBpm.Domain/Enums/FieldType.cs`
- `apps/api/src/TechYouthBpm.Infrastructure/Services/FormService.cs`
- `apps/api/src/TechYouthBpm.Infrastructure/Services/FormDataValidator.cs`
- `apps/api/tests/TechYouthBpm.Tests/Forms/FormServiceTests.cs`
- `apps/api/tests/TechYouthBpm.Tests/Forms/FormDataValidationTests.cs`

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
- Run a final end-to-end smoke test before merge if the integration baseline changes.
- Check integration with the process/task/audit flow after the teammate-owned process/task/audit work is complete.
- Decide whether form definitions should become immutable once processes are started. The current demo behavior updates the form definition in place so form editing is easy to demonstrate.
- Prepare a demo scenario if needed, for example: if request type is Satın Alma, approval note becomes required.
- Keep focused validation tests current if field types or process-start payload rules change.

## Verification

The frontend checks passed after the validation hardening work:

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

The backend solution build and focused form validation tests passed after validation hardening:

```bash
cd apps/api
dotnet build TechYouthBpm.slnx --no-restore
dotnet test tests/TechYouthBpm.Tests/TechYouthBpm.Tests.csproj --no-build --no-restore --filter "FullyQualifiedName~TechYouthBpm.Tests.Forms"
```

The focused Forms test group passed all 8 tests, including the new Radio, Select, Date and TextArea rejection scenarios. Frontend lint completed with no errors; its five existing unused-symbol warnings are in the out-of-scope `ProcessListView.tsx` file.

## Latest Mobile And Navigation UX

- `Form Tasarimi` and `Form Baslat` keep their native routes but now appear under one permission-aware `Formlar` sidebar disclosure.
- At 860 CSS pixels and below, the long palette card is replaced by a draggable circular trigger. The trigger is constrained to the viewport, snaps to the nearest horizontal edge and remembers a normalized vertical position for the device.
- The trigger opens an accessible bottom sheet from the viewport edge. Selecting a field type appends it through the existing palette insertion function, closes the sheet and scrolls the new field into view.
- Tablet/desktop drag-and-drop and field-card move controls remain unchanged.
- Designer, runner and process-detail JSON now use the shared `JsonViewer`, which contains long values, limits vertical growth and provides copy plus expand/collapse actions.
