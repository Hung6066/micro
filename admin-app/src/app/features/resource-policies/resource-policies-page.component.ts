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
  IamResourcePolicy,
  IamScope,
  IamServiceDefinition,
} from "../../core/contracts/admin.contracts";
import { IamApiService } from "../../core/services/iam-api.service";
import { AdminResourceStateController } from "../../core/services/admin-resource-state.controller";

@Component({
  selector: "app-resource-policies-page",
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
      [title]="'admin.resourcePolicies' | hhTranslate: 'Resource policies'"
      [subtitle]="
        'admin.resourcePoliciesSubtitle'
          | hhTranslate
            : 'Resource-owned authorization constraints for business services.'
      "
    /><hh-toolbar
      hhPageToolbar
      [label]="'admin.resourcePolicies' | hhTranslate: 'Resource policies'"
      ><span hhToolbarTitle
        >{{ policies.length }}
        {{ "admin.resourcePolicies" | hhTranslate }}</span
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
          >{{ "admin.scopeId" | hhTranslate
          }}<select name="scopeId" [(ngModel)]="draft.scopeId" required>
            <option value="">{{ "admin.select" | hhTranslate }}</option>
            <option *ngFor="let scope of scopes" [value]="scope.id">
              {{ scope.key }}
            </option>
          </select></label
        ><label
          >{{ "admin.serviceKey" | hhTranslate
          }}<select name="serviceKey" [(ngModel)]="draft.serviceKey" required>
            <option value="">{{ "admin.select" | hhTranslate }}</option>
            <option *ngFor="let service of services" [value]="service.key">
              {{ service.key }}
            </option>
          </select></label
        ><label
          >{{ "admin.resourcePattern" | hhTranslate
          }}<input
            name="resourcePattern"
            [(ngModel)]="draft.resourcePattern"
            required /></label
        ><label
          >{{ "admin.statementsJson" | hhTranslate
          }}<textarea
            name="statementsJson"
            rows="4"
            [(ngModel)]="draft.statementsJson"
            required
          ></textarea>
        </label>
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
      [label]="'admin.resourcePolicies' | hhTranslate: 'Resource policies'"
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
export class ResourcePoliciesPageComponent implements OnInit {
  private readonly api = inject(IamApiService);
  private readonly permissions = inject(HisHopePermissionService);
  private readonly i18n = inject(HisHopeI18nService);
  private readonly cdr = inject(ChangeDetectorRef);
  get canWrite(): boolean {
    return this.permissions.has("admin.roles.write");
  }
  private readonly destroyRef = inject(DestroyRef);
  readonly state = new AdminResourceStateController<{
    policies: IamResourcePolicy[];
    scopes: IamScope[];
    services: IamServiceDefinition[];
  }>({
    destroyRef: this.destroyRef,
    i18n: this.i18n,
    loadErrorMessageKey: "admin.iamLoadFailed",
    loadErrorFallback: "Unable to load resource policies.",
  });
  policies: IamResourcePolicy[] = [];
  scopes: IamScope[] = [];
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
    scopeId: "",
    serviceKey: "",
    resourcePattern: "",
    statementsJson: '{\n  "statements": []\n}',
  };
  get columns(): HisHopeDataTableColumn[] {
    this.i18n.locale();
    return [
      { key: "serviceKey", label: this.i18n.t("admin.serviceKey", "Service") },
      {
        key: "resourcePattern",
        label: this.i18n.t("admin.resourcePattern", "Resource pattern"),
      },
      { key: "scopeId", label: this.i18n.t("admin.scopeId", "Scope") },
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
        this.policies = x.policies;
        this.scopes = x.scopes;
        this.services = x.services;
        this.rows = x.policies.map((item) => ({ ...item }));
        this.cdr.markForCheck();
      }
    });
  }
  load(): void {
    this.state.load(
      forkJoin({
        policies: this.api.getIamResourcePolicies(),
        scopes: this.api.getIamScopes(),
        services: this.api.getIamServices(),
      }),
    );
  }
  toggleForm(): void {
    if (!this.canWrite) return;
    this.formOpen = !this.formOpen;
    this.editingId = "";
    this.draft = {
      scopeId: this.scopes.find((x) => x.isActive)?.id ?? "",
      serviceKey: this.services.find((x) => x.isActive)?.key ?? "",
      resourcePattern: "",
      statementsJson: '{\n  "statements": []\n}',
    };
  }
  edit(row: Record<string, unknown>): void {
    if (!this.canWrite) return;
    const item = this.policies.find((x) => x.id === String(row["id"]));
    if (!item) return;
    this.editingId = item.id;
    this.formOpen = true;
    this.draft = {
      scopeId: item.scopeId,
      serviceKey: item.serviceKey,
      resourcePattern: item.resourcePattern,
      statementsJson: item.statementsJson,
    };
  }
  save(): void {
    if (
      !this.canWrite ||
      !this.draft.scopeId ||
      !this.draft.serviceKey ||
      !this.draft.resourcePattern.trim() ||
      !this.draft.statementsJson.trim()
    )
      return;
    this.saving = true;
    const call = this.editingId
      ? this.api.updateIamResourcePolicy(this.editingId, this.draft)
      : this.api.createIamResourcePolicy(this.draft);
    call.subscribe({
      next: () => {
        this.formOpen = false;
        this.editingId = "";
        this.load();
      },
      error: () => {
        this.error = this.i18n.t(
          "admin.iamSaveFailed",
          "Unable to save resource policy.",
        );
        this.saving = false;
      },
      complete: () => (this.saving = false),
    });
  }
  publish(row: Record<string, unknown>): void {
    if (!this.canWrite) return;
    const id = String(row["id"] ?? "");
    this.api.publishIamResourcePolicy(id).subscribe({
      next: () => this.load(),
      error: () =>
        (this.error = this.i18n.t(
          "admin.iamSaveFailed",
          "Unable to publish resource policy.",
        )),
    });
  }
}
