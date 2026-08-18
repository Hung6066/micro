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
  IamPermissionBoundary,
  IamScope,
  IamWorkloadRole,
  PermissionDefinition,
  User,
} from "../../core/contracts/admin.contracts";
import { IamApiService } from "../../core/services/iam-api.service";
import { AdminResourceStateController } from "../../core/services/admin-resource-state.controller";

@Component({
  selector: "app-boundaries-page",
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
      [title]="'admin.boundaries' | hhTranslate: 'Permission boundaries'"
      [subtitle]="
        'admin.boundariesSubtitle'
          | hhTranslate
            : 'Limit the maximum permissions a principal can receive.'
      "
    /><hh-toolbar
      hhPageToolbar
      [label]="'admin.boundaries' | hhTranslate: 'Permission boundaries'"
      ><span hhToolbarTitle
        >{{ boundaries.length }} {{ "admin.boundaries" | hhTranslate }}</span
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
          >{{ "admin.principalType" | hhTranslate
          }}<select name="principalType" [(ngModel)]="draft.principalType">
            <option value="human">
              {{ "admin.principalHuman" | hhTranslate }}
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
        ><label
          >{{ "admin.permissions" | hhTranslate
          }}<select
            name="allowedPermissions"
            [(ngModel)]="draft.allowedPermissions"
            multiple
            size="6"
            required
          >
            <option
              *ngFor="let permission of permissionsCatalog"
              [value]="permission.code"
            >
              {{ permission.code }} · {{ permission.name }}
            </option>
          </select></label
        ><label
          >{{ "admin.resourceConstraints" | hhTranslate
          }}<textarea
            name="resourceConstraintsJson"
            rows="3"
            [(ngModel)]="draft.resourceConstraintsJson"
            required
          ></textarea>
        </label>
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
      [label]="'admin.boundaries' | hhTranslate: 'Permission boundaries'"
      [columns]="columns"
      [rows]="rows"
      [loading]="loading"
      [empty]="!loading && !error && !rows.length"
      ><ng-template hhDataTableCell="actions" let-row
        ><button
          *ngIf="canWrite && row['isActive']"
          type="button"
          class="hh-icon-button hh-icon-button--small hh-icon-button--danger"
          (click)="toggle(row)"
          [attr.aria-label]="'admin.deactivate' | hhTranslate"
          [attr.title]="'admin.deactivate' | hhTranslate"
        >
          <span class="material-icons" aria-hidden="true"
            >toggle_off</span
          ></button
        ><button
          *ngIf="canWrite && !row['isActive']"
          type="button"
          class="hh-icon-button hh-icon-button--small"
          (click)="toggle(row)"
          [attr.aria-label]="'admin.activate' | hhTranslate"
          [attr.title]="'admin.activate' | hhTranslate"
        >
          <span class="material-icons" aria-hidden="true">toggle_on</span>
        </button></ng-template
      ></hh-data-table
    ></hh-page-layout
  >`,
})
export class BoundariesPageComponent implements OnInit {
  private readonly api = inject(IamApiService);
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
  saving = false;
  get error(): string {
    return this.state.error;
  }
  set error(value: string) {
    this.state.setActionError(value);
  }
  formOpen = false;
  draft = {
    principalType: "human",
    principalId: "",
    scopeId: "",
    allowedPermissions: [] as string[],
    resourceConstraintsJson: "{}",
  };
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
  toggleForm(): void {
    if (!this.canWrite) return;
    this.formOpen = !this.formOpen;
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

  save(): void {
    if (!this.canWrite || !this.draft.principalId || !this.draft.scopeId)
      return;
    this.saving = true;
    const call = this.api.createIamBoundary(this.draft);
    call.subscribe({
      next: () => {
        this.formOpen = false;
        this.load();
      },
      error: () => {
        this.error = this.i18n.t(
          "admin.iamSaveFailed",
          "Unable to create boundary.",
        );
        this.saving = false;
      },
      complete: () => (this.saving = false),
    });
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
