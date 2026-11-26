import {
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component,
  OnInit,
  inject,
} from "@angular/core";
import { CommonModule } from "@angular/common";
import { HttpErrorResponse } from "@angular/common/http";
import { FormControl, FormGroup, ReactiveFormsModule } from "@angular/forms";
import { MatCardModule } from "@angular/material/card";
import { MatFormFieldModule } from "@angular/material/form-field";
import { MatInputModule } from "@angular/material/input";
import { MatSelectModule } from "@angular/material/select";
import {
  HisHopeActionButtonComponent,
  HisHopeDataTableCellDirective,
  HisHopeDataTableColumn,
  HisHopeDataTableComponent,
  HisHopePageHeaderComponent,
  HisHopePageLayoutComponent,
} from "@his-hope/frontend-foundation/ui";
import {
  HisHopeI18nService,
  HisHopeTranslatePipe,
} from "@his-hope/frontend-foundation/i18n";
import {
  AgentRootGrant,
  AgentRootGrantsApiService,
} from "../../core/services/agent-root-grants-api.service";

/** Grant durations offered in the form, in minutes (the server allows 30 min to 8 h). */
export const AGENT_ROOT_DURATIONS = [30, 60, 120, 240, 480] as const;

export const REASON_MIN_LENGTH = 10;
export const REASON_MAX_LENGTH = 500;

/**
 * Time-limited root SSH for the infrastructure automation agent. A super admin requests a
 * grant; a different super admin approves it with a fresh MFA, and IdentityService has the
 * Vault SSH CA sign the agent key. Revoking stops distribution of the certificate.
 */
@Component({
  selector: "app-agent-root-grants-page",
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    MatCardModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    HisHopeActionButtonComponent,
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
        [title]="'admin.agentRootAccess' | hhTranslate: 'Agent root access'"
        [subtitle]="
          'admin.agentRootSubtitle'
            | hhTranslate
              : 'Time-limited root SSH for the automation agent. Another super admin must approve with MFA.'
        "
      >
        <hh-action-button
          [disabled]="loading"
          (pressed)="load()"
          kind="secondary"
          icon="refresh"
          [label]="'admin.refresh' | hhTranslate: 'Refresh'"
        />
      </hh-page-header>

      @if (!enabled && loaded) {
        <p class="warning">
          {{
            "admin.agentRootDisabled"
              | hhTranslate: "Agent root access is disabled on this IdentityService."
          }}
        </p>
      }
      @if (message) {
        <p class="error" role="alert">{{ message }}</p>
      }

      <mat-card class="form-card">
        <mat-card-content>
          <form class="request" [formGroup]="form" (ngSubmit)="submit()">
            <mat-form-field appearance="outline">
              <mat-label>{{ "admin.agentRootDuration" | hhTranslate: "Duration" }}</mat-label>
              <mat-select formControlName="durationMinutes">
                @for (minutes of durations; track minutes) {
                  <mat-option [value]="minutes">{{ durationLabel(minutes) }}</mat-option>
                }
              </mat-select>
            </mat-form-field>
            <mat-form-field appearance="outline" class="reason">
              <mat-label>{{ "admin.reason" | hhTranslate: "Reason" }}</mat-label>
              <textarea
                matInput
                formControlName="reason"
                rows="3"
                [attr.maxlength]="reasonMaxLength"
              ></textarea>
              <mat-hint>{{ form.controls.reason.value.trim().length }} / {{ reasonMaxLength }}</mat-hint>
            </mat-form-field>
            <hh-action-button
              type="submit"
              kind="primary"
              icon="key"
              [disabled]="!canSubmit"
              [label]="'admin.agentRootRequest' | hhTranslate: 'Request root access'"
            />
          </form>
        </mat-card-content>
      </mat-card>

      <hh-data-table
        [label]="'admin.agentRootGrants' | hhTranslate: 'Agent root grants'"
        [columns]="columns"
        [rows]="rows"
        [loading]="loading"
        [error]="tableError"
        [empty]="!loading && !tableError && !rows.length"
        [emptyMessage]="'admin.agentRootEmpty' | hhTranslate: 'No agent root grants yet.'"
      >
        <ng-template hhDataTableCell="actions" let-row>
          <div class="action-cell">
            @if (canApprove(row)) {
              <hh-action-button
                kind="primary"
                mode="icon-only"
                icon="check"
                [label]="'admin.approve' | hhTranslate: 'Approve'"
                [disabled]="busy"
                (pressed)="approve(row)"
              />
            }
            @if (canRevoke(row)) {
              <hh-action-button
                kind="danger"
                mode="icon-only"
                icon="link_off"
                [label]="'admin.revoke' | hhTranslate: 'Revoke'"
                [disabled]="busy"
                (pressed)="revoke(row)"
              />
            }
          </div>
        </ng-template>
      </hh-data-table>
    </hh-page-layout>
  `,
  styles: [
    `
      .form-card {
        margin-bottom: var(--space-lg);
      }
      .request {
        display: flex;
        flex-wrap: wrap;
        gap: var(--space-md);
        align-items: flex-start;
      }
      .reason {
        flex: 1 1 24rem;
      }
      .action-cell {
        display: flex;
        gap: var(--space-sm);
      }
      .warning,
      .error {
        margin: var(--space-sm) 0;
      }
      .error {
        color: var(--color-danger);
      }
    `,
  ],
})
export class AgentRootGrantsPageComponent implements OnInit {
  private readonly api = inject(AgentRootGrantsApiService);
  private readonly i18n = inject(HisHopeI18nService);
  private readonly cdr = inject(ChangeDetectorRef);

  readonly durations = AGENT_ROOT_DURATIONS;
  readonly reasonMaxLength = REASON_MAX_LENGTH;
  readonly form = new FormGroup({
    durationMinutes: new FormControl<number>(60, { nonNullable: true }),
    reason: new FormControl<string>("", { nonNullable: true }),
  });

  grants: AgentRootGrant[] = [];
  enabled = true;
  loaded = false;
  loading = false;
  busy = false;
  message = "";
  tableError = "";

  get canSubmit(): boolean {
    const reason = this.form.controls.reason.value.trim();
    return (
      this.enabled &&
      !this.busy &&
      reason.length >= REASON_MIN_LENGTH &&
      reason.length <= REASON_MAX_LENGTH
    );
  }

  get rows(): Record<string, unknown>[] {
    return this.grants.map((grant) => toRow(grant));
  }

  get columns(): HisHopeDataTableColumn[] {
    this.i18n.locale();
    return [
      { key: "status", label: this.i18n.t("admin.status", "Status"), computed: (row) => this.statusLabel(row) },
      { key: "requestedByEmail", label: this.i18n.t("admin.requester", "Requester") },
      { key: "approvedByEmail", label: this.i18n.t("admin.approver", "Approver") },
      { key: "requestedAt", label: this.i18n.t("admin.requested", "Requested"), format: "dateTime" },
      { key: "approvedAt", label: this.i18n.t("admin.approved", "Approved"), format: "dateTime" },
      { key: "validBefore", label: this.i18n.t("admin.validUntil", "Valid until"), format: "dateTime" },
      {
        key: "remainingMinutes",
        label: this.i18n.t("admin.remaining", "Remaining"),
        computed: (row) => (row["active"] ? this.durationLabel(Number(row["remainingMinutes"])) : ""),
      },
      { key: "reason", label: this.i18n.t("admin.reason", "Reason") },
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
    this.loading = true;
    this.tableError = "";
    this.api.list().subscribe({
      next: (list) => {
        this.grants = list.items;
        this.enabled = list.enabled;
        this.loaded = true;
        this.loading = false;
        this.cdr.markForCheck();
      },
      error: (error: HttpErrorResponse) => {
        this.loading = false;
        this.loaded = true;
        this.tableError = this.describe(error);
        this.cdr.markForCheck();
      },
    });
  }

  submit(): void {
    if (!this.canSubmit) return;
    const value = this.form.getRawValue();
    this.busy = true;
    this.message = "";
    this.api.create({ durationMinutes: value.durationMinutes, reason: value.reason.trim() }).subscribe({
      next: () => {
        this.busy = false;
        this.form.controls.reason.setValue("");
        this.load();
      },
      error: (error: HttpErrorResponse) => this.fail(error),
    });
  }

  canApprove(row: Record<string, unknown>): boolean {
    return row["status"] === "pending" && row["requestedByMe"] !== true;
  }

  canRevoke(row: Record<string, unknown>): boolean {
    return row["status"] === "pending" || row["status"] === "issued";
  }

  approve(row: Record<string, unknown>): void {
    if (!this.canApprove(row)) return;
    this.act(this.api.approve(String(row["id"])));
  }

  revoke(row: Record<string, unknown>): void {
    if (!this.canRevoke(row)) return;
    this.act(this.api.revoke(String(row["id"])));
  }

  durationLabel(minutes: number): string {
    if (minutes < 60) return this.i18n.t("admin.minutes", "{{n}} min").replace("{{n}}", String(minutes));
    const hours = Math.floor(minutes / 60);
    const rest = minutes % 60;
    const label = this.i18n.t("admin.hours", "{{n}} h").replace("{{n}}", String(hours));
    return rest ? `${label} ${this.i18n.t("admin.minutes", "{{n}} min").replace("{{n}}", String(rest))}` : label;
  }

  private act(request: ReturnType<AgentRootGrantsApiService["approve"]>): void {
    this.busy = true;
    this.message = "";
    request.subscribe({
      next: () => {
        this.busy = false;
        this.load();
      },
      error: (error: HttpErrorResponse) => this.fail(error),
    });
  }

  private fail(error: HttpErrorResponse): void {
    this.busy = false;
    this.message = this.describe(error);
    this.cdr.markForCheck();
    // A failed or expired approval changes the grant status; show it.
    if (error.status === 409 || error.status === 502) this.load();
  }

  private statusLabel(row: Record<string, unknown>): string {
    const status = String(row["status"] ?? "");
    if (status === "issued" && !row["active"]) return this.i18n.t("admin.agentRootStatusExpired", "expired");
    return this.i18n.t(`admin.agentRootStatus.${status}`, status);
  }

  describe(error: HttpErrorResponse): string {
    const code = (error.error as { errorCode?: string } | null)?.errorCode;
    switch (code) {
      case "step_up_required":
        return this.i18n.t(
          "admin.agentRootStepUp",
          "This needs a fresh MFA sign-in. Sign out, sign in again with MFA, then retry within 15 minutes.",
        );
      case "agent_root_self_approval":
        return this.i18n.t(
          "admin.agentRootSelfApproval",
          "You requested this grant; another super admin must approve it.",
        );
      case "agent_root_disabled":
        return this.i18n.t("admin.agentRootDisabled", "Agent root access is disabled on this IdentityService.");
      case "agent_root_sign_failed":
        return this.i18n.t(
          "admin.agentRootSignFailed",
          "Vault could not sign the agent key. The grant is marked failed; request a new one.",
        );
      case "agent_root_request_expired":
        return this.i18n.t(
          "admin.agentRootRequestExpired",
          "The request was not approved in time and has expired. Request a new grant.",
        );
      case "agent_root_not_pending":
        return this.i18n.t("admin.agentRootNotPending", "This grant is no longer pending.");
      case "agent_root_grant_closed":
        return this.i18n.t("admin.agentRootClosed", "This grant is already closed.");
    }
    if (error.status === 400)
      return this.i18n.t(
        "admin.agentRootInvalid",
        "Choose a duration and give a reason of 10 to 500 characters.",
      );
    if (error.status === 403)
      return this.i18n.t("admin.agentRootForbidden", "Only super admins can manage agent root access.");
    return this.i18n.t("admin.agentRootFailed", "The agent root request failed.");
  }
}

export function toRow(grant: AgentRootGrant): Record<string, unknown> {
  return {
    ...grant,
    approvedByEmail: grant.approvedByEmail ?? "",
  };
}
