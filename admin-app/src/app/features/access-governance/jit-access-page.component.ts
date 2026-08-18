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
  AccessRequest,
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
} from "@his-hope/frontend-foundation/ui";
import { AdminResourceStateController } from "../../core/services/admin-resource-state.controller";

import { HisHopeActionButtonComponent } from "@his-hope/frontend-foundation/ui";
@Component({
  selector: "app-jit-access-page",
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
        [title]="'admin.jitAccess' | hhTranslate: 'JIT access'"
        [subtitle]="
          'admin.jitAccessSubtitle'
            | hhTranslate: 'Time-limited elevation with explicit expiry.'
        "
        ><hh-action-button [disabled]="busy" (pressed)="load()" kind="secondary" icon="refresh" [label]="'admin.refresh' | hhTranslate: 'Refresh'" /></hh-page-header
      >
      <p class="notice">
        {{
          "admin.jitBoundary"
            | hhTranslate
              : "JIT is implemented by the server access-request workflow with expiry, MFA, maker-checker and audit enforcement."
        }}
      </p>
      <p class="error" *ngIf="error">{{ error }}</p>
      <mat-card
        ><mat-card-header
          ><mat-card-title>{{
            "admin.requestJitAccess" | hhTranslate: "Request JIT access"
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
            ><mat-label>{{ "admin.roles" | hhTranslate: "Roles" }}</mat-label
            ><mat-select multiple [(ngModel)]="draft.roleIds"
              ><mat-option *ngFor="let role of roles" [value]="role.id">{{
                role.name
              }}</mat-option></mat-select
            ></mat-form-field
          ><mat-form-field appearance="outline"
            ><mat-label>{{ "admin.reason" | hhTranslate: "Reason" }}</mat-label
            ><textarea
              matInput
              rows="2"
              [(ngModel)]="draft.reason"
            ></textarea></mat-form-field
          ><mat-form-field appearance="outline"
            ><mat-label>{{
              "admin.expiryHours" | hhTranslate: "Expiry hours"
            }}</mat-label
            ><input
              matInput
              type="number"
              min="1"
              max="72"
              [(ngModel)]="draft.expiryHours" /></mat-form-field
          ><hh-action-button [disabled]="
              busy ||
              !canWrite ||
              !draft.subjectUserId ||
              !draft.roleIds.length ||
              draft.reason.trim().length < 10
            " (pressed)="create()" kind="primary" icon="add" [label]="'admin.createJitRequest' | hhTranslate: 'Create JIT request'" /></mat-card-content
        ></mat-card
      >
      <mat-card class="table-card"
        ><mat-card-header
          ><mat-card-title>{{
            "admin.activeJitRequests" | hhTranslate: "JIT requests"
          }}</mat-card-title></mat-card-header
        ><mat-card-content class="table-wrap"
          ><table>
            <thead>
              <tr>
                <th>{{ "admin.subject" | hhTranslate: "Subject" }}</th>
                <th>{{ "admin.status" | hhTranslate: "Status" }}</th>
                <th>{{ "admin.expires" | hhTranslate: "Expires" }}</th>
                <th>{{ "admin.actions" | hhTranslate: "Actions" }}</th>
              </tr>
            </thead>
            <tbody>
              <tr *ngFor="let item of requests">
                <td>{{ displayUser(item.subjectUserId) }}</td>
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
                    *ngIf="item.status === 'pending'"
                    (click)="reject(item)"
                    [disabled]="busy || !canWrite"
                  >
                    {{ "admin.reject" | hhTranslate: "Reject" }}
                  </button>
                </td>
              </tr>
              <tr *ngIf="!requests.length">
                <td colspan="4">
                  {{ "admin.noJitRequests" | hhTranslate: "No JIT requests." }}
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
export class JitAccessPageComponent implements OnInit {
  private readonly api = inject(AccessGovernanceApiService);
  private readonly permissionService = inject(HisHopePermissionService);
  private readonly i18n = inject(HisHopeI18nService);
  private readonly snack = inject(MatSnackBar);
  private readonly cdr = inject(ChangeDetectorRef);
  private readonly destroyRef = inject(DestroyRef);
  readonly state = new AdminResourceStateController<{
    users: User[];
    roles: Role[];
    requests: AccessRequest[];
  }>({
    destroyRef: this.destroyRef,
    i18n: this.i18n,
    loadErrorMessageKey: "admin.loadAccessGovernanceFailed",
    loadErrorFallback: "Unable to load JIT data.",
  });
  users: User[] = [];
  roles: Role[] = [];
  requests: AccessRequest[] = [];
  busy = false;
  get error(): string {
    return this.state.error;
  }
  set error(value: string) {
    this.state.setActionError(value);
  }
  draft = {
    subjectUserId: "",
    roleIds: [] as string[],
    reason: "",
    expiryHours: 8,
  };
  get canWrite(): boolean {
    return this.permissionService.has("admin.roles.write");
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
        requests: this.api.getAccessRequests(),
      }).pipe(
        tap((state) => {
          this.users = state.users;
          this.roles = state.roles;
          this.requests = state.requests.filter(
            (x) => new Date(x.expiresAt).getTime() > Date.now(),
          );
          this.cdr.markForCheck();
        }),
        catchError(() => {
          this.error = this.i18n.t(
            "admin.loadAccessGovernanceFailed",
            "Unable to load JIT data.",
          );
          return of({ users: [], roles: [], requests: [] });
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
    this.api.createAccessRequest(this.draft).subscribe({
      next: () => {
        this.snack.open(
          this.i18n.t("admin.jitCreated", "JIT request created."),
          this.i18n.t("admin.close", "Close"),
          { duration: 3000 },
        );
        this.draft = {
          subjectUserId: "",
          roleIds: [],
          reason: "",
          expiryHours: 8,
        };
        this.load();
      },
      error: () => this.fail(),
    });
  }
  approve(item: AccessRequest): void {
    this.mutate(
      this.api.approveAccessRequest(item.id),
      "admin.accessRequestApproved",
      "JIT request approved.",
    );
  }
  reject(item: AccessRequest): void {
    this.mutate(
      this.api.rejectAccessRequest(item.id),
      "admin.accessRequestRejected",
      "JIT request rejected.",
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
      "Unable to load JIT data.",
    );
    this.busy = false;
    this.cdr.markForCheck();
  }
}