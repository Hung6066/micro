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
  HisHopePageHeaderComponent,
  HisHopePageLayoutComponent,
  HisHopeToolbarComponent,
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
} from "../../core/contracts/admin.contracts";
import { IamApiService } from "../../core/services/iam-api.service";
import { AdminResourceStateController } from "../../core/services/admin-resource-state.controller";
import { iamScopeLabel } from "../../core/utils/iam-display.util";
import { WorkloadRoleEditDialogComponent } from "./workload-role-edit-dialog.component";

@Component({
  selector: "app-workload-roles-page",
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
    <hh-page-layout
      ><hh-page-header
        hhPageHeader
        [title]="'admin.workloadRoles' | hhTranslate: 'Workload roles'"
        [subtitle]="
          'admin.workloadRolesSubtitle'
            | hhTranslate: 'Issue and govern non-human workload sessions.'
        "
      />
      <hh-toolbar
        hhPageToolbar
        [label]="'admin.workloadRoles' | hhTranslate: 'Workload roles'"
        ><span hhToolbarTitle
          >{{ roles.length }}
          {{ "admin.workloadRoles" | hhTranslate: "Workload roles" }}</span
        ><hh-action-button
          *ngIf="canWrite"
          (pressed)="openCreate()"
          hh-toolbar-actions
          kind="primary"
          icon="add"
          [label]="'admin.create' | hhTranslate" /><hh-action-button
          hhToolbarActions
          kind="secondary"
          icon="refresh"
          [label]="'admin.refresh' | hhTranslate: 'Refresh'"
          (pressed)="load()"
      /></hh-toolbar>
      <div *ngIf="error" class="hh-state hh-state--error" role="alert">
        {{ error }}
      </div>
      <hh-data-table
        [label]="'admin.workloadRoles' | hhTranslate: 'Workload roles'"
        [columns]="columns"
        [rows]="rows"
        [loading]="loading"
        [empty]="!loading && !error && !rows.length"
        ><ng-template hhDataTableCell="actions" let-row
          ><hh-action-button
            *ngIf="canWrite"
            kind="row"
            mode="icon-only"
            icon="edit"
            [label]="'admin.edit' | hhTranslate"
            (pressed)="edit(row)" /><hh-action-button
            *ngIf="canWrite && row['isActive']"
            kind="danger"
            mode="icon-only"
            icon="toggle_off"
            [label]="'admin.deactivate' | hhTranslate"
            (pressed)="toggle(row)" /><hh-action-button
            *ngIf="canWrite && !row['isActive']"
            kind="row"
            mode="icon-only"
            icon="toggle_on"
            [label]="'admin.activate' | hhTranslate"
            (pressed)="toggle(row)" /></ng-template
      ></hh-data-table>
    </hh-page-layout>
  `,
})
export class WorkloadRolesPageComponent implements OnInit {
  private readonly api = inject(IamApiService);
  private readonly dialog = inject(MatDialog);
  private readonly permissionService = inject(HisHopePermissionService);
  private readonly i18n = inject(HisHopeI18nService);
  private readonly cdr = inject(ChangeDetectorRef);
  private readonly destroyRef = inject(DestroyRef);
  readonly state = new AdminResourceStateController<{
    roles: IamWorkloadRole[];
    scopes: IamScope[];
  }>({
    destroyRef: this.destroyRef,
    i18n: this.i18n,
    loadErrorMessageKey: "admin.iamLoadFailed",
    loadErrorFallback: "Unable to load workload roles.",
  });
  roles: IamWorkloadRole[] = [];
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
      const data = this.state.resource.data();
      if (data) {
        this.roles = data.roles;
        this.scopes = data.scopes;
        this.rows = data.roles.map((x) => ({
          ...x,
          scopeId: iamScopeLabel(x.scopeId, data.scopes),
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
        scopes: this.api.getIamScopes(),
      }),
    );
  }
  openCreate(): void {
    if (this.canWrite) {
      this.dialog
        .open(WorkloadRoleEditDialogComponent, {
          width: "680px",
          data: { role: null, scopes: this.scopes },
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
        data: { role: item, scopes: this.scopes },
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
