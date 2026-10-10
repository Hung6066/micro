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
import { forkJoin, map } from "rxjs";
import {
  HisHopeDataTableColumn,
  HisHopeResourceListPageComponent,
  HisHopeResourceRowActionsDirective,
} from "@his-hope/frontend-foundation/ui";
import { HisHopeActionButtonComponent } from "@his-hope/frontend-foundation/ui";
import {
  HisHopeI18nService,
  HisHopeTranslatePipe,
} from "@his-hope/frontend-foundation/i18n";
import { HisHopePermissionService } from "@his-hope/frontend-foundation/auth";
import {
  IamScope,
  IamWorkloadRole,
  PermissionDefinition,
} from "../../core/contracts/admin.contracts";
import { IamApiService } from "../../core/services/iam-api.service";
import { TenantContextService } from "../../core/services/tenant-context.service";
import { AdminResourceStateController } from "../../core/services/admin-resource-state.controller";
import { WorkloadRoleEditDialogComponent } from "./workload-role-edit-dialog.component";

@Component({
  selector: "app-workload-roles-page",
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
      title="admin.workloadRoles"
      titleFallback="Workload roles"
      subtitle="admin.workloadRolesSubtitle"
      subtitleFallback="Issue and govern non-human workload sessions."
      refreshLabelFallback="Refresh"
      [count]="roles.length"
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
export class WorkloadRolesPageComponent implements OnInit {
  private readonly api = inject(IamApiService);
  private readonly tenantContext = inject(TenantContextService);
  private readonly dialog = inject(HisHopeDialogService);
  private readonly permissionService = inject(HisHopePermissionService);
  private readonly i18n = inject(HisHopeI18nService);
  private readonly cdr = inject(ChangeDetectorRef);
  private readonly destroyRef = inject(DestroyRef);
  readonly state = new AdminResourceStateController<{
    roles: IamWorkloadRole[];
    scopes: IamScope[];
    permissions: PermissionDefinition[];
  }>({
    destroyRef: this.destroyRef,
    i18n: this.i18n,
    loadErrorMessageKey: "admin.iamLoadFailed",
    loadErrorFallback: "Unable to load workload roles.",
  });
  roles: IamWorkloadRole[] = [];
  scopes: IamScope[] = [];
  permissions: PermissionDefinition[] = [];
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
  get canWrite(): boolean {
    return this.permissionService.has("admin.roles.write");
  }
  get columns(): HisHopeDataTableColumn[] {
    this.i18n.locale();
    return [
      { key: "key", label: this.i18n.t("admin.key", "Key"), sortable: true },
      {
        key: "displayName",
        label: this.i18n.t("admin.displayName", "Display name"),
      },
      { key: "audience", label: this.i18n.t("admin.audience", "Audience") },
      {
        key: "scopeId",
        label: this.i18n.t("admin.scopeId", "Scope"),
        format: { type: "friendlyReference", references: this.scopes },
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
      const data = this.state.resource.data();
      if (data) {
        this.roles = data.roles;
        this.scopes = data.scopes;
        this.permissions = data.permissions;
        this.rows = data.roles.map((x) => ({
          ...x,
          isActive:
            (x as IamWorkloadRole & { isActive?: boolean }).isActive !== false,
          principalType: "workload",
        }));
        this.cdr.markForCheck();
      }
    });
  }
  load(): void {
    this.state.load(
      forkJoin({
        roles: this.api.getIamWorkloadRoles(),
        scopes: this.api.getIamScopes().pipe(
          map((scopes) => this.tenantContext.filterScopes(scopes)),
        ),
        permissions: this.api.getPermissions(),
      }),
    );
  }
  openCreate(): void {
    if (this.canWrite) {
      this.dialog
        .open(WorkloadRoleEditDialogComponent, {
          width: "680px",
          data: {
            role: null,
            scopes: this.scopes,
            permissions: this.permissions,
          },
        })
        .afterClosed()
        .subscribe((saved) => {
          if (saved) this.load();
        });
    }
  }
  edit(row: Record<string, unknown>): void {
    const item = this.roles.find((x) => x.id === String(row["id"]));
    if (!item || !this.canWrite) return;
    this.dialog
      .open(WorkloadRoleEditDialogComponent, {
        width: "680px",
        data: {
          role: item,
          scopes: this.scopes,
          permissions: this.permissions,
        },
      })
      .afterClosed()
      .subscribe((saved) => {
        if (saved) this.load();
      });
  }
  toggle(row: Record<string, unknown>): void {
    if (!this.canWrite) return;
    const id = String(row["id"] ?? "");
    (row["isActive"]
      ? this.api.deactivateIamWorkloadRole(id)
      : this.api.activateIamWorkloadRole(id)
    ).subscribe({ next: () => this.load(), error: () => this.fail() });
  }
  private fail(): void {
    this.error = this.i18n.t(
      "admin.iamLoadFailed",
      "Unable to load workload roles.",
    );
    this.saving = false;
    this.cdr.markForCheck();
  }
}
