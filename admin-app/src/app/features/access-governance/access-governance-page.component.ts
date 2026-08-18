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
import { MatButtonModule } from "@angular/material/button";
import { MatCardModule } from "@angular/material/card";
import { MatFormFieldModule } from "@angular/material/form-field";
import { MatInputModule } from "@angular/material/input";
import { MatSelectModule } from "@angular/material/select";
import { MatSnackBar, MatSnackBarModule } from "@angular/material/snack-bar";
import { catchError, forkJoin, of, tap } from "rxjs";
import { ActivatedRoute } from "@angular/router";
import {
  AccessGovernanceApiService,
  AccessRequest,
  AccessReview,
  BreakGlassRequest,
  PermissionDefinition,
  Role,
  User,
} from "../../core/services/access-governance-api.service";
import { HisHopePermissionService } from "@his-hope/frontend-foundation/auth";
import { HisHopeResourceState } from "@his-hope/frontend-foundation/query";
import {
  HisHopePageHeaderComponent,
  HisHopePageLayoutComponent,
} from "@his-hope/frontend-foundation/ui";
import {
  HisHopeI18nService,
  HisHopeTranslatePipe,
} from "@his-hope/frontend-foundation/i18n";
import { AccessGovernanceWorkflowsComponent } from "./access-governance-workflows.component";

@Component({
  selector: "app-access-governance-page",
  standalone: true,
  imports: [
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
    AccessGovernanceWorkflowsComponent,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <hh-page-layout>
      <hh-page-header
        hhPageHeader
        [title]="'admin.accessGovernance' | hhTranslate: 'Access governance'"
        [subtitle]="
          'admin.accessGovernanceSubtitle'
            | hhTranslate
              : 'Time-bound access requests, independent reviews and emergency elevation.'
        "
      >
        <button
          mat-stroked-button
          type="button"
          (click)="reload()"
          [disabled]="busy"
        >
          {{ "admin.refresh" | hhTranslate: "Refresh" }}
        </button>
      </hh-page-header>

      <p class="notice">
        {{
          "admin.governanceBoundary"
            | hhTranslate
              : "Approval and certification are enforced by Identity Service. MFA, maker-checker and separation-of-duties checks remain server-side."
        }}
      </p>
      <p class="error" *ngIf="error">{{ error }}</p>

      <app-access-governance-workflows
        [users]="users"
        [roles]="roles"
        [permissions]="permissions"
        [requests]="requests"
        [reviews]="reviews"
        [breakGlassRequests]="breakGlassRequests"
        [request]="request"
        [review]="review"
        [breakGlass]="breakGlass"
        [showJit]="showJit"
        [showBreakGlass]="showBreakGlass"
        [busy]="busy"
        [canWrite]="canWrite"
        [displayUser]="displayUser.bind(this)"
        (requestCreated)="createRequest()"
        (reviewCreated)="createReview()"
        (requestApproved)="approve($event)"
        (requestRejected)="reject($event)"
        (reviewCertified)="certify($event)"
        (reviewRevoked)="revoke($event)"
        (breakGlassCreated)="createBreakGlass()"
        (breakGlassApproved)="approveBreakGlass($event)"
        (breakGlassRevoked)="revokeBreakGlass($event)"
      />
    </hh-page-layout>
  `,
  styles: [
    ":host{display:block}.grid{display:grid;gap:var(--space-4);margin-top:var(--space-4)}.two-col{grid-template-columns:repeat(2,minmax(0,1fr))}.form-grid{display:grid;gap:var(--space-3)}.notice{padding:var(--space-3);border:1px solid var(--border-default);border-radius:var(--radius-card);background:var(--surface-muted);color:var(--text-secondary)}.error{color:var(--color-danger)}.table-wrap{overflow:auto}table{width:100%;border-collapse:collapse}th,td{text-align:left;padding:var(--space-2);border-bottom:1px solid var(--border-subtle);white-space:nowrap}@media(max-width:800px){.two-col{grid-template-columns:1fr}}",
  ],
})
export class AccessGovernancePageComponent implements OnInit {
  private readonly api = inject(AccessGovernanceApiService);
  private readonly route = inject(ActivatedRoute);
  private readonly permissionService = inject(HisHopePermissionService);
  private readonly i18n = inject(HisHopeI18nService);
  private readonly snack = inject(MatSnackBar);
  private readonly cdr = inject(ChangeDetectorRef);
  private readonly destroyRef = inject(DestroyRef);
  readonly resource = new HisHopeResourceState<{
    users: User[];
    roles: Role[];
    permissions: PermissionDefinition[];
    requests: AccessRequest[];
    reviews: AccessReview[];
    breakGlassRequests: BreakGlassRequest[];
  }>(this.destroyRef);
  users: User[] = [];
  roles: Role[] = [];
  requests: AccessRequest[] = [];
  reviews: AccessReview[] = [];
  breakGlassRequests: BreakGlassRequest[] = [];
  permissions: PermissionDefinition[] = [];
  busy = false;
  error = "";
  request = {
    subjectUserId: "",
    roleIds: [] as string[],
    reason: "",
    expiryHours: 8,
  };
  review = { subjectUserId: "", roleIds: [] as string[], dueDays: 30 };
  breakGlass = {
    subjectUserId: "",
    permissionCode: "",
    facilityId: "",
    reason: "",
    durationMinutes: 15,
  };
  currentView = "";
  @Input() initialView = "";

  get showJit(): boolean {
    return !this.currentView || this.currentView === "jit";
  }
  get showBreakGlass(): boolean {
    return !this.currentView || this.currentView === "break-glass";
  }

  get canWrite(): boolean {
    return this.permissionService.has("admin.roles.write");
  }
  ngOnInit(): void {
    const routeView = String(this.route.snapshot.data["governanceView"] ?? "");
    if (routeView) {
      this.currentView = routeView;
      this.reload();
      return;
    }
    this.route.queryParamMap.subscribe((params) => {
      this.currentView = this.initialView || (params.get("view") ?? "");
      this.cdr.markForCheck();
    });
    this.reload();
  }
  reload(): void {
    this.error = "";
    this.resource.load(
      forkJoin({
        users: this.api.getUsers(),
        roles: this.api.getRoles(),
        permissions: this.api.getPermissions(),
        requests: this.api.getAccessRequests(),
        reviews: this.api.getAccessReviews(),
        breakGlassRequests: this.api.getBreakGlassRequests(),
      }).pipe(
        tap((state) => {
          this.users = state.users;
          this.roles = state.roles;
          this.permissions = state.permissions;
          this.requests = state.requests;
          this.reviews = state.reviews;
          this.breakGlassRequests = state.breakGlassRequests;
          this.cdr.markForCheck();
        }),
        catchError(() => {
          this.error = this.i18n.t(
            "admin.loadAccessGovernanceFailed",
            "Unable to load access governance data.",
          );
          return of({
            users: [],
            roles: [],
            permissions: [],
            requests: [],
            reviews: [],
            breakGlassRequests: [],
          });
        }),
      ),
    );
  }
  displayUser(id: string): string {
    const user = this.users.find((item) => item.id === id);
    return user?.email || user?.userName || id;
  }
  createRequest(): void {
    this.execute(
      () => this.api.createAccessRequest(this.request),
      "admin.accessRequestCreated",
      "Access request created.",
      () =>
        (this.request = {
          subjectUserId: "",
          roleIds: [],
          reason: "",
          expiryHours: 8,
        }),
    );
  }
  createReview(): void {
    this.execute(
      () => this.api.createAccessReview(this.review),
      "admin.accessReviewCreated",
      "Access review created.",
      () => (this.review = { subjectUserId: "", roleIds: [], dueDays: 30 }),
    );
  }
  approve(item: AccessRequest): void {
    this.execute(
      () => this.api.approveAccessRequest(item.id),
      "admin.accessRequestApproved",
      "Access request approved.",
    );
  }
  reject(item: AccessRequest): void {
    this.execute(
      () => this.api.rejectAccessRequest(item.id),
      "admin.accessRequestRejected",
      "Access request rejected.",
    );
  }
  certify(item: AccessReview): void {
    this.execute(
      () => this.api.certifyAccessReview(item.id),
      "admin.accessReviewCertified",
      "Access review certified.",
    );
  }
  revoke(item: AccessReview): void {
    this.execute(
      () => this.api.revokeAccessReview(item.id),
      "admin.accessReviewRevoked",
      "Access review revoked.",
    );
  }
  createBreakGlass(): void {
    this.execute(
      () => this.api.createBreakGlassRequest(this.breakGlass),
      "admin.breakGlassCreated",
      "Break-glass request created.",
      () =>
        (this.breakGlass = {
          subjectUserId: "",
          permissionCode: "",
          facilityId: "",
          reason: "",
          durationMinutes: 15,
        }),
    );
  }
  approveBreakGlass(item: BreakGlassRequest): void {
    this.execute(
      () => this.api.approveBreakGlassRequest(item.id),
      "admin.breakGlassApproved",
      "Break-glass request approved.",
    );
  }
  revokeBreakGlass(item: BreakGlassRequest): void {
    this.execute(
      () => this.api.revokeBreakGlassRequest(item.id),
      "admin.breakGlassRevoked",
      "Break-glass request revoked.",
    );
  }
  private execute<T>(
    operation: () => import("rxjs").Observable<T>,
    key: string,
    fallback: string,
    after?: () => void,
  ): void {
    this.busy = true;
    this.error = "";
    operation().subscribe({
      next: () => {
        after?.();
        this.snack.open(
          this.i18n.t(key, fallback),
          this.i18n.t("admin.close", "Close"),
          { duration: 3000 },
        );
        this.reload();
      },
      error: () =>
        this.fail(
          "admin.governanceOperationFailed",
          "The server rejected this governance operation.",
        ),
    });
  }
  private fail(key: string, fallback: string): void {
    this.error = this.i18n.t(key, fallback);
    this.busy = false;
    this.cdr.markForCheck();
  }
}
