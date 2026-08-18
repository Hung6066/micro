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
import { catchError, forkJoin, of } from "rxjs";
import {
  HisHopeDataTableCellDirective,
  HisHopeDataTableColumn,
  HisHopeDataTableComponent,
  HisHopePageHeaderComponent,
  HisHopePageLayoutComponent,
  HisHopeToolbarComponent,
} from "@his-hope/frontend-foundation/ui";
import {
  HisHopeI18nService,
  HisHopeTranslatePipe,
} from "@his-hope/frontend-foundation/i18n";
import { HisHopePermissionService } from "@his-hope/frontend-foundation/auth";
import {
  IamPermissionBoundary,
  IamScope,
  IamWorkloadRole,
  PermissionDefinition,
  User,
} from "../../core/contracts/admin.contracts";
import { IamApiService } from "../../core/services/iam-api.service";
import { AdminResourceStateController } from "../../core/services/admin-resource-state.controller";
import { BoundaryEditDialogComponent } from "./boundary-edit-dialog.component";

import { HisHopeActionButtonComponent } from "@his-hope/frontend-foundation/ui";
@Component({
  selector: "app-boundaries-page",
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
  template: ` <hh-page-layout
    ><hh-page-header
      hhPageHeader
      [title]="'admin.boundaries' | hhTranslate: 'Permission boundaries'"
      [subtitle]="
        'admin.boundariesSubtitle'
          | hhTranslate
            : 'Limit the maximum permissions a principal can receive.'
      " /><hh-toolbar
      hhPageToolbar
      [label]="'admin.boundaries' | hhTranslate: 'Permission boundaries'"
      ><span hhToolbarTitle
        >{{ boundaries.length }} {{ "admin.boundaries" | hhTranslate }}</span
      ><hh-action-button
        *ngIf="canWrite"
        hh-toolbar-actions
        kind="primary"
        icon="add"
        [label]="'admin.create' | hhTranslate"
        (pressed)="openCreate()" /><hh-action-button
        hh-toolbar-actions
        kind="secondary"
        icon="refresh"
        [label]="'admin.refresh' | hhTranslate"
        (pressed)="load()"
    /></hh-toolbar>
    <div *ngIf="error" class="hh-state hh-state--error" role="alert">
      {{ error }}
    </div>
    <hh-data-table
      [label]="'admin.boundaries' | hhTranslate: 'Permission boundaries'"
      [columns]="columns"
      [rows]="rows"
      [loading]="loading"
      [empty]="!loading && !error && !rows.length"
      ><ng-template hhDataTableCell="actions" let-row
        ><hh-action-button
          *ngIf="canWrite && row['isActive']"
          (pressed)="toggle(row)"
          kind="danger"
          mode="icon-only"
          icon="toggle_off"
          [label]="
            'admin.deactivate' | hhTranslate
          " /></ng-template></hh-data-table
  ></hh-page-layout>`,
})
export class BoundariesPageComponent implements OnInit {
  private readonly api = inject(IamApiService);
  private readonly dialog = inject(MatDialog);
  private readonly permissions = inject(HisHopePermissionService);
  private readonly i18n = inject(HisHopeI18nService);
  private readonly cdr = inject(ChangeDetectorRef);
  get canWrite(): boolean {
    return this.permissions.has("admin.roles.write");
  }
  boundaries: IamPermissionBoundary[] = [];
  scopes: IamScope[] = [];
  users: User[] = [];
  workloadRoles: IamWorkloadRole[] = [];
  permissionsCatalog: PermissionDefinition[] = [];
  rows: Record<string, unknown>[] = [];
  private readonly destroyRef = inject(DestroyRef);
  readonly state = new AdminResourceStateController<{
    boundaries: IamPermissionBoundary[];
    scopes: IamScope[];
    users: User[];
    workloadRoles: IamWorkloadRole[];
    permissions: PermissionDefinition[];
  }>({
    destroyRef: this.destroyRef,
    i18n: this.i18n,
    loadErrorMessageKey: "admin.iamLoadFailed",
    loadErrorFallback: "Unable to load boundaries.",
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
  constructor() {
    effect(() => {
      const data = this.state.resource.data();
      if (data) {
        this.boundaries = data.boundaries;
        this.scopes = data.scopes;
        this.users = data.users;
        this.workloadRoles = data.workloadRoles;
        this.permissionsCatalog = data.permissions.filter(
          (item) => !item.isDeprecated,
        );
        this.rows = data.boundaries.map((item) => ({ ...item }));
        this.cdr.markForCheck();
      }
    });
  }
  get columns(): HisHopeDataTableColumn[] {
    this.i18n.locale();
    return [
      {
        key: "principalType",
        label: this.i18n.t("admin.principalType", "Principal type"),
      },
      {
        key: "principalId",
        label: this.i18n.t("admin.principalId", "Principal"),
      },
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
  openCreate(): void {
    if (this.canWrite)
      this.dialog
        .open(BoundaryEditDialogComponent, {
          width: "680px",
          data: {
            scopes: this.scopes,
            users: this.users,
            workloadRoles: this.workloadRoles,
            permissions: this.permissionsCatalog,
          },
        })
        .afterClosed()
        .subscribe((saved) => {
          if (saved) this.load();
        });
  }
  load(): void {
    this.state.load(
      forkJoin({
        boundaries: this.api.getIamBoundaries(),
        scopes: this.api.getIamScopes(),
        users: this.api.getUsers(),
        workloadRoles: this.api.getIamWorkloadRoles(),
        permissions: this.api.getPermissions(),
      }).pipe(
        catchError(() => {
          this.error = this.i18n.t(
            "admin.iamLoadFailed",
            "Unable to load boundaries.",
          );
          return of({
            boundaries: [],
            scopes: [],
            users: [],
            workloadRoles: [],
            permissions: [],
          });
        }),
      ),
    );
  }

  toggle(row: Record<string, unknown>): void {
    if (!this.canWrite) return;
    const id = String(row["id"] ?? "");
    const call = row["isActive"]
      ? this.api.deactivateIamBoundary(id)
      : this.api.activateIamBoundary(id);
    call.subscribe({
      next: () => this.load(),
      error: () =>
        (this.error = this.i18n.t(
          "admin.iamSaveFailed",
          "Unable to update boundary.",
        )),
    });
  }
}
