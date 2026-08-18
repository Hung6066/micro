import {
  ChangeDetectorRef,
  Component,
  DestroyRef,
  OnInit,
  effect,
  inject,
} from "@angular/core";
import { CommonModule } from "@angular/common";
import { MatDialog, MatDialogModule } from "@angular/material/dialog";
import { forkJoin } from "rxjs";
import {
  HisHopeDataTableCellDirective,
  HisHopeDataTableColumn,
  HisHopeDataTableComponent,
  HisHopeToolbarComponent,
  HisHopePageHeaderComponent,
  HisHopePageLayoutComponent,
} from "@his-hope/frontend-foundation/ui";
import {
  HisHopeI18nService,
  HisHopeTranslatePipe,
} from "@his-hope/frontend-foundation/i18n";
import { HisHopePermissionService } from "@his-hope/frontend-foundation/auth";
import {
  IamScope,
  IamWorkloadRole,
} from "../../core/contracts/admin.contracts";
import { IamApiService } from "../../core/services/iam-api.service";
import { AdminResourceStateController } from "../../core/services/admin-resource-state.controller";
import { iamScopeLabel } from "../../core/utils/iam-display.util";
import { WorkloadRoleEditDialogComponent } from "../workload-roles/workload-role-edit-dialog.component";

import { HisHopeActionButtonComponent } from "@his-hope/frontend-foundation/ui";
@Component({
  selector: "app-service-principals-page",
  standalone: true,
  imports: [
    HisHopeActionButtonComponent,
    CommonModule,
    MatDialogModule,
    HisHopeDataTableCellDirective,
    HisHopeDataTableComponent,
    HisHopePageHeaderComponent,
    HisHopePageLayoutComponent,
    HisHopeToolbarComponent,
    HisHopeTranslatePipe,
  ],
  template: `
    <hh-page-layout>
      <hh-page-header
        hhPageHeader
        [title]="'admin.servicePrincipals' | hhTranslate: 'Service principals'"
        [subtitle]="
          'admin.servicePrincipalsSubtitle'
            | hhTranslate: 'Non-human identities and workload credentials.'
        "
      />
      <hh-toolbar
        hhPageToolbar
        [label]="'admin.servicePrincipals' | hhTranslate: 'Service principals'"
      >
        <span hhToolbarTitle
          >{{ roles.length }}
          {{
            "admin.servicePrincipals" | hhTranslate: "Service principals"
          }}</span
        >
        <hh-action-button
          *ngIf="canWrite"
          (pressed)="openCreate()"
          hh-toolbar-actions
          kind="primary"
          icon="add"
          [label]="'admin.create' | hhTranslate"
        />
        <hh-action-button
          (pressed)="load()"
          hh-toolbar-actions
          kind="secondary"
          icon="refresh"
          [label]="'admin.refresh' | hhTranslate"
        />
      </hh-toolbar>
      <div *ngIf="error" class="hh-state hh-state--error" role="alert">
        {{ error }}
      </div>
      <hh-data-table
        [label]="'admin.servicePrincipals' | hhTranslate: 'Service principals'"
        [columns]="columns"
        [rows]="rows"
        [loading]="loading"
        [empty]="!loading && !error && !rows.length"
      >
        <ng-template hhDataTableCell="actions" let-row>
          <hh-action-button
            *ngIf="canWrite"
            (pressed)="edit(row)"
            kind="secondary"
            mode="icon-only"
            icon="edit"
            label="Action"
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
      </hh-data-table>
    </hh-page-layout>
  `,
})
export class ServicePrincipalsPageComponent implements OnInit {
  private readonly api = inject(IamApiService);
  private readonly dialog = inject(MatDialog);
  private readonly permissions = inject(HisHopePermissionService);
  private readonly i18n = inject(HisHopeI18nService);
  private readonly cdr = inject(ChangeDetectorRef);
  get canWrite(): boolean {
    return this.permissions.has("admin.roles.write");
  }
  roles: IamWorkloadRole[] = [];
  scopes: IamScope[] = [];
  rows: Record<string, unknown>[] = [];
  private readonly destroyRef = inject(DestroyRef);
  readonly state = new AdminResourceStateController<{
    roles: IamWorkloadRole[];
    scopes: IamScope[];
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
      { key: "scopeId", label: this.i18n.t("admin.scopeId", "Scope") },
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
        this.rows = x.roles.map((item) => ({
          ...item,
          scopeId: iamScopeLabel(item.scopeId, x.scopes),
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
      }),
    );
  }
  openCreate(): void {
    if (this.canWrite)
      this.dialog
        .open(WorkloadRoleEditDialogComponent, {
          width: "680px",
          data: { role: null, scopes: this.scopes, servicePrincipal: true },
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
        width: "680px",
        data: { role: item, scopes: this.scopes, servicePrincipal: true },
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
