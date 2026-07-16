# Dynamic Workflow Rebase Notes

The dynamic workflow branch was rebased onto the current `master` after Ozgun's responsive form palette, drag stability, field reorder and runner layout updates.

## Conflict Decisions

- `FormDesignerDraft.tsx`: the versioned, multi-page designer remains the structural base. The current master palette positioning, drag threshold, invalid-drop protection, drag preview and reorder feedback are reapplied to that structure.
- `FormRunnerDraft.tsx`: version-aware workflow selection and process start behavior remain. The current master runner hierarchy, selection summary and responsive layout are retained.
- `forms.css`: current master responsive and palette rules take precedence; styles required only by versioning, multi-page forms and workflow binding are layered on top.
- `docs/19-ui-ux-system.md`: the current 1400px centered workspace, 236px default sidebar, 232px scaled-desktop sidebar and 1440px sticky-palette breakpoint replace the older 1180px/1460px notes.

No API contract, form version, workflow binding or task-form behavior was intentionally removed. Frontend tests, lint/build and backend workflow tests must pass after the resolution.
