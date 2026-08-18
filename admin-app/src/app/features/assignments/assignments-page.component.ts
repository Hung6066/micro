import {
  ChangeDetectorRef,
  Component,
  DestroyRef,
  OnInit,
  effect,
  inject,
} from "@angular/core";
import { CommonModule } from "@angular/common";
import { FormsModule } from "@angular/forms";
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

@Component({
  selector: "app-assignments-page",
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
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
      [title]="'admin.assignments' | hhTranslate: 'Assignments'"
      [subtitle]="
        'admin.assignmentsSubtitle'
          | hhTranslate
            : 'Bind permission sets to human, group and workload principals.'
      "
    /><hh-toolbar
      hhPageToolbar
      [label]="'admin.assignments' | hhTranslate: 'Assignments'"
      ><span hhToolbarTitle
        >{{ assignments.length }} {{ "admin.assignments" | hhTranslate }}</span
      ><button
        *ngIf="canWrite"
        hhToolbarActions
        type="button"
        class="hh-button hh-button--primary"
        (click)="toggleForm()"
      >
        {{ (formOpen ? "admin.cancel" : "admin.create") | hhTranslate }}</button
      ><button
        hhToolbarActions
        type="button"
        class="hh-button hh-button--secondary"
        (click)="load()"
      >
        {{ "admin.refresh" | hhTranslate }}
      </button></hh-toolbar
    >
    <form *ngIf="canWrite && formOpen" class="hh-form-card" (ngSubmit)="save()">
      <div class="hh-form-grid">
        <label
          >{{ "admin.permissionSet" | hhTranslate
          }}<select
            name="permissionSetId"
            [(ngModel)]="draft.permissionSetId"
            required
          >
            <option value="">{{ "admin.select" | hhTranslate }}</option>
            <option *ngFor="let set of sets" [value]="set.id">
              {{ set.key }} · {{ set.displayName }}
            </option>
          </select></label
        ><label
          >{{ "admin.principalType" | hhTranslate
          }}<select name="principalType" [(ngModel)]="draft.principalType">
            <option value="human">
              {{ "admin.principalHuman" | hhTranslate }}
            </option>
            <option value="group">
              {{ "admin.principalGroup" | hhTranslate }}
            </option>
            <option value="workload">
              {{ "admin.principalWorkload" | hhTranslate }}
            </option>
          </select></label
        ><label
          >{{ "admin.principalId" | hhTranslate
          }}<select name="principalId" [(ngModel)]="draft.principalId" required>
            <option value="">{{ "admin.select" | hhTranslate }}</option>
            <ng-container [ngSwitch]="draft.principalType"
              ><ng-container *ngSwitchCase="'human'"
                ><option *ngFor="let user of users" [value]="user.id">
                  {{ user.email || user.userName }}
                </option></ng-container
              ><ng-container *ngSwitchCase="'group'"
                ><option *ngFor="let group of groups" [value]="group.id">
                  {{ group.key }}
                </option></ng-container
              ><ng-container *ngSwitchCase="'workload'"
                ><option *ngFor="let role of workloadRoles" [value]="role.id">
                  {{ role.key }}
                </option></ng-container
              ></ng-container
            >
          </select></label
        ><label
          >{{ "admin.scopeId" | hhTranslate
          }}<select name="scopeId" [(ngModel)]="draft.scopeId" required>
            <option value="">{{ "admin.select" | hhTranslate }}</option>
            <option *ngFor="let scope of scopes" [value]="scope.id">
              {{ scope.key }}
            </option>
          </select></label
        >
      </div>
      <button
        class="hh-button hh-button--primary"
        type="submit"
        [disabled]="saving"
      >
        {{ "admin.save" | hhTranslate }}
      </button>
    </form>
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
        ><button
          *ngIf="canWrite && row['status'] === 'active'"
          type="button"
          class="hh-icon-button hh-icon-button--small hh-icon-button--danger"
          (click)="revoke(row)"
          [attr.aria-label]="'admin.revoke' | hhTranslate"
          [attr.title]="'admin.revoke' | hhTranslate"
        >
          <span class="material-icons" aria-hidden="true">link_off</span>
        </button></ng-template
      ></hh-data-table
    ></hh-page-layout
  >`,
})
export class AssignmentsPageComponent implements OnInit {
  private readonly api = inject(IamApiService);
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
  saving = false;
  get error(): string {
    return this.state.error;
  }
  set error(value: string) {
    this.state.setActionError(value);
  }
  formOpen = false;
  draft = {
    permissionSetId: "",
    principalType: "human",
    principalId: "",
    scopeId: "",
  };
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
  toggleForm(): void {
    if (!this.canWrite) return;
    this.formOpen = !this.formOpen;
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

  save(): void {
    if (
      !this.canWrite ||
      !this.draft.permissionSetId ||
      !this.draft.principalId ||
      !this.draft.scopeId
    )
      return;
    this.saving = true;
    this.api
      .createIamAssignment(this.draft.permissionSetId, {
        principalType: this.draft.principalType,
        principalId: this.draft.principalId,
        scopeId: this.draft.scopeId,
      })
      .subscribe({
        next: () => {
          this.formOpen = false;
          this.load();
        },
        error: () => {
          this.error = this.i18n.t(
            "admin.iamSaveFailed",
            "Unable to create assignment.",
          );
          this.saving = false;
        },
        complete: () => (this.saving = false),
      });
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
