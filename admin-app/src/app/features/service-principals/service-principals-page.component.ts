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

@Component({
  selector: "app-service-principals-page",
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
        <button
          *ngIf="canWrite"
          hh-toolbar-actions
          type="button"
          class="hh-button hh-button--primary"
          (click)="toggleForm()"
        >
          {{ (formOpen ? "admin.cancel" : "admin.create") | hhTranslate }}
        </button>
        <button
          hhToolbarActions
          type="button"
          class="hh-button hh-button--secondary"
          (click)="load()"
        >
          {{ "admin.refresh" | hhTranslate }}
        </button>
      </hh-toolbar>
      <form
        *ngIf="canWrite && formOpen"
        class="hh-form-card"
        (ngSubmit)="save()"
      >
        <div class="hh-form-grid">
          <label
            >{{ "admin.key" | hhTranslate
            }}<input name="key" [(ngModel)]="draft.key" required
          /></label>
          <label
            >{{ "admin.displayName" | hhTranslate: "Display name"
            }}<input
              name="displayName"
              [(ngModel)]="draft.displayName"
              required
          /></label>
          <label
            >{{ "admin.scopeId" | hhTranslate
            }}<select name="scopeId" [(ngModel)]="draft.scopeId" required>
              <option value="">
                {{ "admin.select" | hhTranslate: "Select" }}
              </option>
              <option *ngFor="let scope of scopes" [value]="scope.id">
                {{ scope.key }} · {{ scope.displayName }}
              </option>
            </select></label
          >
          <label
            >{{ "admin.audience" | hhTranslate
            }}<input name="audience" [(ngModel)]="draft.audience" required
          /></label>
          <label
            >{{ "admin.maxSessionSeconds" | hhTranslate: "Max session seconds"
            }}<input
              type="number"
              min="300"
              max="86400"
              name="maxSessionSeconds"
              [(ngModel)]="draft.maxSessionSeconds"
              required
          /></label>
          <label
            >{{ "admin.permissionsCsv" | hhTranslate
            }}<input
              name="permissions"
              [(ngModel)]="draft.permissions"
              placeholder="service.read, service.write"
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
        [label]="'admin.servicePrincipals' | hhTranslate: 'Service principals'"
        [columns]="columns"
        [rows]="rows"
        [loading]="loading"
        [empty]="!loading && !error && !rows.length"
      >
        <ng-template hhDataTableCell="actions" let-row>
          <button
            *ngIf="canWrite"
            type="button"
            class="hh-icon-button hh-icon-button--small"
            (click)="edit(row)"
            [attr.aria-label]="'admin.edit' | hhTranslate"
            [attr.title]="'admin.edit' | hhTranslate"
          >
            <span class="material-icons" aria-hidden="true">edit</span>
          </button>
          <button
            *ngIf="canWrite && row['isActive']"
            type="button"
            class="hh-icon-button hh-icon-button--small hh-icon-button--danger"
            (click)="toggle(row)"
            [attr.aria-label]="'admin.deactivate' | hhTranslate"
            [attr.title]="'admin.deactivate' | hhTranslate"
          >
            <span class="material-icons" aria-hidden="true">toggle_off</span>
          </button>
          <button
            *ngIf="canWrite && !row['isActive']"
            type="button"
            class="hh-icon-button hh-icon-button--small"
            (click)="toggle(row)"
            [attr.aria-label]="'admin.activate' | hhTranslate"
            [attr.title]="'admin.activate' | hhTranslate"
          >
            <span class="material-icons" aria-hidden="true">toggle_on</span>
          </button>
        </ng-template>
      </hh-data-table>
    </hh-page-layout>
  `,
})
export class ServicePrincipalsPageComponent implements OnInit {
  private readonly api = inject(IamApiService);
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
    if (!this.canWrite) return;
    const item = this.roles.find((x) => x.id === String(row["id"]));
    if (!item) return;
    let permissions: string[] = [];
    try {
      permissions = JSON.parse(item.permissionsJson);
    } catch {
      /* malformed legacy value is handled as empty */
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
      key: this.draft.key,
      displayName: this.draft.displayName,
      scopeId: this.draft.scopeId,
      audience: this.draft.audience,
      trustPolicyJson: this.draft.trustPolicyJson || "{}",
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
      error: () => {
        this.error = this.i18n.t(
          "admin.iamSaveFailed",
          "Unable to save service principal.",
        );
        this.saving = false;
      },
      complete: () => (this.saving = false),
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
