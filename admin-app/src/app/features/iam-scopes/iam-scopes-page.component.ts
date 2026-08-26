import {
  ChangeDetectorRef,
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  OnInit,
  effect,
  inject,
} from "@angular/core";
import { CommonModule } from "@angular/common";
import { HisHopeDialogService } from "@his-hope/frontend-foundation/ui";
import {
  HisHopeDataTableColumn,
  HisHopeResourceListPageComponent,
  HisHopeResourceRowActionsDirective,
} from "@his-hope/frontend-foundation/ui";
import {
  HisHopeI18nService,
  HisHopeTranslatePipe,
} from "@his-hope/frontend-foundation/i18n";
import { HisHopePermissionService } from "@his-hope/frontend-foundation/auth";
import { IamScope } from "../../core/contracts/admin.contracts";
import { IamApiService } from "../../core/services/iam-api.service";
import { TenantContextService } from "../../core/services/tenant-context.service";
import { AdminResourceStateController } from "../../core/services/admin-resource-state.controller";
import { takeUntilDestroyed } from "@angular/core/rxjs-interop";
import { map } from "rxjs";

import { HisHopeActionButtonComponent } from "@his-hope/frontend-foundation/ui";
import { IamScopeEditDialogComponent } from "./iam-scope-edit-dialog.component";
@Component({
  selector: "app-iam-scopes-page",
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    HisHopeActionButtonComponent,
    CommonModule,
    HisHopeResourceListPageComponent,
    HisHopeResourceRowActionsDirective,
    HisHopeTranslatePipe,
  ],
  template: `
    <hh-resource-list-page
      title="admin.organizationsTenants"
      titleFallback="Organizations & tenants"
      subtitle="admin.scopesSubtitle"
      subtitleFallback="Manage organization, tenant, account and environment boundaries."
      countLabel="admin.scopes"
      countLabelFallback="Scopes"
      tableLabel="admin.scopes"
      tableLabelFallback="Scopes"
      [count]="scopes.length"
      [canWrite]="canWrite"
      [columns]="columns"
      [rows]="rows"
      [loading]="loading"
      [error]="error"
      (create)="openCreate()"
      (refresh)="load()"
    >
      <ng-template hhResourceRowActions let-row>
        <hh-action-button
          *ngIf="canWrite"
          kind="row"
          mode="icon-only"
          icon="edit"
          [label]="'admin.edit' | hhTranslate"
          (pressed)="edit(row)"
        />
        <hh-action-button
          *ngIf="canWrite && row['isActive']"
          kind="danger"
          mode="icon-only"
          icon="toggle_off"
          [label]="'admin.deactivate' | hhTranslate"
          (pressed)="toggle(row)"
        />
        <hh-action-button
          *ngIf="canWrite && !row['isActive']"
          kind="row"
          mode="icon-only"
          icon="toggle_on"
          [label]="'admin.activate' | hhTranslate"
          (pressed)="toggle(row)"
        />
      </ng-template>
    </hh-resource-list-page>
  `,
})
export class IamScopesPageComponent implements OnInit {
  private readonly api = inject(IamApiService);
  private readonly tenantContext = inject(TenantContextService);
  private readonly dialog = inject(HisHopeDialogService);
  private readonly permissions = inject(HisHopePermissionService);
  private readonly i18n = inject(HisHopeI18nService);
  private readonly cdr = inject(ChangeDetectorRef);
  get canWrite(): boolean {
    return this.permissions.has("admin.roles.write");
  }
  private readonly destroyRef = inject(DestroyRef);
  readonly state = new AdminResourceStateController<IamScope[]>({
    destroyRef: this.destroyRef,
    i18n: this.i18n,
    loadErrorMessageKey: "admin.iamLoadFailed",
    loadErrorFallback: "Unable to load scopes.",
  });
  scopes: IamScope[] = [];
  rows: Record<string, unknown>[] = [];
  saving = false;
  get loading(): boolean {
    return this.state.loading;
  }
  get error(): string {
    return this.state.error;
  }
  set error(value: string) {
    this.state.setActionError(value);
  }
  get parentOptions(): IamScope[] {
    return this.scopes.filter((x) => x.isActive);
  }
  get columns(): HisHopeDataTableColumn[] {
    this.i18n.locale();
    return [
      { key: "key", label: this.i18n.t("admin.key", "Key"), sortable: true },
      {
        key: "displayName",
        label: this.i18n.t("admin.displayName", "Display name"),
      },
      { key: "kind", label: this.i18n.t("admin.kind", "Kind") },
      {
        key: "parentId",
        label: this.i18n.t("admin.parentScope", "Parent scope"),
        format: {
          type: "friendlyReference",
          references: this.scopes,
          includeKind: true,
        },
      },
      { key: "isActive", label: this.i18n.t("admin.active", "Active") },
      {
        key: "actions",
        label: this.i18n.t("admin.actions", "Actions"),
        sortable: false,
        hideable: false,
      },
    ];
  }
  ngOnInit(): void {
    this.load();
    this.tenantContext.bindTenantReload(this.destroyRef, () => this.load());
  }
  constructor() {
    effect(() => {
      const scopes = this.state.resource.data();
      if (scopes) {
        this.scopes = scopes;
        this.rows = scopes.map((item) => ({ ...item }));
        this.cdr.markForCheck();
      }
    });
  }
  load(): void {
    this.state.load(
      this.api.getIamScopes().pipe(map((scopes) => this.tenantContext.filterScopes(scopes))),
    );
  }
  openCreate(): void {
    if (!this.canWrite) return;
    this.dialog
      .open(IamScopeEditDialogComponent, {
        width: "560px",
        data: { scope: null, scopes: this.scopes },
      })
      .afterClosed()
      .subscribe((saved) => {
        if (saved) this.load();
      });
  }
  edit(row: Record<string, unknown>): void {
    if (!this.canWrite) return;
    const item = this.scopes.find((x) => x.id === String(row["id"]));
    if (!item) return;
    this.dialog
      .open(IamScopeEditDialogComponent, {
        width: "560px",
        data: { scope: item, scopes: this.scopes },
      })
      .afterClosed()
      .subscribe((saved) => {
        if (saved) this.load();
      });
  }
  toggle(row: Record<string, unknown>): void {
    if (!this.canWrite) return;
    const id = String(row["id"] ?? "");
    const call = row["isActive"]
      ? this.api.deactivateIamScope(id)
      : this.api.activateIamScope(id);
    call.subscribe({
      next: () => this.load(),
      error: () =>
        (this.error = this.i18n.t(
          "admin.iamSaveFailed",
          "Unable to update scope.",
        )),
    });
  }
}
