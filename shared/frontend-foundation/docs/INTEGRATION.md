# His.Hope Frontend Foundation Integration Guide

This guide is the integration contract for the three Angular applications:

- Clinical app: `src/Frontend/his-hope-app` (`8081`)
- Operations dashboard: `dashboard-app` (`8082`)
- Identity administration: `admin-app` (`8083`)

The foundation owns reusable UI, accessibility semantics, visual tokens, and interaction contracts. A feature owns domain models, HTTP calls, authorization decisions, audit persistence, and navigation.

## 1. Install And Bootstrap

For local workspace development, import from the workspace alias:

```ts
import {
  HisHopeDataTableComponent,
  HisHopePageHeaderComponent,
  HisHopePageLayoutComponent,
} from '@his-hope/frontend-foundation';
```

Add the shared styles once in the application build configuration or global stylesheet:

```scss
@use '@his-hope/frontend-foundation/styles' as hisHope;
```

Do not import `_tokens.scss` into individual feature components. The token layer and Material theme must be loaded once at application level. Feature styles may control domain layout, but must use foundation tokens for color, type, spacing, focus, radius, and control height.

The package is versioned. Before publishing, run:

```powershell
npm run release:check --workspace @his-hope/frontend-foundation
```

This runs the TypeScript build, package dry-run, and Storybook build.

## 2. Application Shell

Every application shell should render the shared brand, offline state, toast outlet, and workspace navigation:

```ts
imports: [
  HisHopeBrandComponent,
  HisHopeOfflineBannerComponent,
  HisHopeToastComponent,
  HisHopeTranslatePipe,
]
```

```html
<hh-offline-banner />
<hh-brand caption="Operations workspace" />
<router-outlet />
<hh-toast-outlet />
```

`hh-offline-banner` listens to browser `online` and `offline` events. It is a connectivity signal, not an offline write queue. A feature must still disable unsafe mutations or queue them explicitly.

## 2.1 Permission-aware actions

Populate the shared permission service from the authenticated `/me/permissions` response in each app shell. The service fails closed when no permission snapshot is available:

```ts
const permissions = inject(HisHopePermissionService);
permissions.setSnapshot(response);
permissions.clear(); // logout or session revocation
```

Use `hh-permission-button` for sensitive actions. The backend remains authoritative:

```html
<hh-permission-button requiredPermission="clients.write" (clicked)="rotateSecret()">
  Rotate secret
</hh-permission-button>
```

## 2.2 Performance telemetry

Report page and interaction timings through the vendor-neutral telemetry boundary:

```ts
const telemetry = inject(HisHopePerformanceTelemetryService);
telemetry.record('clients.table.load', durationMs);
telemetry.configure(metric => rum.record(metric.name, metric.duration));
telemetry.clear(); // optional: clear the in-memory diagnostic window
```

## 3. Page Composition

Use one page layout and one page header per feature screen:

```html
<hh-page-layout>
  <hh-page-header
    hhPageHeader
    title="Patients"
    subtitle="Manage the hospital patient registry">
    <button type="button" class="hh-button hh-button--primary" (click)="create()">
      <span class="material-icons" aria-hidden="true">add</span>
      New patient
    </button>
  </hh-page-header>

  <hh-filter-toolbar
    [search]="search"
    searchPlaceholder="Search by name or patient number"
    (searchChange)="onSearch($event)"
    (cleared)="clearFilters()" />

  <hh-data-table ... />
</hh-page-layout>
```

Page state must have four explicit states: loading, error with retry, empty with an action where useful, and ready. Do not show an empty table while the first request is still pending.

## 4. DataTable Integration

### 4.1 Client-side table

```ts
readonly columns: HisHopeDataTableColumn[] = [
  { key: 'name', label: 'Name', sortable: true },
  { key: 'status', label: 'Status', sortable: true },
  { key: 'actions', label: 'Actions', hideable: false, align: 'end' },
];

rows = [
  { id: 'p-1', name: 'Nguyen An', status: 'Active' },
];
```

```html
<hh-data-table
  label="Patients"
  [columns]="columns"
  [rows]="rows"
  [selection]="true"
  [loading]="loading"
  [error]="error"
  [empty]="!loading && !error && rows.length === 0"
  emptyMessage="No patients found."
  (sortChange)="onSort($event)"
  (selectionChange)="selected = $event"
  (retry)="reload()" />
```

Each row must have a stable `id` or `key`. Selection is keyed by that value. Project domain cells with `hhDataTableCell` rather than duplicating the table shell:

```html
<ng-template hhDataTableCell="status" let-row>
  <hh-status-badge [status]="row.status" />
</ng-template>
```

### 4.2 Server-side pagination, sorting, and filtering

Use `mode="server"`. The table never performs HTTP requests itself. It emits a `HisHopePageQuery`; the feature calls its API and replaces `rows` and `totalItems`.

```ts
query: HisHopePageQuery = { page: 1, pageSize: 20 };
rows: Record<string, unknown>[] = [];
totalItems = 0;

onQueryChange(query: HisHopePageQuery): void {
  this.query = query;
  this.loading = true;
  this.api.searchPatients(query).subscribe({
    next: result => {
      this.rows = result.items;
      this.totalItems = result.totalCount;
      this.loading = false;
    },
    error: () => {
      this.error = 'Unable to load patients.';
      this.loading = false;
    },
  });
}
```

```html
<hh-data-table
  mode="server"
  [query]="query"
  [rows]="rows"
  [totalItems]="totalItems"
  [columns]="columns"
  [loading]="loading"
  (queryChange)="onQueryChange($event)" />
```

`HisHopePageQuery` supports `page`, `pageSize`, `sort`, `search`, `filters`, and structured `filterItems`. Map this contract to the backend query DTO; do not concatenate query strings manually.

### 4.3 Inline editing

```ts
columns = [
  {
    key: 'displayName',
    label: 'Display name',
    editable: true,
    editor: 'text',
    editValidator: (value: unknown) =>
      String(value).trim().length < 2 ? 'Enter at least two characters.' : null,
  },
  {
    key: 'clientType',
    label: 'Type',
    editable: true,
    editor: 'select',
    options: [
      { value: 'spa', label: 'SPA' },
      { value: 'web', label: 'Web' },
    ],
  },
];
```

```html
<hh-data-table
  [inlineEdit]="true"
  [columns]="columns"
  [rows]="rows"
  [savingRowKey]="savingRowKey"
  [editState]="editState"
  (rowEditSaveRequested)="saveRow($event)"
  (rowEditCancel)="cancelRowEdit()"
  (rowEditUndo)="undoRow($event)" />
```

The feature owns persistence. Set `savingRowKey` while the request is pending and set `editState` to `error` when the server rejects the update. `rowEditUndo` is a domain event; implement rollback only when the API and audit policy support it.

### 4.4 Bulk actions and export

```ts
bulkActions: HisHopeBulkAction[] = [
  { id: 'deactivate', label: 'Deactivate', tone: 'danger' },
];

onBulk(request: HisHopeBulkActionRequest): void {
  this.api.bulkUpdate(request.actionId, request.rowKeys, request.query)
    .subscribe(() => this.reload());
}

onExport(request: HisHopeTableExportRequest): void {
  this.api.export(request).subscribe(file => download(file));
}
```

```html
<hh-data-table
  [selection]="true"
  [bulkActions]="bulkActions"
  [exportable]="true"
  [exportFormats]="['csv', 'xlsx']"
  (bulkActionRequested)="onBulk($event)"
  (exportRequested)="onExport($event)" />
```

Bulk and export events expose `rowKeys`, not the full row objects. The feature API must re-authorize those keys server-side and reload the minimum required data. Never trust client-side permissions or use client-provided row data as the source of truth for PHI operations. In server mode, the selected rows are limited to rows loaded in the current page. For cross-page selection, use the selection contract and pass the selected IDs/query to the API.

### 4.5 Column preferences

Use `columnOrder` and listen to `columnOrderChange` and `columnResizeChange`. Persist preferences by user and screen; never persist them globally across unrelated tables.

## 5. Forms And State

Use `hh-form-field` for label, hint, required marker, validation error, disabled, and dirty presentation:

```html
<hh-form-field
  controlId="email"
  label="Email address"
  hint="Use your hospital email"
  [required]="true"
  error="Email is invalid">
  <input id="email" type="email" aria-describedby="email-hint email-error" />
</hh-form-field>
```

Use `hh-form-layout` to control form sections and responsive columns. A form action must expose pending, success, and failure states and must not silently discard dirty values.

## 6. Theme And Typography

Inject `HisHopeThemeService` once in the application shell:

```ts
private readonly theme = inject(HisHopeThemeService);

setDarkMode(enabled: boolean): void {
  this.theme.setTheme(enabled ? 'dark' : 'light');
}

setHighContrast(enabled: boolean): void {
  this.theme.setHighContrast(enabled);
}
```

Supported themes are `light`, `dark`, and `system`. High contrast is independent and uses `data-contrast="high"`. Feature CSS must use variables such as `--text-primary`, `--surface-white`, `--color-primary`, `--space-4`, and `--font-size-body`; no feature-level hex colors or arbitrary font stacks.

## 7. Internationalization

The foundation includes two built-in dictionaries: `vi-VN` and `en`. Configure the service at application startup or after the authenticated user profile is loaded:

```ts
const i18n = inject(HisHopeI18nService);
i18n.setLocale('vi-VN'); // or i18n.setLocale('en')
```

Use the shared language menu in the application shell. It includes compact locale
icons and can be extended without changing the component:

```ts
locales = [
  { code: 'vi-VN', label: 'Tiếng Việt', icon: 'VI' },
  { code: 'en', label: 'English', icon: 'EN' },
  { code: 'ja', label: '日本語', icon: 'JA' },
];

jaDictionary = { common: { save: '保存' } };
i18n.registerLocale('ja', jaDictionary);
```

```html
<hh-language-switcher [locales]="locales" />
```

Register a dictionary before selecting its locale. The selected locale is persisted
in `localStorage` under `hh-locale`; the built-in dictionaries are `vi-VN` and `en`.
The `icon` value is intentionally configurable, so a project can use a locale code,
flag glyph, or another short accessible label while keeping the same interaction.

To extend a built-in dictionary with application-specific keys, pass a custom dictionary to `configure`:

```ts
i18n.configure('vi-VN', {
  ...hisHopeViVN,
  patients: { title: 'Bệnh nhân' },
});
```

Use the pipe for visible UI text:

```html
<h1>{{ 'patients.title' | hhTranslate:'Patients' }}</h1>
<p>{{ 'table.empty' | hhTranslate:'No records found.' }}</p>
```

Fallback text is required so a missing translation never renders an empty control. Keep API error codes separate from translated presentation messages.

## 8. Permissions And Sensitive Actions

Permission checks are presentation checks only. The backend must authorize every request.

```html
<hh-permission-button
  [permissions]="currentPermissions"
  [requiredPermissions]="['client.write', 'client.delete']"
  permissionMode="all"
  [showDenied]="true"
  (clicked)="deleteClient()">
  Delete client
</hh-permission-button>
```

Use `permissionMode="any"` for alternative capabilities. Use `showDenied` when discoverability is important; otherwise the action remains hidden. Destructive actions still require `hh-confirm-dialog`.

## 9. Toast And Audit Feedback

Render one `<hh-toast-outlet />` in the shell and inject `HisHopeToastService` for ordinary transient feedback:

```ts
private readonly toast = inject(HisHopeToastService);
this.toast.success('Client updated.');
this.toast.error('Unable to update client.');
```

For sensitive actions, also use `HisHopeAuditFeedbackService`:

```ts
private readonly audit = inject(HisHopeAuditFeedbackService);

this.audit.report({
  action: 'Delete',
  resource: 'OIDC client',
  resourceId: client.id,
  outcome: 'success',
  message: 'Client deleted.',
});
```

The service gives immediate user feedback. Durable audit persistence remains a backend concern and should happen before reporting success.

## 10. Command Palette

```html
<button type="button" (click)="paletteOpen = true" aria-label="Open command palette">
  <span class="material-icons" aria-hidden="true">search</span>
</button>
<hh-command-palette
  [open]="paletteOpen"
  [commands]="commands"
  (selected)="runCommand($event)"
  (closed)="paletteOpen = false" />
```

Commands should navigate to a safe route or invoke a permission-checked action. The component handles search, Escape, focus trap, and restore focus; it does not perform navigation.

## 11. Navigation Components

- `hh-drawer`: modal navigation or detail panel; handles Escape, focus trap, and focus restoration.
- `hh-popover`: lightweight contextual surface; close on Escape and restore trigger focus.
- `hh-tabs`: projected elements with `role="tab"`; ArrowLeft/ArrowRight/Home/End move focus.
- `hh-breadcrumb`: navigation landmark; projected list items should contain real links.
- `hh-workspace-header`: consistent workspace context and session affordances.

Do not nest interactive controls inside a button, tab, or summary element. Every icon-only control needs an accessible label.

## 12. Accessibility And Visual CI

Run the shared foundation gate from the repository root:

```powershell
npm run test:a11y
npm run test:visual
```

The Playwright suite in `tests/e2e/specs/shared-foundation.spec.js` runs axe against all three ports and compares desktop shell screenshots. Update snapshots only after deliberate visual review:

```powershell
npm --prefix tests/e2e run test:shared-foundation -- --update-snapshots
```

The command-palette interaction test requires authenticated dashboard storage state. CI must provision that state using the existing auth setup before enabling the non-skipped interaction gate.

## 13. Migration Order For Existing Pages

1. Replace page shell with `hh-page-layout` and `hh-page-header`.
2. Replace local loading/error/empty markup with `hh-state` or DataTable inputs.
3. Replace custom table with `hh-data-table` and map the API result to `HisHopePageResult`.
4. Move search/filter state into `HisHopePageQuery`.
5. Replace local permission buttons, snackbar messages, and destructive dialogs.
6. Replace visible hard-coded text with `hhTranslate` keys.
7. Remove feature CSS only after desktop, tablet, mobile, dark, and high-contrast review.
8. Add one Storybook or Playwright interaction story for every new state.

## 14. Ownership And Compatibility

Foundation changes must preserve selector, input, output, token, and ARIA behavior unless the compatibility document and changelog are updated. Feature teams must not fork a shared component to add domain behavior; add a typed input/output contract or keep the behavior in the feature adapter.

Current known boundary: the foundation emits server query, bulk, export, and audit events, but it does not call application APIs. Every consuming page must provide the adapter and authorization logic.
