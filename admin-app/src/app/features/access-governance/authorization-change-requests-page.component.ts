import {
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component,
  DestroyRef,
  OnInit,
  inject,
} from "@angular/core";
import { CommonModule } from "@angular/common";
import {
  HisHopeDataTableCellDirective,
  HisHopeDataTableColumn,
  HisHopeDataTableComponent,
  HisHopePageHeaderComponent,
  HisHopePageLayoutComponent,
  HisHopeToastService,
  HisHopeActionButtonComponent,
  HisHopeAlertComponent,
} from "@his-hope/frontend-foundation/ui";
import { HisHopePermissionService } from "@his-hope/frontend-foundation/auth";
import {
  HisHopeI18nService,
  HisHopeTranslatePipe,
} from "@his-hope/frontend-foundation/i18n";
import { takeUntilDestroyed } from "@angular/core/rxjs-interop";
import { catchError, of, tap } from "rxjs";
import { AuthorizationChangeRequest } from "../../core/contracts/admin.contracts";
import { AccessGovernanceApiService } from "../../core/services/access-governance-api.service";
import { AdminResourceStateController } from "../../core/services/admin-resource-state.controller";
import { TenantContextService } from "../../core/services/tenant-context.service";

@Component({
  selector: "app-authorization-change-requests-page",
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    CommonModule,
    HisHopeActionButtonComponent,
    HisHopeAlertComponent,
    HisHopeDataTableCellDirective,
    HisHopeDataTableComponent,
    HisHopePageHeaderComponent,
    HisHopePageLayoutComponent,
    HisHopeTranslatePipe,
  ],
  template: `
    <hh-page-layout>
      <hh-page-header
        hhPageHeader
        [title]="'admin.authorizationChangeRequests' | hhTranslate: 'Authorization change requests'"
        [subtitle]="'admin.authorizationChangeRequestsSubtitle' | hhTranslate: 'Four-eyes approval queue for high-risk authorization changes.'"
      >
        <hh-action-button
          kind="secondary"
          icon="refresh"
          [disabled]="busy"
          [label]="'admin.refresh' | hhTranslate: 'Refresh'"
          (pressed)="load()"
        />
      </hh-page-header>
      <hh-alert
        tone="info"
        icon="policy"
        [title]="'admin.authorizationChangeGovernance' | hhTranslate: 'Approval and execution are separate actions. The requester cannot approve their own change.'"
      />
      @if (error) {
        <p class="error">{{ error }}</p>
      }
      <hh-data-table
        [label]="'admin.authorizationChangeRequests' | hhTranslate: 'Authorization change requests'"
        [columns]="columns"
        [rows]="rows"
        [loading]="state.loading"
        [empty]="!state.loading && !error && !rows.length"
      >
        <ng-template hhDataTableCell="actions" let-row>
          @if (row['status'] === 'pending') {
            <div class="action-cell">
            <hh-action-button
              kind="primary"
              mode="icon-only"
              icon="check"
              [disabled]="busy || !canWrite"
              [label]="'admin.approve' | hhTranslate: 'Approve'"
              (pressed)="approveByRow(row)"
            />
            <hh-action-button
              kind="danger"
              mode="icon-only"
              icon="close"
              [disabled]="busy || !canWrite"
              [label]="'admin.reject' | hhTranslate: 'Reject'"
              (pressed)="rejectByRow(row)"
            />
            </div>
          }
        </ng-template>
      </hh-data-table>
    </hh-page-layout>
  `,
  styles: [
    ":host{display:block}.error{color:var(--color-danger)}.action-cell{display:flex;gap:var(--space-sm)}",
  ],
})
export class AuthorizationChangeRequestsPageComponent implements OnInit {
  private readonly api = inject(AccessGovernanceApiService);
  private readonly permissions = inject(HisHopePermissionService);
  private readonly i18n = inject(HisHopeI18nService);
  private readonly toast = inject(HisHopeToastService);
  private readonly cdr = inject(ChangeDetectorRef);
  private readonly tenantContext = inject(TenantContextService);
  private readonly destroyRef = inject(DestroyRef);
  readonly state = new AdminResourceStateController<AuthorizationChangeRequest[]>({
    destroyRef: this.destroyRef,
    i18n: this.i18n,
    loadErrorMessageKey: "admin.authorizationChangeRequestsLoadFailed",
    loadErrorFallback: "Unable to load authorization change requests.",
  });
  requests: AuthorizationChangeRequest[] = [];
  busy = false;

  get canWrite(): boolean {
    return this.permissions.has("admin.roles.write");
  }
  get error(): string {
    return this.state.error;
  }
  get rows(): Record<string, unknown>[] {
    return this.requests.map((item) => ({ ...item }));
  }
  get columns(): HisHopeDataTableColumn[] {
    this.i18n.locale();
    return [
      { key: "resourceType", label: this.i18n.t("admin.resourceType", "Resource") },
      { key: "action", label: this.i18n.t("admin.action", "Action") },
      { key: "requestedBy", label: this.i18n.t("admin.requestedBy", "Requested by") },
      {
        key: "status",
        label: this.i18n.t("admin.status", "Status"),
        computed: (row) => this.statusLabel(String(row["status"] ?? "")),
      },
      { key: "requestedAt", label: this.i18n.t("admin.requestedAt", "Requested"), format: "dateTime" },
      { key: "expiresAt", label: this.i18n.t("admin.expires", "Expires"), format: "dateTime" },
      { key: "actions", label: this.i18n.t("admin.actions", "Actions"), sortable: false, hideable: false },
    ];
  }
  ngOnInit(): void {
    this.load();
    this.tenantContext.bindTenantReload(this.destroyRef, () => this.load());
  }
  load(): void {
    this.state.load(
      this.api.getAuthorizationChangeRequests().pipe(
        takeUntilDestroyed(this.destroyRef),
        tap((items) => {
          this.requests = items;
          this.cdr.markForCheck();
        }),
        catchError(() => {
          this.state.setActionError(this.i18n.t("admin.authorizationChangeRequestsLoadFailed", "Unable to load authorization change requests."));
          return of([] as AuthorizationChangeRequest[]);
        }),
      ),
    );
  }
  approveByRow(row: Record<string, unknown>): void {
    this.mutate(String(row["id"] ?? ""), "approve");
  }
  rejectByRow(row: Record<string, unknown>): void {
    this.mutate(String(row["id"] ?? ""), "reject");
  }
  private statusLabel(status: string): string {
    return this.i18n.t(`admin.authorizationChangeStatus.${status}`, status || "-");
  }
  private mutate(id: string, action: "approve" | "reject"): void {
    if (!id || !this.canWrite) return;
    this.busy = true;
    const operation = action === "approve"
      ? this.api.approveAuthorizationChangeRequest(id)
      : this.api.rejectAuthorizationChangeRequest(id);
    operation.pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: () => {
        this.busy = false;
        this.toast.success(this.i18n.t("admin.authorizationChangeUpdated", "Authorization change request updated."), { duration: 3000 });
        this.load();
        this.cdr.markForCheck();
      },
      error: () => {
        this.busy = false;
        this.state.setActionError(this.i18n.t("admin.authorizationChangeMutationFailed", "Authorization change request mutation failed."));
        this.cdr.markForCheck();
      },
    });
  }
}
