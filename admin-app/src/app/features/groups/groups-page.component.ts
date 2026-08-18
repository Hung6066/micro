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
import { IamGroup, IamScope } from "../../core/contracts/admin.contracts";
import { IamApiService } from "../../core/services/iam-api.service";
import { AdminResourceStateController } from "../../core/services/admin-resource-state.controller";
import { finalize, forkJoin, take } from "rxjs";

@Component({
  selector: "app-groups-page",
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
        [title]="'admin.groups' | hhTranslate"
        [subtitle]="
          'admin.groupsSubtitle'
            | hhTranslate: 'Manage identity groups and memberships.'
        "
      />
      <hh-toolbar hhPageToolbar [label]="'admin.groups' | hhTranslate">
        <span hhToolbarTitle
          >{{ groups.length }} {{ "admin.groups" | hhTranslate }}</span
        >
        <button
          *ngIf="canWrite"
          hh-toolbar-actions
          type="button"
          class="hh-icon-button"
          (click)="startCreate()"
          [attr.aria-label]="
            (editingId ? 'common.cancel' : 'admin.create') | hhTranslate
          "
          [attr.title]="
            (editingId ? 'common.cancel' : 'admin.create') | hhTranslate
          "
        >
          <span class="material-icons" aria-hidden="true">{{
            editingId ? "close" : "add"
          }}</span>
        </button>
        <button
          hh-toolbar-actions
          type="button"
          class="hh-icon-button"
          (click)="load()"
          [attr.aria-label]="'common.refresh' | hhTranslate: 'Refresh'"
          [attr.title]="'common.refresh' | hhTranslate: 'Refresh'"
        >
          <span class="material-icons" aria-hidden="true">refresh</span>
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
        [label]="'admin.groups' | hhTranslate"
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
export class GroupsPageComponent implements OnInit {
  private readonly api = inject(IamApiService);
  private readonly permissions = inject(HisHopePermissionService);
  private readonly i18n = inject(HisHopeI18nService);
  private readonly cdr = inject(ChangeDetectorRef);
  get canWrite(): boolean {
    return this.permissions.has("admin.roles.write");
  }
  groups: IamGroup[] = [];
  scopes: IamScope[] = [];
  rows: Record<string, unknown>[] = [];
  private readonly destroyRef = inject(DestroyRef);
  readonly state = new AdminResourceStateController<{
    groups: IamGroup[];
    scopes: IamScope[];
  }>({
    destroyRef: this.destroyRef,
    i18n: this.i18n,
    loadErrorMessageKey: "admin.iamLoadFailed",
    loadErrorFallback: "Unable to load groups.",
  });
  saving = false;
  private actionError = "";
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
  draft: { key: string; displayName: string; scopeId: string } = {
    key: "",
    displayName: "",
    scopeId: "",
  };
  get columns(): HisHopeDataTableColumn[] {
    this.i18n.locale();
    return [
      { key: "key", label: this.i18n.t("admin.key", "Key"), sortable: true },
      {
        key: "displayName",
        label: this.i18n.t("admin.displayName", "Display name"),
        sortable: true,
      },
      { key: "scopeId", label: this.i18n.t("admin.scopeId", "Scope") },
      { key: "isActive", label: this.i18n.t("admin.active", "Active") },
      {
        key: "actions",
        label: this.i18n.t("admin.actions", "Actions"),
        sortable: false,
        hideable: false,
        width: "112px",
        minWidth: 112,
        align: "center" as const,
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
        this.groups = data.groups;
        this.scopes = data.scopes;
        this.rows = data.groups.map((item) => ({ ...item }));
        this.cdr.markForCheck();
      }
    });
  }
  load(): void {
    this.state.load(
      forkJoin({
        groups: this.api.getIamGroups().pipe(take(1)),
        scopes: this.api.getIamScopes().pipe(take(1)),
      }),
    );
  }
  startCreate(): void {
    if (!this.canWrite) return;
    this.formOpen = !this.formOpen;
    this.editingId = "";
    this.draft = {
      key: "",
      displayName: "",
      scopeId: this.scopes.find((x) => x.isActive)?.id ?? "",
    };
  }
  edit(row: Record<string, unknown>): void {
    if (!this.canWrite) return;
    const item = this.groups.find((x) => x.id === String(row["id"]));
    if (!item) return;
    this.editingId = item.id;
    this.formOpen = true;
    this.draft = {
      key: item.key,
      displayName: item.displayName,
      scopeId: item.scopeId,
    };
  }
  save(): void {
    if (
      !this.canWrite ||
      !this.draft.key.trim() ||
      !this.draft.displayName.trim() ||
      !this.draft.scopeId
    )
      return;
    this.saving = true;
    const request = this.editingId
      ? this.api.updateIamGroup(this.editingId, this.draft)
      : this.api.createIamGroup(this.draft);
    request.subscribe({
      next: () => {
        this.formOpen = false;
        this.editingId = "";
        this.load();
      },
      error: () => {
        this.error = this.i18n.t(
          "admin.iamSaveFailed",
          "Unable to save group.",
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
      ? this.api.deactivateIamGroup(id)
      : this.api.activateIamGroup(id);
    request.subscribe({
      next: () => this.load(),
      error: () =>
        (this.error = this.i18n.t(
          "admin.iamSaveFailed",
          "Unable to update group.",
        )),
    });
  }
}
