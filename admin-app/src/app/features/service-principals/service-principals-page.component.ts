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
import { forkJoin } from "rxjs";
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
import {
  IamScope,
  IamWorkloadRole,
  PermissionDefinition,
} from "../../core/contracts/admin.contracts";
import { IamApiService } from "../../core/services/iam-api.service";
import { AdminResourceStateController } from "../../core/services/admin-resource-state.controller";
import { WorkloadRoleEditDialogComponent } from "../workload-roles/workload-role-edit-dialog.component";

import { HisHopeActionButtonComponent } from "@his-hope/frontend-foundation/ui";
@Component({
  selector: "app-service-principals-page",
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
      title="admin.servicePrincipals"
      titleFallback="Service principals"
      subtitle="admin.servicePrincipalsSubtitle"
      subtitleFallback="Non-human identities and workload credentials."
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
          (pressed)="edit(row)"
          kind="row"
          mode="icon-only"
          icon="edit"
          [label]="'admin.edit' | hhTranslate: 'Edit'"
        />
        <hh-action-button
          *ngIf="canWrite && row['isActive']"
          (pressed)="toggle(row)"
          kind="danger"
          mode="icon-only"
          icon="toggle_off"
          [label]="'admin.deactivate' | hhTranslate"
        />
        <hh-action-button
          *ngIf="canWrite && !row['isActive']"
          (pressed)="toggle(row)"
          kind="row"
          mode="icon-only"
          icon="toggle_on"
          [label]="'admin.activate' | hhTranslate"
        />
      </ng-template>
    </hh-resource-list-page>
  `,
})
export class ServicePrincipalsPageComponent implements OnInit {
  private readonly api = inject(IamApiService);
  private readonly dialog = inject(HisHopeDialogService);
  private readonly permissions = inject(HisHopePermissionService);
  private readonly i18n = inject(HisHopeI18nService);
  private readonly cdr = inject(ChangeDetectorRef);
  get canWrite(): boolean {
    return this.permissions.has("admin.roles.write");
  }
  roles: IamWorkloadRole[] = [];
  scopes: IamScope[] = [];
  permissionCatalog: PermissionDefinition[] = [];
  rows: Record<string, unknown>[] = [];
  private readonly destroyRef = inject(DestroyRef);
  readonly state = new AdminResourceStateController<{
    roles: IamWorkloadRole[];
    scopes: IamScope[];
    permissions: PermissionDefinition[];
  }>({
    destroyRef: this.destroyRef,
    i18n: this.i18n,
    loadErrorMessageKey: "admin.iamLoadFailed",
    loadErrorFallback: "Unable to load service principals.",
  });
  get loading(): boolean {
    return this.state.loading;
  }
  get error(): string {
    return this.state.error;
  }
  set error(value: string) {
    this.state.setActionError(value);
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
  }
  constructor() {
    effect(() => {
      const x = this.state.resource.data();
      if (x) {
        this.roles = x.roles;
        this.scopes = x.scopes;
        this.permissionCatalog = x.permissions;
        this.rows = x.roles.map((item) => ({
          ...item,
          isActive:
            (item as IamWorkloadRole & { isActive?: boolean }).isActive !==
            false,
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
        scopes: this.api.getIamScopes(),
        permissions: this.api.getPermissions(),
      }),
    );
  }
  openCreate(): void {
    if (this.canWrite)
      this.dialog
        .open(WorkloadRoleEditDialogComponent, {
          width: "min(840px, calc(100vw - 32px))",
          data: {
            role: null,
            scopes: this.scopes,
            permissions: this.permissionCatalog,
            servicePrincipal: true,
          },
        })
        .afterClosed()
        .subscribe((saved) => {
          if (saved) this.load();
        });
  }
  edit(row: Record<string, unknown>): void {
    if (!this.canWrite) return;
    const item = this.roles.find((x) => x.id === String(row["id"]));
    if (!item) return;
    this.dialog
      .open(WorkloadRoleEditDialogComponent, {
        width: "min(840px, calc(100vw - 32px))",
        data: {
          role: item,
          scopes: this.scopes,
          permissions: this.permissionCatalog,
          servicePrincipal: true,
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
    if (!id) return;
    const request = row["isActive"]
      ? this.api.deactivateIamWorkloadRole(id)
      : this.api.activateIamWorkloadRole(id);
    request.subscribe({
      next: () => this.load(),
      error: () =>
        (this.error = this.i18n.t(
          "admin.iamSaveFailed",
          "Unable to update service principal.",
        )),
    });
  }
}
