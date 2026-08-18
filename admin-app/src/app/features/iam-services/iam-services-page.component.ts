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
import { IamServiceDefinition } from "../../core/contracts/admin.contracts";
import { IamApiService } from "../../core/services/iam-api.service";
import { AdminResourceStateController } from "../../core/services/admin-resource-state.controller";

@Component({
  selector: "app-iam-services-page",
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
      [title]="'admin.services' | hhTranslate: 'Service catalog'"
      [subtitle]="
        'admin.servicesSubtitle'
          | hhTranslate
            : 'Register business services and their permission namespaces.'
      "
    /><hh-toolbar
      hhPageToolbar
      [label]="'admin.services' | hhTranslate: 'Service catalog'"
      ><span hhToolbarTitle
        >{{ services.length }}
        {{ "admin.services" | hhTranslate: "Services" }}</span
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
          >{{ "admin.permissionPrefix" | hhTranslate
          }}<input
            name="permissionPrefix"
            [(ngModel)]="draft.permissionPrefix"
            required /></label
        ><label
          >{{ "admin.owner" | hhTranslate
          }}<input name="owner" [(ngModel)]="draft.owner" required
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
      [label]="'admin.services' | hhTranslate: 'Service catalog'"
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
export class IamServicesPageComponent implements OnInit {
  private readonly api = inject(IamApiService);
  private readonly permissions = inject(HisHopePermissionService);
  private readonly i18n = inject(HisHopeI18nService);
  private readonly cdr = inject(ChangeDetectorRef);
  get canWrite(): boolean {
    return this.permissions.has("admin.roles.write");
  }
  private readonly destroyRef = inject(DestroyRef);
  readonly state = new AdminResourceStateController<IamServiceDefinition[]>({
    destroyRef: this.destroyRef,
    i18n: this.i18n,
    loadErrorMessageKey: "admin.iamLoadFailed",
    loadErrorFallback: "Unable to load services.",
  });
  services: IamServiceDefinition[] = [];
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
    permissionPrefix: "",
    owner: "identity-service",
  };
  get columns(): HisHopeDataTableColumn[] {
    this.i18n.locale();
    return [
      { key: "key", label: this.i18n.t("admin.key", "Key"), sortable: true },
      {
        key: "displayName",
        label: this.i18n.t("admin.displayName", "Display name"),
      },
      {
        key: "permissionPrefix",
        label: this.i18n.t("admin.permissionPrefix", "Permission prefix"),
      },
      { key: "owner", label: this.i18n.t("admin.owner", "Owner") },
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
      const services = this.state.resource.data();
      if (services) {
        this.services = services;
        this.rows = services.map((item) => ({ ...item }));
        this.cdr.markForCheck();
      }
    });
  }
  load(): void {
    this.state.load(this.api.getIamServices());
  }
  toggleForm(): void {
    if (!this.canWrite) return;
    this.formOpen = !this.formOpen;
    this.editingId = "";
    this.draft = {
      key: "",
      displayName: "",
      permissionPrefix: "",
      owner: "identity-service",
    };
  }
  edit(row: Record<string, unknown>): void {
    const item = this.services.find((x) => x.id === String(row["id"]));
    if (!item || !this.canWrite) return;
    this.editingId = item.id;
    this.formOpen = true;
    this.draft = {
      key: item.key,
      displayName: item.displayName,
      permissionPrefix: item.permissionPrefix,
      owner: item.owner,
    };
  }
  save(): void {
    if (
      !this.canWrite ||
      !this.draft.key.trim() ||
      !this.draft.displayName.trim() ||
      !this.draft.permissionPrefix.trim() ||
      !this.draft.owner.trim()
    )
      return;
    this.saving = true;
    const call = this.editingId
      ? this.api.updateIamService(this.editingId, this.draft)
      : this.api.createIamService(this.draft);
    call.subscribe({
      next: () => {
        this.formOpen = false;
        this.editingId = "";
        this.load();
      },
      error: () => {
        this.error = this.i18n.t(
          "admin.iamSaveFailed",
          "Unable to save service.",
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
      ? this.api.deactivateIamService(id)
      : this.api.activateIamService(id);
    call.subscribe({
      next: () => this.load(),
      error: () =>
        (this.error = this.i18n.t(
          "admin.iamSaveFailed",
          "Unable to update service.",
        )),
    });
  }
}
