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
import { forkJoin } from "rxjs";
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
  IamScope,
  IamWorkloadRole,
} from "../../core/contracts/admin.contracts";
import { IamApiService } from "../../core/services/iam-api.service";
import { AdminResourceStateController } from "../../core/services/admin-resource-state.controller";

@Component({
  selector: "app-workload-roles-page",
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
        ><button
          *ngIf="canWrite"
          hh-toolbar-actions
          type="button"
          class="hh-button hh-button--primary"
          (click)="toggleForm()"
        >
          {{
            (formOpen ? "admin.cancel" : "admin.create") | hhTranslate
          }}</button
        ><button
          hhToolbarActions
          type="button"
          class="hh-button hh-button--secondary"
          (click)="load()"
        >
          {{ "admin.refresh" | hhTranslate: "Refresh" }}
        </button></hh-toolbar
      >
      <form
        *ngIf="canWrite && formOpen"
        class="hh-form-card"
        (ngSubmit)="save()"
      >
        <div class="hh-form-grid">
          <label
            >{{ "admin.key" | hhTranslate
            }}<input name="key" [(ngModel)]="draft.key" required /></label
          ><label
            >{{ "admin.displayName" | hhTranslate: "Display name"
            }}<input
              name="displayName"
              [(ngModel)]="draft.displayName"
              required /></label
          ><label
            >{{ "admin.scopeId" | hhTranslate
            }}<select name="scopeId" [(ngModel)]="draft.scopeId" required>
              <option value="">
                {{ "admin.select" | hhTranslate: "Select" }}
              </option>
              <option *ngFor="let scope of scopes" [value]="scope.id">
                {{ scope.key }} · {{ scope.displayName }}
              </option>
            </select></label
          ><label
            >{{ "admin.audience" | hhTranslate
            }}<input
              name="audience"
              [(ngModel)]="draft.audience"
              required /></label
          ><label
            >{{ "admin.maxSessionSeconds" | hhTranslate: "Max session seconds"
            }}<input
              type="number"
              min="300"
              max="86400"
              name="maxSessionSeconds"
              [(ngModel)]="draft.maxSessionSeconds"
              required /></label
          ><label
            >{{ "admin.permissionsCsv" | hhTranslate
            }}<input name="permissions" [(ngModel)]="draft.permissions"
          /></label>
        </div>
        <button
          class="hh-button hh-button--primary"
          type="submit"
          [disabled]="saving"
        >
          {{ (editingId ? "admin.update" : "admin.save") | hhTranslate }}
        </button>
      </form>
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
          ><button
            *ngIf="canWrite"
            type="button"
            class="hh-icon-button hh-icon-button--small"
            (click)="edit(row)"
            [attr.aria-label]="'admin.edit' | hhTranslate"
            [attr.title]="'admin.edit' | hhTranslate"
          >
            <span class="material-icons" aria-hidden="true">edit</span></button
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
      >
    </hh-page-layout>
  `,
})
export class WorkloadRolesPageComponent implements OnInit {
  private readonly api = inject(IamApiService);
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
  formOpen = false;
  editingId = "";
  get loading(): boolean {
    return this.state.loading;
  }
  get error(): string {
    return this.state.error;
  }
  set error(value: string) {
    this.state.setActionError(value);
  }
  draft = {
    key: "",
    displayName: "",
    scopeId: "",
    audience: "",
    maxSessionSeconds: 3600,
    permissions: "",
    trustPolicyJson: "{}",
  };
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
  toggleForm(): void {
    if (!this.canWrite) return;
    this.formOpen = !this.formOpen;
    this.editingId = "";
    this.draft = {
      ...this.draft,
      key: "",
      displayName: "",
      scopeId: this.scopes.find((x) => x.isActive)?.id ?? "",
      audience: "",
      permissions: "",
    };
  }
  edit(row: Record<string, unknown>): void {
    const item = this.roles.find((x) => x.id === String(row["id"]));
    if (!item || !this.canWrite) return;
    let permissions: string[] = [];
    try {
      permissions = JSON.parse(item.permissionsJson);
    } catch {
      permissions = [];
    }
    this.editingId = item.id;
    this.formOpen = true;
    this.draft = {
      key: item.key,
      displayName: item.displayName,
      scopeId: item.scopeId,
      audience: item.audience,
      maxSessionSeconds: item.maxSessionSeconds,
      permissions: permissions.join(", "),
      trustPolicyJson: item.trustPolicyJson,
    };
  }
  save(): void {
    if (
      !this.canWrite ||
      !this.draft.key.trim() ||
      !this.draft.displayName.trim() ||
      !this.draft.scopeId ||
      !this.draft.audience.trim()
    )
      return;
    this.saving = true;
    const request = {
      ...this.draft,
      maxSessionSeconds: Number(this.draft.maxSessionSeconds),
      permissions: this.draft.permissions
        .split(",")
        .map((x) => x.trim())
        .filter(Boolean),
    };
    const call = this.editingId
      ? this.api.updateIamWorkloadRole(this.editingId, request)
      : this.api.createIamWorkloadRole(request);
    call.subscribe({
      next: () => {
        this.formOpen = false;
        this.editingId = "";
        this.load();
      },
      error: () => this.fail(),
      complete: () => (this.saving = false),
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
