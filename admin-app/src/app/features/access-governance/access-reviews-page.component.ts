import {
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component,
  DestroyRef,
  OnInit,
  inject,
} from "@angular/core";
import { CommonModule } from "@angular/common";
import { FormControl, FormGroup, ReactiveFormsModule } from "@angular/forms";
import { MatButtonModule } from "@angular/material/button";
import { MatCardModule } from "@angular/material/card";
import { MatFormFieldModule } from "@angular/material/form-field";
import { MatInputModule } from "@angular/material/input";
import { MatSelectModule } from "@angular/material/select";
import {
  AccessGovernanceApiService,
  AccessReview,
  Role,
  User,
} from "../../core/services/access-governance-api.service";
import { catchError, forkJoin, of, tap } from "rxjs";
import {
  HisHopeI18nService,
  HisHopeTranslatePipe,
} from "@his-hope/frontend-foundation/i18n";
import { HisHopePermissionService } from "@his-hope/frontend-foundation/auth";
import {
  HisHopeDataTableCellDirective,
  HisHopeDataTableColumn,
  HisHopeDataTableComponent,
  HisHopePageHeaderComponent,
  HisHopePageLayoutComponent,
  HisHopeToastService,
} from "@his-hope/frontend-foundation/ui";
import { AdminResourceStateController } from "../../core/services/admin-resource-state.controller";

import { HisHopeActionButtonComponent } from "@his-hope/frontend-foundation/ui";
@Component({
  selector: "app-access-reviews-page",
  standalone: true,
  imports: [
    HisHopeActionButtonComponent,
    CommonModule,
    ReactiveFormsModule,
    MatButtonModule,
    MatCardModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    HisHopeDataTableCellDirective,
    HisHopeDataTableComponent,
    HisHopePageHeaderComponent,
    HisHopePageLayoutComponent,
    HisHopeTranslatePipe,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <hh-page-layout
      ><hh-page-header
        hhPageHeader
        [title]="'admin.accessReviews' | hhTranslate: 'Access reviews'"
        [subtitle]="
          'admin.accessReviewsSubtitle'
            | hhTranslate
              : 'Certify or revoke user access on a governed schedule.'
        "
        ><hh-action-button
          [disabled]="busy"
          (pressed)="load()"
          kind="secondary"
          icon="refresh"
          [label]="'admin.refresh' | hhTranslate: 'Refresh'"
      /></hh-page-header>
      <p class="notice">
        {{
          "admin.governanceBoundary"
            | hhTranslate
              : "Certification and separation-of-duties checks remain server-side."
        }}
      </p>
      <p class="error" *ngIf="error">{{ error }}</p>
      <mat-card
        ><mat-card-header
          ><mat-card-title>{{
            "admin.startAccessReview" | hhTranslate: "Start access review"
          }}</mat-card-title></mat-card-header
        ><mat-card-content
          ><form [formGroup]="formGroup" class="form-grid">
            <mat-form-field appearance="outline"
              ><mat-label>{{
                "admin.subject" | hhTranslate: "Subject"
              }}</mat-label
              ><mat-select [formControl]="formGroup.controls.subjectUserId"
                ><mat-option *ngFor="let user of users" [value]="user.id">{{
                  user.email || user.userName
                }}</mat-option></mat-select
              ></mat-form-field
            ><mat-form-field appearance="outline"
              ><mat-label>{{
                "admin.roles" | hhTranslate: "Roles to review"
              }}</mat-label
              ><mat-select multiple [formControl]="formGroup.controls.roleIds"
                ><mat-option *ngFor="let role of roles" [value]="role.id">{{
                  role.name
                }}</mat-option></mat-select
              ></mat-form-field
            ><mat-form-field appearance="outline"
              ><mat-label>{{
                "admin.dueDays" | hhTranslate: "Due in days"
              }}</mat-label
              ><input
                matInput
                type="number"
                min="1"
                max="90"
                [formControl]="formGroup.controls.dueDays" /></mat-form-field
            ><hh-action-button
              [disabled]="busy || !canWrite || !canSubmit"
              (pressed)="create()"
              kind="primary"
              icon="add"
              [label]="
                'admin.createAccessReview' | hhTranslate: 'Create review'
              "
            /></form></mat-card-content
      ></mat-card>
      <mat-card class="table-card"
        ><mat-card-header
          ><mat-card-title>{{
            "admin.accessReviews" | hhTranslate: "Access reviews"
          }}</mat-card-title></mat-card-header
        ><mat-card-content
          ><hh-data-table
            [label]="'admin.accessReviews' | hhTranslate"
            [columns]="columns"
            [rows]="rows"
            [loading]="state.loading"
            [empty]="!state.loading && !error && !rows.length"
            ><ng-template hhDataTableCell="actions" let-row
              ><div class="action-cell">
                <hh-action-button
                  *ngIf="row['status'] === 'pending'"
                  kind="primary"
                  mode="icon-only"
                  icon="check"
                  [label]="'admin.certify' | hhTranslate: 'Certify'"
                  [disabled]="busy || !canWrite"
                  (pressed)="certifyByRow(row)"
                />
                <hh-action-button
                  *ngIf="row['status'] === 'pending'"
                  kind="danger"
                  mode="icon-only"
                  icon="link_off"
                  [label]="'admin.revoke' | hhTranslate: 'Revoke'"
                  [disabled]="busy || !canWrite"
                  (pressed)="revokeByRow(row)"
                /></div></ng-template></hh-data-table></mat-card-content
      ></mat-card>
    </hh-page-layout>
  `,
  styles: [
    ":host{display:block}.form-grid{display:grid;gap:var(--space-3)}.notice{padding:var(--space-3);border:1px solid var(--border-default);border-radius:var(--radius-card);background:var(--surface-muted);color:var(--text-secondary)}.error{color:var(--color-danger)}.table-card{margin-top:var(--space-4)}.action-cell{display:flex;gap:var(--space-2)}",
  ],
})
export class AccessReviewsPageComponent implements OnInit {
  private readonly api = inject(AccessGovernanceApiService);
  private readonly permissions = inject(HisHopePermissionService);
  private readonly i18n = inject(HisHopeI18nService);
  private readonly toast = inject(HisHopeToastService);
  private readonly cdr = inject(ChangeDetectorRef);
  private readonly destroyRef = inject(DestroyRef);
  readonly state = new AdminResourceStateController<{
    users: User[];
    roles: Role[];
    reviews: AccessReview[];
  }>({
    destroyRef: this.destroyRef,
    i18n: this.i18n,
    loadErrorMessageKey: "admin.loadAccessGovernanceFailed",
    loadErrorFallback: "Unable to load access review data.",
  });
  users: User[] = [];
  roles: Role[] = [];
  reviews: AccessReview[] = [];
  busy = false;
  get error(): string {
    return this.state.error;
  }
  set error(value: string) {
    this.state.setActionError(value);
  }
  readonly formGroup = new FormGroup({
    subjectUserId: new FormControl("", { nonNullable: true }),
    roleIds: new FormControl<string[]>([], { nonNullable: true }),
    dueDays: new FormControl(30, { nonNullable: true }),
  });
  get canWrite(): boolean {
    return this.permissions.has("admin.roles.write");
  }
  get canSubmit(): boolean {
    const value = this.formGroup.getRawValue();
    return !!value.subjectUserId && value.roleIds.length > 0;
  }
  get rows(): Record<string, unknown>[] {
    return this.reviews.map((item) => ({ ...item }));
  }
  get columns(): HisHopeDataTableColumn[] {
    this.i18n.locale();
    return [
      {
        key: "subjectUserId",
        label: this.i18n.t("admin.subject", "Subject"),
        computed: (row) => this.displayUser(String(row["subjectUserId"] ?? "")),
      },
      { key: "reviewer", label: this.i18n.t("admin.reviewer", "Reviewer") },
      { key: "status", label: this.i18n.t("admin.status", "Status") },
      {
        key: "dueAt",
        label: this.i18n.t("admin.dueAt", "Due"),
        format: "dateTime",
      },
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
  load(): void {
    this.error = "";
    this.state.load(
      forkJoin({
        users: this.api.getUsers(),
        roles: this.api.getRoles(),
        reviews: this.api.getAccessReviews(),
      }).pipe(
        tap((state) => {
          this.users = state.users;
          this.roles = state.roles;
          this.reviews = state.reviews;
          this.cdr.markForCheck();
        }),
        catchError(() => {
          this.error = this.i18n.t(
            "admin.loadAccessGovernanceFailed",
            "Unable to load access review data.",
          );
          return of({ users: [], roles: [], reviews: [] });
        }),
      ),
    );
  }
  displayUser(id: string): string {
    const user = this.users.find((x) => x.id === id);
    return user?.email || user?.userName || id;
  }
  create(): void {
    if (!this.canWrite) return;
    this.busy = true;
    this.cdr.markForCheck();
    this.api.createAccessReview(this.formGroup.getRawValue()).subscribe({
      next: () => {
        this.busy = false;
        this.toast.success(
          this.i18n.t("admin.accessReviewCreated", "Access review created."),
          { duration: 3000 },
        );
        this.formGroup.reset({ subjectUserId: "", roleIds: [], dueDays: 30 });
        this.cdr.markForCheck();
        this.load();
      },
      error: () =>
        this.fail(
          "admin.accessReviewCreateFailed",
          "Unable to create access review.",
        ),
    });
  }
  certify(item: AccessReview): void {
    this.mutate(
      this.api.certifyAccessReview(item.id),
      "admin.accessReviewCertified",
      "Access review certified.",
    );
  }
  revoke(item: AccessReview): void {
    this.mutate(
      this.api.revokeAccessReview(item.id),
      "admin.accessReviewRevoked",
      "Access review revoked.",
    );
  }
  certifyByRow(row: Record<string, unknown>): void {
    const item = this.reviews.find((review) => review.id === row["id"]);
    if (item) {
      this.certify(item);
    }
  }
  revokeByRow(row: Record<string, unknown>): void {
    const item = this.reviews.find((review) => review.id === row["id"]);
    if (item) {
      this.revoke(item);
    }
  }
  private mutate(
    operation: import("rxjs").Observable<unknown>,
    key: string,
    fallback: string,
  ): void {
    if (!this.canWrite) return;
    this.busy = true;
    this.cdr.markForCheck();
    operation.subscribe({
      next: () => {
        this.busy = false;
        this.toast.success(this.i18n.t(key, fallback), { duration: 3000 });
        this.cdr.markForCheck();
        this.load();
      },
      error: () => this.fail("admin.accessReviewMutationFailed", fallback),
    });
  }
  private fail(key: string, fallback: string): void {
    this.error = this.i18n.t(key, fallback);
    this.toast.error(this.error, { duration: 5000 });
    this.busy = false;
    this.cdr.markForCheck();
  }
}
