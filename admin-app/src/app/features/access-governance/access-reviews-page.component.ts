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
import { FormsModule } from "@angular/forms";
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
    FormsModule,
    MatButtonModule,
    MatCardModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
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
        ><hh-action-button [disabled]="busy" (pressed)="load()" kind="secondary" icon="refresh" [label]="'admin.refresh' | hhTranslate: 'Refresh'" /></hh-page-header
      >
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
        ><mat-card-content class="form-grid"
          ><mat-form-field appearance="outline"
            ><mat-label>{{
              "admin.subject" | hhTranslate: "Subject"
            }}</mat-label
            ><mat-select [(ngModel)]="draft.subjectUserId"
              ><mat-option *ngFor="let user of users" [value]="user.id">{{
                user.email || user.userName
              }}</mat-option></mat-select
            ></mat-form-field
          ><mat-form-field appearance="outline"
            ><mat-label>{{
              "admin.roles" | hhTranslate: "Roles to review"
            }}</mat-label
            ><mat-select multiple [(ngModel)]="draft.roleIds"
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
              [(ngModel)]="draft.dueDays" /></mat-form-field
          ><hh-action-button [disabled]="
              busy || !canWrite || !draft.subjectUserId || !draft.roleIds.length
            " (pressed)="create()" kind="primary" icon="add" [label]="'admin.createAccessReview' | hhTranslate: 'Create review'" /></mat-card-content
        ></mat-card
      >
      <mat-card class="table-card"
        ><mat-card-header
          ><mat-card-title>{{
            "admin.accessReviews" | hhTranslate: "Access reviews"
          }}</mat-card-title></mat-card-header
        ><mat-card-content class="table-wrap"
          ><table>
            <thead>
              <tr>
                <th>{{ "admin.subject" | hhTranslate: "Subject" }}</th>
                <th>{{ "admin.reviewer" | hhTranslate: "Reviewer" }}</th>
                <th>{{ "admin.status" | hhTranslate: "Status" }}</th>
                <th>{{ "admin.dueAt" | hhTranslate: "Due" }}</th>
                <th>{{ "admin.actions" | hhTranslate: "Actions" }}</th>
              </tr>
            </thead>
            <tbody>
              <tr *ngFor="let item of reviews">
                <td>{{ displayUser(item.subjectUserId) }}</td>
                <td>{{ item.reviewer }}</td>
                <td>{{ item.status }}</td>
                <td>{{ item.dueAt | date: "short" }}</td>
                <td>
                  <button
                    mat-button
                    color="primary"
                    *ngIf="item.status === 'pending'"
                    (click)="certify(item)"
                    [disabled]="busy || !canWrite"
                  >
                    {{ "admin.certify" | hhTranslate: "Certify" }}</button
                  ><button
                    mat-button
                    color="warn"
                    *ngIf="item.status === 'pending'"
                    (click)="revoke(item)"
                    [disabled]="busy || !canWrite"
                  >
                    {{ "admin.revoke" | hhTranslate: "Revoke" }}
                  </button>
                </td>
              </tr>
              <tr *ngIf="!reviews.length">
                <td colspan="5">
                  {{
                    "admin.noAccessReviews" | hhTranslate: "No access reviews."
                  }}
                </td>
              </tr>
            </tbody>
          </table></mat-card-content
        ></mat-card
      >
    </hh-page-layout>
  `,
  styles: [
    ":host{display:block}.form-grid{display:grid;gap:var(--space-3)}.notice{padding:var(--space-3);border:1px solid var(--border-default);border-radius:var(--radius-card);background:var(--surface-muted);color:var(--text-secondary)}.error{color:var(--color-danger)}.table-card{margin-top:var(--space-4)}.table-wrap{overflow:auto}table{width:100%;border-collapse:collapse}th,td{text-align:left;padding:var(--space-2);border-bottom:1px solid var(--border-subtle);white-space:nowrap}",
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
  draft = { subjectUserId: "", roleIds: [] as string[], dueDays: 30 };
  get canWrite(): boolean {
    return this.permissions.has("admin.roles.write");
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
    this.api.createAccessReview(this.draft).subscribe({
      next: () => {
        this.toast.success(
          this.i18n.t("admin.accessReviewCreated", "Access review created."),
          { duration: 3000 },
        );
        this.draft = { subjectUserId: "", roleIds: [], dueDays: 30 };
        this.load();
      },
      error: () => this.fail(),
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
  private mutate(
    operation: import("rxjs").Observable<unknown>,
    key: string,
    fallback: string,
  ): void {
    if (!this.canWrite) return;
    this.busy = true;
    operation.subscribe({
      next: () => {
        this.toast.success(this.i18n.t(key, fallback), { duration: 3000 });
        this.load();
      },
      error: () => this.fail(),
    });
  }
  private fail(): void {
    this.error = this.i18n.t(
      "admin.loadAccessGovernanceFailed",
      "Unable to load access review data.",
    );
    this.busy = false;
    this.cdr.markForCheck();
  }
}