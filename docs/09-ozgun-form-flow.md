# Ozgun Form Flow

## Purpose And Ownership

Ozgun owns the versioned form definition, Form Designer, Form Runner, validation,
field ordering and workflow task-form binding. Workflow graph execution belongs
to Cagdas; access, navigation and community permission policy belong to Ufuk.

The full chronological implementation record is archived in
[Form Flow Implementation History](history/09-ozgun-form-flow-implementation-log.md).
This file describes only current behavior and extension boundaries.

## Form Lifecycle

- `FormDefinition` is the logical identity.
- `FormDefinitionVersion` contains editable draft or immutable published content.
- A version owns ordered `FormPage` records; pages own ordered fields and rules.
- Editing published content creates a new draft rather than changing a schema
  already used by a running process.
- Published versions may be archived but remain inspectable by pinned processes.
- A Start node binds a published start-form version. A User Task may bind a
  separate published task-form version.

The provider-neutral runtime contract is defined in
[Dynamic Workflow Contract](20-dynamic-workflow-contract.md).

## Designer

The Designer can create, load, update, publish and archive versioned forms. It
edits form metadata, pages, fields, ordering, options and dependent validation.

Supported field types:

- `Text`
- `TextArea`
- `Number`
- `Email`
- `Select`
- `Radio`
- `Checkbox`
- `Date`
- `FileUpload`

Field keys are non-empty and unique across the whole version. Labels are
required. Select/Radio options must be non-empty, trimmed and unique without
case sensitivity. Fields can be moved within one page or between pages.

Desktop ordering uses `@dnd-kit` with field-only collision handling and a visual
insertion indicator. Palette drops create a field only over a valid canvas/field
target; ambiguous or cancelled drops create nothing. Move-up/down controls remain
the keyboard-friendly alternative.

At wide desktop sizes, a sticky right palette rail remains visible. At narrower
or zoom-constrained widths, a draggable viewport-safe trigger opens the mobile
drawer; selecting an item appends it and scrolls it into view. The exact loading,
motion and responsive rules are owned by [UI And UX System](19-ui-ux-system.md).

## Runner

The Runner lists usable published form versions, builds initial values and
renders pages through shared field components. It validates the active page
before advancing and validates the complete payload before process start.

Submission behavior:

1. Convert number strings to numbers and preserve checkbox booleans.
2. Convert file selection to JSON-safe metadata.
3. Submit the exact form version with `formData` to a compatible published
   workflow version.
4. Keep backend validation authoritative.
5. On success, deep-link to the created process.

True first loads use a skeleton. Cached refresh keeps content and uses compact
feedback. Submit has an in-flight guard against double requests; reset rebuilds
the version's initial values. The shared `JsonViewer` contains long output,
supports copy and expand/collapse, and prevents horizontal page overflow.

## Validation Contract

Frontend validation exists for immediate UX; backend validation protects data
integrity and cannot be bypassed.

- Required values are checked independently from type errors.
- Text/TextArea values must be strings.
- Number values must be numeric.
- Email values must match email format.
- Date values must be exact `yyyy-MM-dd` JSON strings.
- Select/Radio values must belong to their declared option set.
- Checkbox remains boolean.
- `RequiredWhen` makes one field required when a referenced field equals a typed
  expected value; self-reference and incomplete rules are rejected.

Example process variable paths are `start.bonservis` and
`steps.financeApproval.onaylananButce`. The gateway picker derives these paths
from bound published forms; users do not write arbitrary expressions.

## File Metadata Boundary

`FileUpload` is intentionally metadata-only. The JSON value contains `name`,
`size`, MIME `type` and `lastModified`; no binary is uploaded or retained. The
current policy allows one file up to 10 MB and validates the documented
PDF/image/Office MIME and extension lists on both sides of the boundary.

Production binary support would require a separate multipart endpoint,
attachment entity, object storage, community-scoped authorization, signed
download, MIME/signature reconciliation, malware scanning, quarantine, cleanup
and retention rules. Until those exist, the product must not claim durable file
upload.

## Code Map

- `apps/web/src/features/form-designer`: Designer state, pages and drag/drop UI.
- `apps/web/src/features/form-runner`: Version selection, runner and process start.
- `apps/web/src/features/forms`: Shared field catalog, renderer, values and
  validation.
- `apps/api/src/TechYouthBpm.Infrastructure/Services/FormService.cs`: logical form
  CRUD and compatibility behavior.
- `apps/api/src/TechYouthBpm.Infrastructure/Services/FormVersionService.cs`:
  version lifecycle and immutable publication.
- `apps/api/src/TechYouthBpm.Infrastructure/Services/ProcessGraphValidator.cs`:
  published form binding and graph validation.

## Extension Rules

- Add a field type through the shared frontend catalog/renderer/value/validator,
  backend enum/definition/data validation and focused tests.
- Do not put workflow routing rules into the form renderer.
- Do not mutate a published version to implement an edit.
- Keep React drag/drop state out of API DTOs.
- Keep field-level feedback local and map known backend messages only at the
  presentation boundary.

## Known Limits

- Real binary upload/storage is deferred.
- Mobile palette selection is click-to-add; existing fields remain reorderable.
- Cross-browser/zoom drag geometry and real-device touch remain manual acceptance
  concerns until Playwright/device coverage is added.
- `FormDesignerDraft.tsx` remains physically large and should be split into page,
  field editor, palette and version-action modules without changing this contract.

## Verification

Automated coverage and commands are canonical in
[Testing And Quality Gates](24-testing-and-quality-gates.md). The cross-role
published-form and task-form chain is in
[Workflow End-to-End Test Scenarios](22-workflow-end-to-end-test-scenarios.md).
