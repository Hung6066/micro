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
import { AuthorizationPolicy } from "../../core/contracts/admin.contracts";
import { IamApiService } from "../../core/services/iam-api.service";
import { AdminResourceStateController } from "../../core/services/admin-resource-state.controller";

@Component({
  selector: "app-policies-page",
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
      [title]="'admin.policies' | hhTranslate: 'Policies'"
      [subtitle]="
        'admin.policiesSubtitle'
          | hhTranslate
            : 'Versioned authorization policies with lint and publish controls.'
      "
    /><hh-toolbar
      hhPageToolbar
      [label]="'admin.policies' | hhTranslate: 'Policies'"
      ><span hhToolbarTitle
        >{{ policies.length }} {{ "admin.policies" | hhTranslate }}</span
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
          }}<input
            name="key"
            [(ngModel)]="draft.key"
            [disabled]="!!editingId"
            required /></label
        ><label
          >{{ "admin.description" | hhTranslate
          }}<input
            name="description"
            [(ngModel)]="draft.description"
            required /></label
        ><label
          >{{ "admin.owner" | hhTranslate
          }}<input name="owner" [(ngModel)]="draft.owner" required /></label
        ><label
          >{{ "admin.rulesJson" | hhTranslate: "Rules JSON"
          }}<textarea
            name="rulesJson"
            rows="4"
            [(ngModel)]="draft.rulesJson"
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
      [label]="'admin.policies' | hhTranslate: 'Policies'"
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
          *ngIf="canWrite"
          type="button"
          class="hh-icon-button hh-icon-button--small"
          (click)="lint(row)"
          [attr.aria-label]="'admin.lint' | hhTranslate: 'Lint'"
          [attr.title]="'admin.lint' | hhTranslate: 'Lint'"
        >
          <span class="material-icons" aria-hidden="true">rule</span></button
        ><button
          *ngIf="canWrite && row['lifecycleStatus'] !== 'published'"
          type="button"
          class="hh-icon-button hh-icon-button--small"
          (click)="publish(row)"
          [attr.aria-label]="'admin.publish' | hhTranslate"
          [attr.title]="'admin.publish' | hhTranslate"
        >
          <span class="material-icons" aria-hidden="true">publish</span></button
        ><button
          *ngIf="canWrite && row['lifecycleStatus'] === 'published'"
          type="button"
          class="hh-icon-button hh-icon-button--small hh-icon-button--danger"
          (click)="rollback(row)"
          [attr.aria-label]="'admin.rollback' | hhTranslate"
          [attr.title]="'admin.rollback' | hhTranslate"
        >
          <span class="material-icons" aria-hidden="true">undo</span>
        </button></ng-template
      ></hh-data-table
    ></hh-page-layout
  >`,
})
export class PoliciesPageComponent implements OnInit {
  private readonly api = inject(IamApiService);
  private readonly permissions = inject(HisHopePermissionService);
  private readonly i18n = inject(HisHopeI18nService);
  private readonly cdr = inject(ChangeDetectorRef);
  get canWrite(): boolean {
    return this.permissions.has("admin.settings.write");
  }
  private readonly destroyRef = inject(DestroyRef);
  readonly state = new AdminResourceStateController<AuthorizationPolicy[]>({
    destroyRef: this.destroyRef,
    i18n: this.i18n,
    loadErrorMessageKey: "admin.iamLoadFailed",
    loadErrorFallback: "Unable to load policies.",
  });
  policies: AuthorizationPolicy[] = [];
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
    description: "",
    owner: "identity-service",
    rulesJson: '{\n  "statements": []\n}',
  };
  get columns(): HisHopeDataTableColumn[] {
    this.i18n.locale();
    return [
      { key: "key", label: this.i18n.t("admin.key", "Key"), sortable: true },
      {
        key: "description",
        label: this.i18n.t("admin.description", "Description"),
      },
      { key: "owner", label: this.i18n.t("admin.owner", "Owner") },
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
      const policies = this.state.resource.data();
      if (policies) {
        this.policies = policies;
        this.rows = policies.map((item) => ({ ...item }));
        this.cdr.markForCheck();
      }
    });
  }
  load(): void {
    this.state.load(this.api.getAuthorizationPolicies());
  }
  toggleForm(): void {
    if (!this.canWrite) return;
    this.formOpen = !this.formOpen;
    this.editingId = "";
    this.draft = {
      key: "",
      description: "",
      owner: "identity-service",
      rulesJson: '{\n  "statements": []\n}',
    };
  }
  edit(row: Record<string, unknown>): void {
    if (!this.canWrite) return;
    const item = this.policies.find((x) => x.id === String(row["id"]));
    if (!item) return;
    this.editingId = item.id;
    this.formOpen = true;
    this.draft = {
      key: item.key,
      description: item.description,
      owner: item.owner,
      rulesJson: item.rulesJson,
    };
  }
  save(): void {
    if (
      !this.canWrite ||
      !this.draft.description.trim() ||
      !this.draft.rulesJson.trim()
    )
      return;
    this.saving = true;
    const call = this.editingId
      ? this.api.updateAuthorizationPolicy(this.editingId, {
          description: this.draft.description,
          owner: this.draft.owner,
          rulesJson: this.draft.rulesJson,
        })
      : this.api.createAuthorizationPolicy(this.draft);
    call.subscribe({
      next: () => {
        this.formOpen = false;
        this.editingId = "";
        this.load();
      },
      error: () => {
        this.error = this.i18n.t(
          "admin.iamSaveFailed",
          "Unable to save policy.",
        );
        this.saving = false;
      },
      complete: () => (this.saving = false),
    });
  }
  lint(row: Record<string, unknown>): void {
    if (!this.canWrite) return;
    const id = String(row["id"] ?? "");
    this.api.lintAuthorizationPolicy(id).subscribe({
      next: (result) =>
        (this.error = result.valid ? "" : result.errors.join("; ")),
      error: () =>
        (this.error = this.i18n.t(
          "admin.iamAnalyzerFailed",
          "Policy analysis failed.",
        )),
    });
  }
  publish(row: Record<string, unknown>): void {
    if (!this.canWrite) return;
    const id = String(row["id"] ?? "");
    this.api.publishAuthorizationPolicy(id).subscribe({
      next: () => this.load(),
      error: () =>
        (this.error = this.i18n.t(
          "admin.iamSaveFailed",
          "Unable to publish policy.",
        )),
    });
  }
  rollback(row: Record<string, unknown>): void {
    if (!this.canWrite) return;
    const id = String(row["id"] ?? "");
    this.api.rollbackAuthorizationPolicy(id).subscribe({
      next: () => this.load(),
      error: () =>
        (this.error = this.i18n.t(
          "admin.iamSaveFailed",
          "Unable to rollback policy.",
        )),
    });
  }
}
