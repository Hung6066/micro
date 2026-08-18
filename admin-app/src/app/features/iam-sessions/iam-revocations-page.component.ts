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
import { catchError, of } from "rxjs";
import {
  HisHopeDataTableComponent,
  HisHopeDataTableColumn,
  HisHopePageHeaderComponent,
  HisHopePageLayoutComponent,
  HisHopeToolbarComponent,
} from "@his-hope/frontend-foundation/ui";
import {
  HisHopeI18nService,
  HisHopeTranslatePipe,
} from "@his-hope/frontend-foundation/i18n";
import { HisHopePermissionService } from "@his-hope/frontend-foundation/auth";
import { IamRevocation } from "../../core/contracts/admin.contracts";
import { IamApiService } from "../../core/services/iam-api.service";
import { AdminResourceStateController } from "../../core/services/admin-resource-state.controller";

@Component({
  selector: "app-iam-revocations-page",
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    HisHopeDataTableComponent,
    HisHopePageHeaderComponent,
    HisHopePageLayoutComponent,
    HisHopeToolbarComponent,
    HisHopeTranslatePipe,
  ],
  template: ` <hh-page-layout
    ><hh-page-header
      hhPageHeader
      [title]="'admin.revocations' | hhTranslate: 'Revocations'"
      [subtitle]="
        'admin.revocationsSubtitle'
          | hhTranslate: 'Record and inspect explicit principal revocations.'
      " /><hh-toolbar
      hhPageToolbar
      [label]="'admin.revocations' | hhTranslate: 'Revocations'"
      ><span hhToolbarTitle
        >{{ rows.length }} {{ "admin.revocations" | hhTranslate }}</span
      ><button
        *ngIf="canWrite"
        hhToolbarActions
        type="button"
        class="hh-button hh-button--primary"
        (click)="formOpen = !formOpen"
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
    <form
      *ngIf="canWrite && formOpen"
      class="hh-form-card"
      (ngSubmit)="create()"
    >
      <div class="hh-form-grid">
        <label
          >{{ "admin.principalId" | hhTranslate
          }}<input
            name="principalId"
            [(ngModel)]="draft.principalId"
            required /></label
        ><label
          >{{ "admin.principalType" | hhTranslate
          }}<select name="principalType" [(ngModel)]="draft.principalType">
            <option value="human">
              {{ "admin.principalHuman" | hhTranslate: "Human" }}
            </option>
            <option value="workload">
              {{ "admin.principalWorkload" | hhTranslate: "Workload" }}
            </option>
          </select></label
        ><label
          >{{ "admin.reason" | hhTranslate
          }}<input name="reason" [(ngModel)]="draft.reason" required
        /></label>
      </div>
      <button class="hh-button hh-button--primary" type="submit">
        {{ "admin.revoke" | hhTranslate }}
      </button>
    </form>
    <div *ngIf="error" class="hh-state hh-state--error" role="alert">
      {{ error }}
    </div>
    <hh-data-table
      [label]="'admin.revocations' | hhTranslate"
      [columns]="columns"
      [rows]="rows"
      [loading]="loading"
      [empty]="!loading && !error && !rows.length"
    ></hh-data-table
  ></hh-page-layout>`,
})
export class IamRevocationsPageComponent implements OnInit {
  private readonly api = inject(IamApiService);
  private readonly permissions = inject(HisHopePermissionService);
  private readonly i18n = inject(HisHopeI18nService);
  private readonly cdr = inject(ChangeDetectorRef);
  get canWrite(): boolean {
    return this.permissions.has("admin.sessions.revoke");
  }
  rows: Record<string, unknown>[] = [];
  private readonly destroyRef = inject(DestroyRef);
  readonly state = new AdminResourceStateController<{
    revocations: IamRevocation[];
  }>({
    destroyRef: this.destroyRef,
    i18n: this.i18n,
    loadErrorMessageKey: "admin.iamLoadFailed",
    loadErrorFallback: "Unable to load revocations.",
  });
  get loading(): boolean {
    return this.state.loading;
  }
  get error(): string {
    return this.state.error;
  }
  set error(value: string) {
    this.state.setActionError(value);
  }
  formOpen = false;
  draft = { principalId: "", principalType: "human", reason: "" };
  constructor() {
    effect(() => {
      const data = this.state.resource.data();
      if (data) {
        this.rows = data.revocations.map((item) => ({ ...item }));
        this.cdr.markForCheck();
      }
    });
  }
  get columns(): HisHopeDataTableColumn[] {
    this.i18n.locale();
    return [
      {
        key: "principalId",
        label: this.i18n.t("admin.principalId", "Principal"),
      },
      {
        key: "principalType",
        label: this.i18n.t("admin.principalType", "Type"),
      },
      { key: "reason", label: this.i18n.t("admin.reason", "Reason") },
      { key: "occurredAt", label: this.i18n.t("admin.createdAt", "Occurred") },
    ];
  }
  ngOnInit(): void {
    this.load();
  }
  load(): void {
    this.state.load(
      this.api.getIamRevocations().pipe(
        catchError(() => {
          this.error = this.i18n.t(
            "admin.iamLoadFailed",
            "Unable to load revocations.",
          );
          return of({ schemaVersion: "", evaluatedAt: "", revocations: [] });
        }),
      ),
    );
  }
  create(): void {
    if (!this.canWrite || !this.draft.principalId || !this.draft.reason) return;
    this.api.createIamRevocation(this.draft).subscribe({
      next: () => {
        this.formOpen = false;
        this.draft = { principalId: "", principalType: "human", reason: "" };
        this.load();
      },
      error: () =>
        (this.error = this.i18n.t(
          "admin.iamSaveFailed",
          "Unable to create revocation.",
        )),
    });
  }
}
