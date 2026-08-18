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
  IamAssignment,
  IamGroup,
  IamPermissionSet,
  IamScope,
  IamWorkloadRole,
  User,
} from "../../core/contracts/admin.contracts";
import { IamApiService } from "../../core/services/iam-api.service";
import { AdminResourceStateController } from "../../core/services/admin-resource-state.controller";
import { AssignmentEditDialogComponent } from "./assignment-edit-dialog.component";

import { HisHopeActionButtonComponent } from "@his-hope/frontend-foundation/ui";
@Component({
  selector: "app-assignments-page",
  standalone: true,
  imports: [
    CommonModule,
    MatDialogModule,
    HisHopeDataTableCellDirective,
    HisHopeDataTableComponent,
    HisHopeActionButtonComponent,
    HisHopePageHeaderComponent,
    HisHopePageLayoutComponent,
    HisHopeToolbarComponent,
    HisHopeTranslatePipe,
  ],
  template: ` <hh-page-layout
    ><hh-page-header
      hhPageHeader
      [title]="'admin.assignments' | hhTranslate: 'Assignments'"
      [subtitle]="
        'admin.assignmentsSubtitle'
          | hhTranslate
            : 'Bind permission sets to human, group and workload principals.'
      " /><hh-toolbar
      hhPageToolbar
      [label]="'admin.assignments' | hhTranslate: 'Assignments'"
      ><span hhToolbarTitle
        >{{ assignments.length }} {{ "admin.assignments" | hhTranslate }}</span
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
      [label]="'admin.assignments' | hhTranslate: 'Assignments'"
      [columns]="columns"
      [rows]="rows"
      [loading]="loading"
      [empty]="!loading && !error && !rows.length"
      ><ng-template hhDataTableCell="actions" let-row
        ><hh-action-button
          *ngIf="canWrite && row['status'] === 'active'"
          kind="danger"
          mode="icon-only"
          icon="link_off"
          [label]="'admin.revoke' | hhTranslate"
          (pressed)="revoke(row)" /></ng-template></hh-data-table
  ></hh-page-layout>`,
})
export class AssignmentsPageComponent implements OnInit {
  private readonly api = inject(IamApiService);
  private readonly dialog = inject(MatDialog);
  private readonly permissions = inject(HisHopePermissionService);
  private readonly i18n = inject(HisHopeI18nService);
  private readonly cdr = inject(ChangeDetectorRef);
  get canWrite(): boolean {
    return this.permissions.has("admin.roles.write");
  }
  assignments: IamAssignment[] = [];
  sets: IamPermissionSet[] = [];
  scopes: IamScope[] = [];
  users: User[] = [];
  groups: IamGroup[] = [];
  workloadRoles: IamWorkloadRole[] = [];
  rows: Record<string, unknown>[] = [];
  private readonly destroyRef = inject(DestroyRef);
  readonly state = new AdminResourceStateController<{
    assignments: IamAssignment[];
    sets: IamPermissionSet[];
    scopes: IamScope[];
    users: User[];
    groups: IamGroup[];
    workloadRoles: IamWorkloadRole[];
  }>({
    destroyRef: this.destroyRef,
    i18n: this.i18n,
    loadErrorMessageKey: "admin.iamLoadFailed",
    loadErrorFallback: "Unable to load assignments.",
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
        this.assignments = data.assignments;
        this.sets = data.sets;
        this.scopes = data.scopes;
        this.users = data.users;
        this.groups = data.groups;
        this.workloadRoles = data.workloadRoles;
        this.rows = data.assignments.map((item) => ({ ...item }));
        this.cdr.markForCheck();
      }
    });
  }
  get columns(): HisHopeDataTableColumn[] {
    this.i18n.locale();
    return [
      {
        key: "permissionSetId",
        label: this.i18n.t("admin.permissionSet", "Permission set"),
      },
      {
        key: "principalType",
        label: this.i18n.t("admin.principalType", "Principal type"),
      },
      {
        key: "principalId",
        label: this.i18n.t("admin.principalId", "Principal"),
      },
      { key: "scopeId", label: this.i18n.t("admin.scopeId", "Scope") },
      { key: "status", label: this.i18n.t("admin.status", "Status") },
      { key: "expiresAt", label: this.i18n.t("admin.expiresAt", "Expires") },
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
    if (!this.canWrite) return;
    this.dialog
      .open(AssignmentEditDialogComponent, {
        width: "640px",
        data: {
          sets: this.sets,
          scopes: this.scopes,
          users: this.users,
          groups: this.groups,
          workloadRoles: this.workloadRoles,
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
        assignments: this.api.getIamAssignments(),
        sets: this.api.getIamPermissionSets(),
        scopes: this.api.getIamScopes(),
        users: this.api.getUsers(),
        groups: this.api.getIamGroups(),
        workloadRoles: this.api.getIamWorkloadRoles(),
      }).pipe(
        catchError(() => {
          this.error = this.i18n.t(
            "admin.iamLoadFailed",
            "Unable to load assignments.",
          );
          return of({
            assignments: [],
            sets: [],
            scopes: [],
            users: [],
            groups: [],
            workloadRoles: [],
          });
        }),
      ),
    );
  }

  revoke(row: Record<string, unknown>): void {
    if (!this.canWrite) return;
    const id = String(row["id"] ?? "");
    if (!id) return;
    this.api.revokeIamAssignment(id).subscribe({
      next: () => this.load(),
      error: () =>
        (this.error = this.i18n.t(
          "admin.iamSaveFailed",
          "Unable to revoke assignment.",
        )),
    });
  }
}
