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
import { catchError, forkJoin, map, of } from "rxjs";
import { takeUntilDestroyed } from "@angular/core/rxjs-interop";
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
  IamAssignment,
  IamGroup,
  IamPermissionSet,
  IamScope,
  IamWorkloadRole,
  User,
} from "../../core/contracts/admin.contracts";
import { IamApiService } from "../../core/services/iam-api.service";
import { TenantContextService } from "../../core/services/tenant-context.service";
import { AdminResourceStateController } from "../../core/services/admin-resource-state.controller";
import { iamPrincipalLabel } from "../../core/utils/iam-display.util";
import { AssignmentEditDialogComponent } from "./assignment-edit-dialog.component";

import { HisHopeActionButtonComponent } from "@his-hope/frontend-foundation/ui";
@Component({
  selector: "app-assignments-page",
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    CommonModule,
    HisHopeActionButtonComponent,
    HisHopeResourceListPageComponent,
    HisHopeResourceRowActionsDirective,
    HisHopeTranslatePipe,
  ],
  template: `
    <hh-resource-list-page
      title="admin.assignments"
      titleFallback="Assignments"
      subtitle="admin.assignmentsSubtitle"
      subtitleFallback="Bind permission sets to human, group and workload principals."
      [count]="assignments.length"
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
          *ngIf="canWrite && row['status'] === 'active'"
          kind="danger"
          mode="icon-only"
          icon="link_off"
          [label]="'admin.revoke' | hhTranslate"
          (pressed)="revoke(row)"
        />
      </ng-template>
    </hh-resource-list-page>
  `,
})
export class AssignmentsPageComponent implements OnInit {
  private readonly api = inject(IamApiService);
  private readonly tenantContext = inject(TenantContextService);
  private readonly dialog = inject(HisHopeDialogService);
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
        format: { type: "friendlyReference", references: this.sets },
      },
      {
        key: "principalType",
        label: this.i18n.t("admin.principalType", "Principal type"),
      },
      {
        key: "principalId",
        label: this.i18n.t("admin.principalId", "Principal"),
        computed: (row) =>
          iamPrincipalLabel(
            String(row["principalId"] ?? ""),
            String(row["principalType"] ?? ""),
            this.users,
            this.groups,
            this.workloadRoles,
          ),
      },
      {
        key: "scopeId",
        label: this.i18n.t("admin.scopeId", "Scope"),
        format: { type: "friendlyReference", references: this.scopes },
      },
      { key: "status", label: this.i18n.t("admin.status", "Status") },
      {
        key: "expiresAt",
        label: this.i18n.t("admin.expiresAt", "Expires"),
        format: "dateTime",
      },
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
    const environmentScopeId = this.tenantContext.getActiveEnvironmentScopeId();
    this.state.load(
      forkJoin({
        assignments: this.api.getIamAssignments(
          environmentScopeId ? { scopeId: environmentScopeId } : {},
        ),
        sets: this.api.getIamPermissionSets(environmentScopeId),
        scopes: this.api.getIamScopes(),
        users: this.api.getUsers(),
        groups: this.api.getIamGroups(),
        workloadRoles: this.api.getIamWorkloadRoles(),
      }).pipe(
        map((result) => ({
          ...result,
          scopes: this.tenantContext.filterScopes(result.scopes),
        })),
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
