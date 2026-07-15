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
- Added `FileUpload` to the frontend/backend field-type contract without changing the existing enum values. File Upload is available from the Designer palette and type selector, keeps required-field behavior, does not open the Select/Radio option editor, and survives form definition create/update round trips.
- Added a single-file Runner control that stores JSON-safe file metadata instead of a browser `File`, plus explicit copy explaining that actual upload will be delivered in a later phase.
- Added frontend and backend validation for the fixed File Upload metadata policy, and normalized ASP.NET model-binding validation dictionaries in the frontend API client without breaking the existing localized community-required error mapping.
- During validation hardening, kept login/session, dashboard, app shell, process state machine, task approve/reject, audit generation, drag/drop UI and package/dependency definitions out of scope.
- Fixed the ThemeToggleButton first-render theme mismatch that caused hydration errors on `/login`, without changing persisted theme behavior.
- Compacted and centered the desktop palette rail, removed its unnecessary internal scrollbar and placed save/update in a separate action panel below the palette.
- Hardened palette drops so field creation requires a valid canvas or existing-field target; palette, empty and invalid targets create nothing, and cancellation clears insertion feedback.
- Added a short form-selection overlay plus management-style opening skeletons for the designer and runner. Designer loading includes field-card-shaped placeholders with header, metadata, controls and action/drag affordances.
- Added scoped move up/down feedback: the clicked card receives a short primary highlight and directional motion, while the displaced neighbor receives subtler neutral motion. This uses no FLIP measurements or drag overlay.

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
  - `FileUpload` / File Upload / Dosya yükleme
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
- Changing the saved-form selection shows a short translucent loading overlay over the Form Information panel only; the field list, palette, JSON preview and save panel remain available in their existing layout.
- Initial designer loading uses shared `SkeletonBlock`/`InlineValueLoader` styling and field-card-shaped placeholders before the normal designer is revealed.

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
- Process `startedAt` uses the shared `formatApiDateTime` formatter with the current language and a safe fallback when the value is empty.
- Validated form data is sent to `POST /api/processes/start` through the existing API client.
- Number values are converted before submit so they do not stay as plain strings when sent to the backend.
- Checkbox values stay boolean in the submitted payload.
- The form runner keeps the latest loaded form definitions in a lightweight client cache and uses a management-style opening skeleton for the form and payload preview during the true initial `loading` state.
- Form-list loading uses an unmount ignore guard so a completed async request cannot update runner state, messages or the shared form cache after the component is gone.
- Cached `refreshing` behavior and runner business logic remain unchanged.
- The Runner heading, description, form panel and JSON preview share the same centered `.runner-section` axis. Runner-scoped CSS balances the space after the sidebar without changing global `.content`, `.main-area`, AppShell or sidebar rules; below 960px of actual Runner width, the JSON panel can stack under the form.
- Changing the saved form updates its initial values and JSON preview immediately, then shows a 240ms `Form hazırlanıyor...` overlay scoped to `.runner-form`; the JSON panel is never covered by this overlay.
- Process start enters its disabled spinner state with `Süreç başlatılıyor...` only after `validateFormValues` succeeds. A ref-based in-flight lock blocks double submit and is always released in `finally`, while existing success/error feedback remains intact.
- Clear keeps the existing `buildInitialValues` reset behavior, clears validation errors and the previous process result immediately, and shows short `Temizleniyor...` feedback.
- `validateFormValues`, `prepareFormData`, the API call and payload shape, `RequiredWhen`, metadata-only File Upload, number/date/select/radio/checkbox conversions, JSON preview copy/expand actions, Form Designer, palette drag/drop and existing-field reorder remain unchanged. No package or lockfile update was required.

## File Upload Foundation

- `FileUpload` is part of the frontend `FieldType` union, shared supported-field list and backend `FieldType` enum. The backend value was appended without renumbering existing field types.
- The Designer palette and field type selector expose File Upload with the existing icon/localization system. It uses the general field-definition shape, supports `required`, persists through create/update, and never opens the Select/Radio option editor.
- Existing Text, TextArea, Number, Email, Select, Radio, Checkbox and Date behavior remains unchanged.
- Palette creation and invalid-drop guards remain unchanged: returning a palette item to the palette creates nothing. Existing drag/drop reorder, move up/down, `RequiredWhen`, sequential `sortOrder`, JSON preview and save/update payload behavior remain intact.

## File Upload Metadata-Only Behavior

The current File Upload support is a metadata-only foundation, not a real file upload module. The Runner lets the user select one local file and creates this JSON-safe value:

```json
{
  "name": "document.pdf",
  "size": 1024,
  "type": "application/pdf",
  "lastModified": 0
}
```

- The browser `File` object is never placed in form values, JSON preview or the process-start request.
- JSON preview and `formData` contain only the metadata object or `null`.
- The Runner shows the selected file name, size and MIME type, and provides selection/clear controls styled with the existing form UI.
- UI copy deliberately avoids language such as “uploaded” or “upload complete.” It states that selected metadata is included now and actual upload will be added later.

## File Upload Validation

- The first-phase policy is single-file only with a maximum size of 10 MB.
- Allowed extensions are `pdf`, `png`, `jpg`, `jpeg`, `doc`, `docx`, `xls` and `xlsx`.
- Allowed MIME types are:
  - `application/pdf`
  - `image/png`
  - `image/jpeg`
  - `application/msword`
  - `application/vnd.openxmlformats-officedocument.wordprocessingml.document`
  - `application/vnd.ms-excel`
  - `application/vnd.openxmlformats-officedocument.spreadsheetml.sheet`
- Frontend validation rejects an empty required File Upload, malformed metadata, non-positive or over-limit size, disallowed MIME type and disallowed extension before process start.
- Backend `FormDataValidator` independently requires the metadata object shape and validates `name`, `size`, `type`, `lastModified`, the 10 MB limit and both allowlists.
- Backend validation protects only the metadata contract. It does not inspect or validate file contents because no binary is transferred or stored in this phase.

## API Error Normalization

- The frontend API client preserves the existing `{ "errors": ["..."] }` behavior.
- ASP.NET model-binding responses with an `errors` object/dictionary are flattened from their string-array values into a stable `ApiError.errors` list.
- Standard problem-details `message`, `detail` and `title` values are supported as fallbacks; unknown response shapes use a generic error without throwing a parsing `TypeError`.
- The known `A community is required for form definitions.` message still reaches the Designer presentation mapping and is shown in Turkish as `Form tanımı için bir topluluk seçilmelidir.`.

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
- The Form Designer uses a sticky third-column palette rail when at least 1600 CSS pixels are available. Narrower and zoom-constrained layouts hide the inline palette and use the right-side FAB/drawer, so the palette no longer falls below the form.
- Palette items use field-specific icons, localized field names and short Turkish descriptions.
- Desktop rail palette items do not create fields on click; a real drag/drop gesture is required there. Drawer selection intentionally uses click-to-add.
- Palette drags use pointer-based `pointerWithin` collision detection, prioritize field droppables over the parent canvas and use pointer Y for upper/lower-half insertion. Existing-field reorder uses a separate field-only collision path: pointer collisions are preferred and keyboard/no-pointer operation falls back to `closestCenter`, with both result sets filtered to current field IDs.
- While dragging from the palette, the field list shows the authoritative insertion indicator. Drag end uses the last displayed `paletteInsertIndex` and does not estimate the index again; if no preview/index exists, no field is created.
- A populated canvas has no general `fields.length` fallback. An empty canvas still inserts at index `0`; the last field's lower half appends normally, and a controlled 24px region immediately below only the last field makes the same end insertion slightly easier. Distant or ambiguous canvas space remains invalid.
- Dropping a palette item back on the palette, outside the designer or on any other invalid target does not create a field. Cancelling a palette drag clears the insertion preview.
- Palette drag visuals do not use dnd-kit `DragOverlay`. A presentation-only `position: fixed` ghost is rendered through a `document.body` portal and follows a palette-only `window.pointermove` listener with `pointer-events: none`; it does not participate in collision, preview or index calculation.
- The source palette item stays in place as a low-opacity placeholder with its drag transform disabled. The palette list keeps its own scroll boundary and cannot expand over the separate save/update panel during a drag.
- Existing-field drag keeps each editor's natural height; the previous active option/rule collapse was removed so long Select, Radio, `RequiredWhen` and File Upload cards no longer change the sortable slot geometry. The active card remains opaque and is separated with a clearer border, shadow and stacking level.
- Existing-only collision filtering prevents `field-canvas`, the palette rail and other parent droppables from becoming reorder targets. Keyboard reorder retains the field-only `closestCenter` fallback, while the palette collision branch remains unchanged.
- The existing `SortableFieldCard` preserves the x/y translation from `useSortable` but normalizes `scaleX` and `scaleY` to `1`, preventing short cards from visually growing over tall targets. Reorder still uses the same `arrayMove`, sequential `sortOrder` normalization and move up/down controls.
- Text Area and Radio Button field creation use the same default field helper as the manual fallback.
- Move up/down controls remain available as a fallback and accessibility-friendly ordering path.
- Move up/down feedback is presentation-only: the moved card uses a short primary ring and soft directional motion, while the swapped neighbor uses neutral, lower-distance motion. Reduced-motion users receive highlight-only feedback.
- After drag/drop, move up/down or remove operations, the field list is normalized so `sortOrder` stays sequential.
- JSON preview and save payload are generated from the current field order, so persisted form definitions follow the visible order.
- Palette field creation, cursor ghost, authoritative insertion preview/index, no-preview/no-create guard, bottom insertion tolerance and drawer/FAB click-to-add remain unchanged. Existing move up/down, field editing, options, `RequiredWhen`, File Upload metadata and save/update/Runner payload behavior also remain unchanged.

## Responsive Designer And Palette

- The Designer layout is hardened for the 1920, 1440, 1366, 1240, 1100, 1024, 860, 768 and 640 viewport bands. Medium layouts avoid two-column panel squeezing; field, rule and option editors and their scoped input/select controls use safe wrapping and width containment.
- Form Designer content is centered within the workspace area without changing the global sidebar or app-shell layout. Drawer mode centers the form content without reserving inline palette width; desktop rail mode centers the combined canvas and rail.
- The desktop rail is viewport-height constrained. Its palette list scrolls independently while the separate save/update panel remains in the rail's visible bottom row, including after the File Upload palette item increased the list length.
- The right drawer is viewport-height constrained, scrolls its one-column themed item list and preserves backdrop, close, focus and body-scroll behavior. Drawer field creation is click-based for now.

## Supported Field Types

- `Text`: single-line text input; non-empty values must be strings on both frontend and backend.
- `TextArea`: multi-line text input, shown as Uzun metin in Turkish UI; it uses the same string-type rule as Text and a type failure is not reported as required.
- `Number`: numeric input with number conversion before process start.
- `Email`: email input with format validation.
- `Select`: option-based dropdown, shown with corrected Turkish copy such as Açılır seçim listesi.
- `Radio`: option-based single-choice field, shown to Turkish users as Seçenek düğmesi.
- `Checkbox`: boolean input that remains a boolean in submitted payloads.
- `Date`: date input with early frontend validation and authoritative backend JSON-string validation in exact `yyyy-MM-dd` format.
- `FileUpload`: single-file metadata selection with required, size, MIME and extension validation; it does not transfer or persist binary content.

Every palette card has an icon from the existing lucide-react dependency. The UI can show localized Turkish labels, while generated field keys and technical payload keys stay ASCII-safe.

Select and Radio share option validation across the designer and backend definition boundary: option sets cannot be null or empty, trimmed values cannot be empty, and duplicate values are rejected case-insensitively.

## UI/UX Notes

- Designer cards were widened and the field list now uses the available layout more effectively.
- Checkbox controls were adjusted to stay normal-sized and aligned with other inputs.
- Dependent validation and option areas were made more readable with scoped layout polish.
- Drag handles are visibly labeled so ordering is easier to discover during a demo.
- The field palette uses a 280-320px sticky right column on wide desktop layouts. Form Designer alone may widen to 1460px, while other workspace routes retain the standard 1180px content width.
- The palette is compact, centered within its rail and does not create its own unnecessary scrollbar. Save/update and its validation/status feedback live in a separate sticky action panel immediately below it.
- Saved-form changes use a panel-scoped translucent loading overlay. Initial designer and runner loads use the same shared skeleton language as Users and Communities, and the designer skeleton mirrors real field-card structure to reduce layout shift.
- The `/login` hydration fix keeps the ThemeToggleButton first server/client render stable while preserving normal theme switching after mount.
- Turkish UI copy was corrected for terms such as Satın Alma, Seçenek düğmesi and Açılır seçim listesi.
- RequiredWhen helper text was removed from every field card to keep the dependent validation area quieter while preserving the rule UI.
- Runner states now explain loading, empty list, validation-blocked submit, success and backend-error outcomes.
- The process-start payload panel is labeled for demo review and keeps submitted JSON visible.
- CSS changes were scoped to form designer/form runner classes and did not intentionally change global app behavior.

## Latest Form UX Polish

- Form Designer and Form Runner content is centered and width-balanced inside the workspace content area; the sidebar remains outside this calculation.
- Actionable Designer/Runner warnings use scoped, more visible panels with a short fade/translate highlight. Existing live-region semantics remain intact and reduced-motion preferences are respected.
- Manual `Add field` shows a short overlay/spinner on its own panel while preserving immediate field creation and input reset behavior. Starting a new form uses matching short feedback over the Form Information panel. The saved-form selection overlay remains unchanged.
- Palette-created fields still require a valid canvas or existing-field target. Returning to the palette, cancelling, dropping outside the designer or using another invalid target creates no field.
- Palette insertion feedback uses the same decorative, non-interactive preview at top, middle and bottom indices. Index resolution and field creation follow the authoritative-preview safeguards and the narrow last-field tolerance documented above.
- Existing field reorder, move up/down feedback, sequential `sortOrder` normalization, JSON preview order and save/update payload order remain aligned. The drag polish is limited to active-card presentation, existing-only collision selection and wrapper scale normalization; reorder handlers and persisted ordering behavior are unchanged.

## Validation Feedback

- Designer blocking feedback is derived from the existing field validation results instead of relying only on a generic save-blocked sentence. Summaries identify empty or duplicate keys, empty labels, missing/empty/duplicate Select or Radio options and invalid dependent `RequiredWhen` rules.
- The save panel shows a short localized heading, one concise line for a single problem, or at most three list items for multiple problems. Additional problems are summarized with `and X more errors` / `ve X hata daha`; field-level inline errors remain visible.
- Known API errors are mapped only at the presentation boundary. In particular, `A community is required for form definitions.` is shown in Turkish as `Form tanımı için bir topluluk seçilmelidir.` when Turkish is active. Unknown API messages keep the existing fallback behavior, and backend/API behavior is unchanged.

## Known Limitations / Deferred Items

- **Known follow-up:** Mixed-height existing-field drag visuals are improved; continue manual verification across browser and zoom combinations.
- **Known follow-up:** Drawer palette selection is click-to-add rather than drag/drop. This is accepted for now and can be researched in a separate isolated batch if required.
- **Known follow-up:** Real binary File Upload transfer, storage, endpoint and attachment-entity design remains deferred until the responsible stakeholders provide the required architecture and security decisions.
- **Known follow-up:** Continue manual regression coverage for Runner centering and loading feedback across browser and zoom combinations.

## Deferred Real Upload / Storage

Real file upload/storage was deliberately excluded from the foundation batch. It will be considered in a separate future batch only after the responsible stakeholders approve the storage, security and lifecycle design. Deferred work includes:

- Binary file transfer and a `multipart/form-data` endpoint.
- Attachment/file entities and disk, database or object-storage persistence.
- Attachment relationships across community, form field, process instance and uploader.
- Authorized download/view, delete and replace operations, plus audit logging.
- Permission-based file access and community isolation.
- Safe storage paths, path-traversal prevention, executable blocking, extension/MIME reconciliation and content-signature or magic-byte inspection.
- Virus scanning, quarantine, orphan-upload cleanup and retention policy.

Until that work is approved and implemented, File Upload must be described and treated only as metadata selection; it does not prove that a file was uploaded or retained.

The current JSON-safe value contains only `name`, `size`, MIME `type` and `lastModified`. Binary transfer, storage, upload endpoints and attachment entities will be decided only after the responsible stakeholders answer the pending architecture and security questions.

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
- `apps/web/src/features/app-shell/ThemeToggleButton.tsx`
- `apps/web/src/styles/forms.css`

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

- Login/session/auth logic. ThemeToggleButton received only the minimal stable-first-render hydration fix; theme behavior was not redesigned.
- Dashboard.
- App shell behavior beyond that hydration fix.
- Process state machine rules.
- Task approve/reject business logic.
- Audit log generation.
- Backend form/process behavior, validation logic and field helper definitions during the UX polish pass.
- Package files such as `package.json` and `package-lock.json`.
- New dependencies.
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

The latest Form Designer UX polish was verified with:

```bash
cd apps/web
npm run lint
npm run build
git diff --check
```

All checks passed. Lint still reports the same five pre-existing unused-symbol warnings in `ProcessListView.tsx`; that file was not changed by this branch.

The latest feedback, insertion-preview and validation-readability polish uses the same verification baseline. No backend, API endpoint, field-helper, auth/session, process/task/audit, package or dependency changes were part of that batch.

The File Upload foundation and API error-normalization work was verified with:

```bash
cd apps/web
npm run lint
npm run build

cd apps/api
dotnet build
dotnet test

git diff --check
```

Frontend lint/build, the File Upload foundation backend build/test suite and `git diff --check` passed. Lint still reports the five pre-existing unused-symbol warnings in `ProcessListView.tsx`; that file and those warnings were not changed in this scope. No package/lockfile or dependency changes were required.

## Latest Mobile And Navigation UX

- `Form Tasarimi` and `Form Baslat` keep their native routes but now appear under one permission-aware `Formlar` sidebar disclosure.
- Below the 1600 CSS-pixel rail breakpoint, the inline palette is replaced by a draggable circular trigger. The trigger remains viewport-constrained, snaps to the nearest horizontal edge and remembers a normalized vertical position for the device.
- The trigger opens an accessible right-side drawer. Selecting a field type appends it through the existing palette insertion function, closes the drawer and scrolls the new field into view.
- Desktop rail drag-and-drop, field-card reorder and move controls remain unchanged.
- Designer, runner and process-detail JSON now use the shared `JsonViewer`, which contains long values, limits vertical growth and provides copy plus expand/collapse actions.
