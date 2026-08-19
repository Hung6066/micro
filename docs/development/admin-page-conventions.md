# Admin Page Baseline

Use this convention for new admin list pages and create/edit dialogs. The Clients feature is the reference implementation.

## Page

- Use a standalone component with `ChangeDetectionStrategy.OnPush`.
- Use `HisHopeResourceState` or `AdminResourceStateController` for async data. Call `markForCheck()` after assigning ordinary component fields inside subscriptions or effects.
- Build `columns`, `bulkActions`, and `rows` as stable fields. Rebuild them only when locale, permissions, or API data changes; do not allocate them in template getters.
- Use `hh-page-layout`, `hh-page-header`, `hh-toolbar`, and `hh-data-table` for every list page.
- Keep API DTO values in `rows`. Use `HisHopeDataTableColumn.computed` for friendly foreign-key labels and `HisHopeI18nService.formatDate()` for locale-aware date/time output.
- Load each dialog's foreign-key catalog before opening it. Options must show a friendly label, never an ID.

## Dialog And Mutation

- Use `hh-create-dialog-shell`, `hh-form-layout`, and `hh-form-section`.
- Use one reactive `FormGroup` per dialog. Do not mix `[(ngModel)]` and reactive controls in the same form.
- Validate required fields and domain constraints in the client before submit; repeat validation server-side.
- In `save()`, call `markAllAsTouched()`, return when invalid or already saving, then create one immutable request payload.
- Existing template-driven CRUD dialogs must use a named `NgForm`, call `markAllAsTouched()`, block invalid or duplicate submits, and disable the save action while saving until they are migrated to a reactive `FormGroup`.
- Use separate success and error handlers for each mutation. A failed request must not emit success toast, audit feedback, or reload as a successful operation.
- Disable repeat submission while saving. For one-time secrets, disable the form and expose only copy and done actions after success.

## Verification

- Add focused tests for validation and failed mutations.
- Run `npm run build` for the admin app and `git diff --check` before deployment.
