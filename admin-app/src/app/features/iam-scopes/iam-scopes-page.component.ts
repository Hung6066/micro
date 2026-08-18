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
import { IamScope } from "../../core/contracts/admin.contracts";
import { IamApiService } from "../../core/services/iam-api.service";
import { AdminResourceStateController } from "../../core/services/admin-resource-state.controller";
import { iamScopeLabel } from "../../core/utils/iam-display.util";

@Component({
  selector: "app-iam-scopes-page",
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
      [title]="
        'admin.organizationsTenants' | hhTranslate: 'Organizations & tenants'
      "
      [subtitle]="
        'admin.scopesSubtitle'
          | hhTranslate
            : 'Manage organization, tenant, account and environment boundaries.'
      "
    /><hh-toolbar
      hhPageToolbar
      [label]="
        'admin.organizationsTenants' | hhTranslate: 'Organizations & tenants'
      "
      ><span hhToolbarTitle
        >{{ scopes.length }} {{ "admin.scopes" | hhTranslate: "Scopes" }}</span
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
          >{{ "admin.kind" | hhTranslate
          }}<select name="kind" [(ngModel)]="draft.kind" required>
            <option value="organization">
              {{ "admin.organization" | hhTranslate }}
            </option>
            <option value="tenant">{{ "admin.tenant" | hhTranslate }}</option>
            <option value="account">{{ "admin.account" | hhTranslate }}</option>
            <option value="environment">
              {{ "admin.environment" | hhTranslate }}
            </option>
          </select></label
        ><label *ngIf="draft.kind !== 'organization'"
          >{{ "admin.parentScope" | hhTranslate
          }}<select name="parentId" [(ngModel)]="draft.parentId">
            <option value="">{{ "admin.select" | hhTranslate }}</option>
            <option *ngFor="let parent of parentOptions" [value]="parent.id">
              {{ parent.displayName }} · {{ parent.key }} · {{ parent.kind }}
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
      [label]="'admin.scopes' | hhTranslate: 'Scopes'"
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
    ></hh-page-layout
  >`,
})
export class IamScopesPageComponent implements OnInit {
  private readonly api = inject(IamApiService);
  private readonly permissions = inject(HisHopePermissionService);
  private readonly i18n = inject(HisHopeI18nService);
  private readonly cdr = inject(ChangeDetectorRef);
  get canWrite(): boolean {
    return this.permissions.has("admin.roles.write");
  }
  private readonly destroyRef = inject(DestroyRef);
  readonly state = new AdminResourceStateController<IamScope[]>({
    destroyRef: this.destroyRef,
    i18n: this.i18n,
    loadErrorMessageKey: "admin.iamLoadFailed",
    loadErrorFallback: "Unable to load scopes.",
  });
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
  draft: { key: string; displayName: string; kind: string; parentId: string } =
    { key: "", displayName: "", kind: "organization", parentId: "" };
  get parentOptions(): IamScope[] {
    return this.scopes.filter((x) => x.isActive && x.id !== this.editingId);
  }
  get columns(): HisHopeDataTableColumn[] {
    this.i18n.locale();
    return [
      { key: "key", label: this.i18n.t("admin.key", "Key"), sortable: true },
      {
        key: "displayName",
        label: this.i18n.t("admin.displayName", "Display name"),
      },
      { key: "kind", label: this.i18n.t("admin.kind", "Kind") },
      {
        key: "parentId",
        label: this.i18n.t("admin.parentScope", "Parent scope"),
      },
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
      const scopes = this.state.resource.data();
      if (scopes) {
        this.scopes = scopes;
        this.rows = scopes.map((item) => ({
          ...item,
          parentId: iamScopeLabel(item.parentId, scopes, true),
        }));
        this.cdr.markForCheck();
      }
    });
  }
  load(): void {
    this.state.load(this.api.getIamScopes());
  }
  toggleForm(): void {
    if (!this.canWrite) return;
    this.formOpen = !this.formOpen;
    this.editingId = "";
    this.draft = {
      key: "",
      displayName: "",
      kind: "organization",
      parentId: "",
    };
  }
  edit(row: Record<string, unknown>): void {
    if (!this.canWrite) return;
    const item = this.scopes.find((x) => x.id === String(row["id"]));
    if (!item) return;
    this.editingId = item.id;
    this.formOpen = true;
    this.draft = {
      key: item.key,
      displayName: item.displayName,
      kind: item.kind,
      parentId: item.parentId ?? "",
    };
  }
  save(): void {
    if (
      !this.canWrite ||
      !this.draft.key.trim() ||
      !this.draft.displayName.trim()
    )
      return;
    this.saving = true;
    const request = {
      key: this.draft.key,
      displayName: this.draft.displayName,
      kind: this.draft.kind,
      parentId: this.draft.parentId || null,
    };
    const call = this.editingId
      ? this.api.updateIamScope(this.editingId, request)
      : this.api.createIamScope(request);
    call.subscribe({
      next: () => {
        this.formOpen = false;
        this.editingId = "";
        this.load();
      },
      error: () => {
        this.error = this.i18n.t(
          "admin.iamSaveFailed",
          "Unable to save scope.",
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
      ? this.api.deactivateIamScope(id)
      : this.api.activateIamScope(id);
    call.subscribe({
      next: () => this.load(),
      error: () =>
        (this.error = this.i18n.t(
          "admin.iamSaveFailed",
          "Unable to update scope.",
        )),
    });
  }
}
