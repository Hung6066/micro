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
  IamPermissionSet,
  IamScope,
  PermissionDefinition,
} from "../../core/contracts/admin.contracts";
import { IamApiService } from "../../core/services/iam-api.service";
import { AdminResourceStateController } from "../../core/services/admin-resource-state.controller";

@Component({
  selector: "app-permission-sets-page",
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
      [title]="'admin.permissionSets' | hhTranslate: 'Permission sets'"
      [subtitle]="
        'admin.permissionSetsSubtitle'
          | hhTranslate: 'Governed bundles of canonical permissions.'
      "
    /><hh-toolbar
      hhPageToolbar
      [label]="'admin.permissionSets' | hhTranslate: 'Permission sets'"
      ><span hhToolbarTitle
        >{{ sets.length }} {{ "admin.permissionSets" | hhTranslate }}</span
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
            <option value="">{{ "admin.select" | hhTranslate }}</option>
            <option *ngFor="let scope of scopes" [value]="scope.id">
              {{ scope.key }} · {{ scope.displayName }}
            </option>
          </select></label
        ><label
          >{{ "admin.permissions" | hhTranslate
          }}<select
            name="permissions"
            [(ngModel)]="draft.permissions"
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
        >
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
      [label]="'admin.permissionSets' | hhTranslate: 'Permission sets'"
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
          *ngIf="canWrite && row['lifecycleStatus'] !== 'published'"
          type="button"
          class="hh-icon-button hh-icon-button--small"
          (click)="publish(row)"
          [attr.aria-label]="'admin.publish' | hhTranslate"
          [attr.title]="'admin.publish' | hhTranslate"
        >
          <span class="material-icons" aria-hidden="true">publish</span>
        </button></ng-template
      ></hh-data-table
    ></hh-page-layout
  >`,
})
export class PermissionSetsPageComponent implements OnInit {
  private readonly api = inject(IamApiService);
  private readonly permissions = inject(HisHopePermissionService);
  private readonly i18n = inject(HisHopeI18nService);
  private readonly cdr = inject(ChangeDetectorRef);
  get canWrite(): boolean {
    return this.permissions.has("admin.roles.write");
  }
  private readonly destroyRef = inject(DestroyRef);
  readonly state = new AdminResourceStateController<{
    sets: IamPermissionSet[];
    scopes: IamScope[];
    permissions: PermissionDefinition[];
  }>({
    destroyRef: this.destroyRef,
    i18n: this.i18n,
    loadErrorMessageKey: "admin.iamLoadFailed",
    loadErrorFallback: "Unable to load permission sets.",
  });
  sets: IamPermissionSet[] = [];
  scopes: IamScope[] = [];
  permissionsCatalog: PermissionDefinition[] = [];
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
    permissions: [] as string[],
  };
  get columns(): HisHopeDataTableColumn[] {
    this.i18n.locale();
    return [
      { key: "key", label: this.i18n.t("admin.key", "Key"), sortable: true },
      {
        key: "displayName",
        label: this.i18n.t("admin.displayName", "Display name"),
      },
      { key: "scopeId", label: this.i18n.t("admin.scopeId", "Scope") },
      { key: "version", label: this.i18n.t("admin.version", "Version") },
      { key: "lifecycleStatus", label: this.i18n.t("admin.status", "Status") },
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
        this.sets = x.sets;
        this.scopes = x.scopes;
        this.permissionsCatalog = x.permissions.filter(
          (item) => !item.isDeprecated,
        );
        this.rows = x.sets.map((item) => ({ ...item }));
        this.cdr.markForCheck();
      }
    });
  }
  load(): void {
    this.state.load(
      forkJoin({
        sets: this.api.getIamPermissionSets(),
        scopes: this.api.getIamScopes(),
        permissions: this.api.getPermissions(),
      }),
    );
  }
  toggleForm(): void {
    if (!this.canWrite) return;
    this.formOpen = !this.formOpen;
    this.editingId = "";
    this.draft = {
      key: "",
      displayName: "",
      scopeId: this.scopes.find((x) => x.isActive)?.id ?? "",
      permissions: [],
    };
  }
  edit(row: Record<string, unknown>): void {
    if (!this.canWrite) return;
    const item = this.sets.find((x) => x.id === String(row["id"]));
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
      permissions,
    };
  }
  save(): void {
    if (
      !this.canWrite ||
      !this.draft.key.trim() ||
      !this.draft.displayName.trim() ||
      !this.draft.scopeId ||
      !this.draft.permissions.length
    )
      return;
    this.saving = true;
    const request = {
      key: this.draft.key,
      displayName: this.draft.displayName,
      scopeId: this.draft.scopeId,
      permissions: this.draft.permissions,
    };
    const call = this.editingId
      ? this.api.updateIamPermissionSet(this.editingId, request)
      : this.api.createIamPermissionSet(request);
    call.subscribe({
      next: () => {
        this.formOpen = false;
        this.editingId = "";
        this.load();
      },
      error: () => {
        this.error = this.i18n.t(
          "admin.iamSaveFailed",
          "Unable to save permission set.",
        );
        this.saving = false;
      },
      complete: () => (this.saving = false),
    });
  }
  publish(row: Record<string, unknown>): void {
    if (!this.canWrite) return;
    const id = String(row["id"] ?? "");
    if (!id) return;
    this.api.publishIamPermissionSet(id).subscribe({
      next: () => this.load(),
      error: () =>
        (this.error = this.i18n.t(
          "admin.iamSaveFailed",
          "Unable to publish permission set.",
        )),
    });
  }
}
