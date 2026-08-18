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
import { MatSnackBar, MatSnackBarModule } from "@angular/material/snack-bar";
import {
  AccessGovernanceApiService,
  BreakGlassRequest,
  PermissionDefinition,
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
} from "@his-hope/frontend-foundation/ui";
import { AdminResourceStateController } from "../../core/services/admin-resource-state.controller";

import { HisHopeActionButtonComponent } from "@his-hope/frontend-foundation/ui";
@Component({
  selector: "app-break-glass-page",
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
    MatSnackBarModule,
    HisHopePageHeaderComponent,
    HisHopePageLayoutComponent,
    HisHopeTranslatePipe,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <hh-page-layout
      ><hh-page-header
        hhPageHeader
        [title]="'admin.breakGlass' | hhTranslate: 'Break-glass access'"
        [subtitle]="
          'admin.breakGlassSubtitle'
            | hhTranslate
              : 'Emergency elevation with short expiry and full audit.'
        "
        ><hh-action-button [disabled]="busy" (pressed)="load()" kind="secondary" icon="refresh" [label]="'admin.refresh' | hhTranslate: 'Refresh'" /></hh-page-header
      >
      <p class="warning">
        {{
          "admin.breakGlassWarning"
            | hhTranslate
              : "Use only for an audited emergency. The server enforces MFA, approval and expiry."
        }}
      </p>
      <p class="error" *ngIf="error">{{ error }}</p>
      <mat-card
        ><mat-card-header
          ><mat-card-title>{{
            "admin.requestBreakGlass" | hhTranslate: "Request break-glass"
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
              "admin.permission" | hhTranslate: "Permission"
            }}</mat-label
            ><mat-select [(ngModel)]="draft.permissionCode"
              ><mat-option
                *ngFor="let permission of permissions"
                [value]="permission.code"
                >{{ permission.name }} · {{ permission.code }}</mat-option
              ></mat-select
            ></mat-form-field
          ><mat-form-field appearance="outline"
            ><mat-label>{{
              "admin.facility" | hhTranslate: "Facility"
            }}</mat-label
            ><input matInput [(ngModel)]="draft.facilityId" /></mat-form-field
          ><mat-form-field appearance="outline"
            ><mat-label>{{ "admin.reason" | hhTranslate: "Reason" }}</mat-label
            ><textarea
              matInput
              rows="2"
              [(ngModel)]="draft.reason"
            ></textarea></mat-form-field
          ><hh-action-button [disabled]="
              busy ||
              !canWrite ||
              !draft.subjectUserId ||
              !draft.permissionCode ||
              !draft.facilityId ||
              draft.reason.trim().length < 10
            " (pressed)="create()" kind="danger" icon="link_off" [label]="'admin.requestBreakGlass' | hhTranslate: 'Request break-glass'" /></mat-card-content
        ></mat-card
      >
      <mat-card class="table-card"
        ><mat-card-header
          ><mat-card-title>{{
            "admin.breakGlassRequests" | hhTranslate: "Break-glass requests"
          }}</mat-card-title></mat-card-header
        ><mat-card-content class="table-wrap"
          ><table>
            <thead>
              <tr>
                <th>{{ "admin.subject" | hhTranslate: "Subject" }}</th>
                <th>{{ "admin.permission" | hhTranslate: "Permission" }}</th>
                <th>{{ "admin.status" | hhTranslate: "Status" }}</th>
                <th>{{ "admin.expires" | hhTranslate: "Expires" }}</th>
                <th>{{ "admin.actions" | hhTranslate: "Actions" }}</th>
              </tr>
            </thead>
            <tbody>
              <tr *ngFor="let item of requests">
                <td>{{ displayUser(item.subjectUserId) }}</td>
                <td>{{ item.permissionCode }}</td>
                <td>{{ item.status }}</td>
                <td>{{ item.expiresAt | date: "short" }}</td>
                <td>
                  <button
                    mat-button
                    color="primary"
                    *ngIf="item.status === 'pending'"
                    (click)="approve(item)"
                    [disabled]="busy || !canWrite"
                  >
                    {{ "admin.approve" | hhTranslate: "Approve" }}</button
                  ><button
                    mat-button
                    color="warn"
                    *ngIf="item.status === 'approved'"
                    (click)="revoke(item)"
                    [disabled]="busy || !canWrite"
                  >
                    {{ "admin.revoke" | hhTranslate: "Revoke" }}
                  </button>
                </td>
              </tr>
              <tr *ngIf="!requests.length">
                <td colspan="5">
                  {{
                    "admin.noBreakGlassRequests"
                      | hhTranslate: "No break-glass requests."
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
    ":host{display:block}.form-grid{display:grid;gap:var(--space-3)}.warning{padding:var(--space-3);border:1px solid var(--color-warning);border-radius:var(--radius-card);color:var(--text-secondary)}.error{color:var(--color-danger)}.table-card{margin-top:var(--space-4)}.table-wrap{overflow:auto}table{width:100%;border-collapse:collapse}th,td{text-align:left;padding:var(--space-2);border-bottom:1px solid var(--border-subtle);white-space:nowrap}",
  ],
})
export class BreakGlassPageComponent implements OnInit {
  private readonly api = inject(AccessGovernanceApiService);
  private readonly permissionsService = inject(HisHopePermissionService);
  private readonly i18n = inject(HisHopeI18nService);
  private readonly snack = inject(MatSnackBar);
  private readonly cdr = inject(ChangeDetectorRef);
  private readonly destroyRef = inject(DestroyRef);
  readonly state = new AdminResourceStateController<{
    users: User[];
    permissions: PermissionDefinition[];
    requests: BreakGlassRequest[];
  }>({
    destroyRef: this.destroyRef,
    i18n: this.i18n,
    loadErrorMessageKey: "admin.loadAccessGovernanceFailed",
    loadErrorFallback: "Unable to load break-glass data.",
  });
  users: User[] = [];
  permissions: PermissionDefinition[] = [];
  requests: BreakGlassRequest[] = [];
  busy = false;
  get error(): string {
    return this.state.error;
  }
  set error(value: string) {
    this.state.setActionError(value);
  }
  draft = {
    subjectUserId: "",
    permissionCode: "",
    facilityId: "",
    reason: "",
    durationMinutes: 15,
  };
  get canWrite(): boolean {
    return this.permissionsService.has("admin.breakglass.write");
  }
  ngOnInit(): void {
    this.load();
  }
  load(): void {
    this.error = "";
    this.state.load(
      forkJoin({
        users: this.api.getUsers(),
        permissions: this.api.getPermissions(),
        requests: this.api.getBreakGlassRequests(),
      }).pipe(
        tap((state) => {
          this.users = state.users;
          this.permissions = state.permissions;
          this.requests = state.requests;
          this.cdr.markForCheck();
        }),
        catchError(() => {
          this.error = this.i18n.t(
            "admin.loadAccessGovernanceFailed",
            "Unable to load break-glass data.",
          );
          return of({ users: [], permissions: [], requests: [] });
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
    this.mutate(
      this.api.createBreakGlassRequest(this.draft),
      "admin.breakGlassCreated",
      "Break-glass request created.",
      () =>
        (this.draft = {
          subjectUserId: "",
          permissionCode: "",
          facilityId: "",
          reason: "",
          durationMinutes: 15,
        }),
    );
  }
  approve(item: BreakGlassRequest): void {
    this.mutate(
      this.api.approveBreakGlassRequest(item.id),
      "admin.breakGlassApproved",
      "Break-glass request approved.",
    );
  }
  revoke(item: BreakGlassRequest): void {
    this.mutate(
      this.api.revokeBreakGlassRequest(item.id),
      "admin.breakGlassRevoked",
      "Break-glass request revoked.",
    );
  }
  private mutate(
    operation: import("rxjs").Observable<unknown>,
    key: string,
    fallback: string,
    after?: () => void,
  ): void {
    if (!this.canWrite) return;
    this.busy = true;
    operation.subscribe({
      next: () => {
        after?.();
        this.snack.open(
          this.i18n.t(key, fallback),
          this.i18n.t("admin.close", "Close"),
          { duration: 3000 },
        );
        this.load();
      },
      error: () => this.fail(),
    });
  }
  private fail(): void {
    this.error = this.i18n.t(
      "admin.loadAccessGovernanceFailed",
      "Unable to load break-glass data.",
    );
    this.busy = false;
    this.cdr.markForCheck();
  }
}