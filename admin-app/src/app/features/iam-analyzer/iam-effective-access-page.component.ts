import {
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component,
  DestroyRef,
  OnInit,
  effect,
  inject,
} from "@angular/core";
import { CommonModule } from "@angular/common";
import { FormControl, FormGroup, ReactiveFormsModule } from "@angular/forms";
import { catchError, of } from "rxjs";
import {
  HisHopePageHeaderComponent,
  HisHopePageLayoutComponent,
  HisHopeTableStateComponent,
  HisHopeToolbarComponent,
} from "@his-hope/frontend-foundation/ui";
import {
  HisHopeI18nService,
  HisHopeTranslatePipe,
} from "@his-hope/frontend-foundation/i18n";
import { takeUntilDestroyed } from "@angular/core/rxjs-interop";
import { User } from "../../core/contracts/admin.contracts";
import { IamApiService } from "../../core/services/iam-api.service";
import { AdminResourceStateController } from "../../core/services/admin-resource-state.controller";
import { TenantContextService } from "../../core/services/tenant-context.service";

import { HisHopeActionButtonComponent } from "@his-hope/frontend-foundation/ui";
@Component({
  selector: "app-iam-effective-access-page",
  standalone: true,
  imports: [
    HisHopeActionButtonComponent,
    CommonModule,
    ReactiveFormsModule,
    HisHopePageHeaderComponent,
    HisHopePageLayoutComponent,
    HisHopeTableStateComponent,
    HisHopeToolbarComponent,
    HisHopeTranslatePipe,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<hh-page-layout
    ><hh-page-header
      hhPageHeader
      [title]="'admin.effectiveAccess' | hhTranslate: 'Effective access'"
      [subtitle]="
        'admin.effectiveAccessSubtitle'
          | hhTranslate
            : 'Resolve a principal’s effective permissions from assignments and boundaries.'
      "
    /><hh-toolbar hhPageToolbar [label]="'admin.effectiveAccess' | hhTranslate"
      ><hh-action-button
        (pressed)="load()"
        hh-toolbar-actions
        kind="secondary"
        icon="refresh"
        [label]="'admin.refresh' | hhTranslate"
    /></hh-toolbar>
    <form [formGroup]="formGroup">
      <label class="hh-field"
        >{{ "admin.subject" | hhTranslate
        }}<select
          [formControl]="formGroup.controls.selectedUserId"
          [disabled]="loadingUsers || !users.length"
        >
          <option *ngIf="loadingUsers" value="">
            {{ "admin.loading" | hhTranslate: "Loading…" }}
          </option>
          <option *ngIf="!loadingUsers && !users.length" value="">
            {{ "admin.noUsers" | hhTranslate: "No users available" }}
          </option>
          <option *ngFor="let user of users" [value]="user.id">
            {{ user.email || user.userName }}
          </option>
        </select></label
      >
    </form>
    <hh-action-button
      [disabled]="loadingUsers || !formGroup.controls.selectedUserId.value"
      (pressed)="evaluate()"
      kind="primary"
      icon="refresh"
      [label]="'admin.evaluate' | hhTranslate: 'Evaluate'"
    />
    <hh-table-state
      *ngIf="loadingUsers"
      kind="loading"
      message="admin.loading"
    /><hh-table-state
      *ngIf="!loadingUsers && !error && !users.length"
      kind="empty"
      message="admin.noUsers"
    /><hh-table-state
      *ngIf="error"
      kind="error"
      message="admin.iamAnalyzerFailed"
      ><hh-action-button
        kind="secondary"
        icon="refresh"
        [label]="'admin.retry' | hhTranslate: 'Retry'"
        (pressed)="load()"
    /></hh-table-state>
    <pre *ngIf="result">{{ result | json }}</pre>
  </hh-page-layout>`,
})
export class IamEffectiveAccessPageComponent implements OnInit {
  private readonly api = inject(IamApiService);
  private readonly tenantContext = inject(TenantContextService);
  private readonly i18n = inject(HisHopeI18nService);
  private readonly cdr = inject(ChangeDetectorRef);
  private readonly destroyRef = inject(DestroyRef);
  users: User[] = [];
  readonly formGroup = new FormGroup({
    selectedUserId: new FormControl("", { nonNullable: true }),
  });
  result: unknown;
  error = "";
  readonly state = new AdminResourceStateController<User[]>({
    destroyRef: this.destroyRef,
    i18n: this.i18n,
    loadErrorMessageKey: "admin.iamAnalyzerFailed",
    loadErrorFallback: "Analyzer failed.",
  });
  get loadingUsers(): boolean {
    return this.state.loading;
  }
  constructor() {
    effect(() => {
      const users = this.state.resource.data();
      if (users) {
        this.users = users;
        this.formGroup.patchValue({
          selectedUserId:
            this.formGroup.controls.selectedUserId.value || users[0]?.id || "",
        });
        this.cdr.markForCheck();
      }
    });
  }
  ngOnInit(): void {
    this.load();
    this.tenantContext.bindTenantReload(this.destroyRef, () => this.load());
  }
  load(): void {
    this.error = "";
    this.state.load(
      this.api.getUsers().pipe(
        catchError(() => {
          this.error = this.i18n.t(
            "admin.iamAnalyzerFailed",
            "Analyzer failed.",
          );
          return of([]);
        }),
      ),
    );
  }
  evaluate(): void {
    const selectedUserId = this.formGroup.controls.selectedUserId.value;
    if (!selectedUserId) return;
    this.api.getIamEffectiveAccess(selectedUserId).subscribe({
        next: (result) => {
          this.result = result;
          this.cdr.markForCheck();
        },
        error: () => {
          this.error = this.i18n.t(
            "admin.iamAnalyzerFailed",
            "Analyzer failed.",
          );
          this.cdr.markForCheck();
        },
      });
  }
}
