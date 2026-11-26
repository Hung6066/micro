import {
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component,
  OnInit,
  inject,
} from "@angular/core";
import { CommonModule } from "@angular/common";
import { HttpErrorResponse } from "@angular/common/http";
import { Observable } from "rxjs";
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
  AgentRootDevice,
  AgentRootDeviceChoice,
  AgentRootGrant,
  AgentRootGrantsApiService,
} from "../../core/services/agent-root-grants-api.service";

/** Grant durations offered in the form, in minutes (the server allows 30 min to 24 h). */
export const AGENT_ROOT_DURATIONS = [30, 60, 120, 240, 480, 720, 1440] as const;

export const REASON_MIN_LENGTH = 10;
export const REASON_MAX_LENGTH = 500;

/**
 * Time-limited root SSH for the infrastructure automation agent. A super admin requests a
 * grant; a different super admin approves it with a fresh MFA, and IdentityService has the
 * Vault SSH CA sign the key of every active device. Revoking stops distribution of the
 * certificates; disabling a device stops that device's fetches.
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
              | hhTranslate
                : "Agent root access is disabled on this IdentityService."
          }}
        </p>
      }
      @if (message) {
        <p class="error" role="alert">{{ message }}</p>
      }

      <p class="devices-note">
        @if (activeDevices.length) {
          {{
            "admin.agentRootChooseDevices"
              | hhTranslate
                : "Choose the devices that need root; an approval signs only those."
          }}
        } @else if (loaded) {
          {{
            "admin.agentRootNoDevices"
              | hhTranslate
                : "No active device yet: add one under Agent root devices before requesting access."
          }}
        }
      </p>

      <mat-card class="form-card">
        <mat-card-content>
          <form class="request" [formGroup]="form" (ngSubmit)="submit()">
            <mat-form-field appearance="outline">
              <mat-label>{{
                "admin.agentRootDuration" | hhTranslate: "Duration"
              }}</mat-label>
              <mat-select formControlName="durationMinutes">
                @for (minutes of durations; track minutes) {
                  <mat-option [value]="minutes">{{
                    durationLabel(minutes)
                  }}</mat-option>
                }
              </mat-select>
            </mat-form-field>
            <mat-form-field appearance="outline">
              <mat-label>{{
                "admin.agentRootDevicesColumn" | hhTranslate: "Devices"
              }}</mat-label>
              <mat-select formControlName="deviceIds" multiple>
                @for (device of activeDevices; track device.id) {
                  <mat-option [value]="device.id">{{ device.name }}</mat-option>
                }
              </mat-select>
            </mat-form-field>
            <mat-form-field appearance="outline" class="reason">
              <mat-label>{{
                "admin.reason" | hhTranslate: "Reason"
              }}</mat-label>
              <textarea
                matInput
                formControlName="reason"
                rows="3"
                [attr.maxlength]="reasonMaxLength"
              ></textarea>
              <mat-hint
                >{{ form.controls.reason.value.trim().length }} /
                {{ reasonMaxLength }}</mat-hint
              >
            </mat-form-field>
            <hh-action-button
              type="submit"
              kind="primary"
              icon="key"
              [disabled]="!canSubmit"
              [label]="
                'admin.agentRootRequest' | hhTranslate: 'Request root access'
              "
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
        [emptyMessage]="
          'admin.agentRootEmpty' | hhTranslate: 'No agent root grants yet.'
        "
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

      <h2 class="section-title">
        {{ "admin.agentRootDevices" | hhTranslate: "Agent root devices" }}
      </h2>
      <p class="devices-note">
        {{
          "admin.agentRootDevicesHelp"
            | hhTranslate
              : "Each machine has its own ed25519 key and its own client_credentials client (scope openship.agent.root.certificate) and fetches only its own certificate."
        }}
      </p>
      <mat-card class="form-card">
        <mat-card-content>
          <form
            class="request"
            [formGroup]="deviceForm"
            (ngSubmit)="addDevice()"
          >
            <mat-form-field appearance="outline">
              <mat-label>{{
                "admin.agentRootDeviceName" | hhTranslate: "Device name"
              }}</mat-label>
              <input matInput formControlName="name" maxlength="64" />
            </mat-form-field>
            <mat-form-field appearance="outline">
              <mat-label>{{
                "admin.clientId" | hhTranslate: "Client ID"
              }}</mat-label>
              <input matInput formControlName="clientId" maxlength="128" />
            </mat-form-field>
            <mat-form-field appearance="outline" class="reason">
              <mat-label>{{
                "admin.agentRootPublicKey" | hhTranslate: "Public key (.pub)"
              }}</mat-label>
              <textarea
                matInput
                formControlName="publicKey"
                rows="2"
                maxlength="1024"
              ></textarea>
            </mat-form-field>
            <hh-action-button
              type="submit"
              kind="primary"
              icon="add"
              [disabled]="!canAddDevice"
              [label]="'admin.agentRootAddDevice' | hhTranslate: 'Add device'"
            />
          </form>
        </mat-card-content>
      </mat-card>
      <hh-data-table
        [label]="'admin.agentRootDevices' | hhTranslate: 'Agent root devices'"
        [columns]="deviceColumns"
        [rows]="deviceRows"
        [loading]="loading"
        [empty]="!loading && !deviceRows.length"
        [emptyMessage]="
          'admin.agentRootNoDevicesTable' | hhTranslate: 'No devices yet.'
        "
      >
        <ng-template hhDataTableCell="actions" let-row>
          <div class="action-cell">
            <hh-action-button
              [kind]="row['isActive'] ? 'danger' : 'secondary'"
              mode="icon-only"
              [icon]="row['isActive'] ? 'block' : 'check_circle'"
              [label]="
                row['isActive']
                  ? ('admin.disable' | hhTranslate: 'Disable')
                  : ('admin.enable' | hhTranslate: 'Enable')
              "
              [disabled]="busy"
              (pressed)="toggleDevice(row)"
            />
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
      .section-title {
        margin: var(--space-lg) 0 var(--space-sm);
      }
      .devices-note {
        margin: var(--space-sm) 0;
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
    deviceIds: new FormControl<string[]>([], { nonNullable: true }),
    reason: new FormControl<string>("", { nonNullable: true }),
  });

  readonly deviceForm = new FormGroup({
    name: new FormControl<string>("", { nonNullable: true }),
    clientId: new FormControl<string>("", { nonNullable: true }),
    publicKey: new FormControl<string>("", { nonNullable: true }),
  });

  grants: AgentRootGrant[] = [];
  devices: AgentRootDevice[] = [];
  activeDevices: AgentRootDeviceChoice[] = [];
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
      this.form.controls.deviceIds.value.length > 0 &&
      reason.length >= REASON_MIN_LENGTH &&
      reason.length <= REASON_MAX_LENGTH
    );
  }

  get canAddDevice(): boolean {
    const value = this.deviceForm.getRawValue();
    return (
      !this.busy &&
      /^[A-Za-z0-9][A-Za-z0-9._-]{0,63}$/.test(value.name.trim()) &&
      value.clientId.trim().length > 0 &&
      value.publicKey.trim().startsWith("ssh-ed25519 ")
    );
  }

  get deviceRows(): Record<string, unknown>[] {
    return this.devices.map((device) => ({ ...device }));
  }

  get deviceColumns(): HisHopeDataTableColumn[] {
    this.i18n.locale();
    return [
      {
        key: "name",
        label: this.i18n.t("admin.agentRootDeviceName", "Device name"),
      },
      {
        key: "isActive",
        label: this.i18n.t("admin.status", "Status"),
        computed: (row) =>
          row["isActive"]
            ? this.i18n.t("admin.active", "active")
            : this.i18n.t("admin.disabled", "disabled"),
      },
      { key: "clientId", label: this.i18n.t("admin.clientId", "Client ID") },
      {
        key: "fingerprint",
        label: this.i18n.t("admin.fingerprint", "Fingerprint"),
      },
      {
        key: "lastFetchedAt",
        label: this.i18n.t("admin.agentRootLastFetch", "Last fetch"),
        format: "dateTime",
      },
      { key: "addedByEmail", label: this.i18n.t("admin.addedBy", "Added by") },
      {
        key: "addedAt",
        label: this.i18n.t("admin.added", "Added"),
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

  get rows(): Record<string, unknown>[] {
    return this.grants.map((grant) => toRow(grant));
  }

  get columns(): HisHopeDataTableColumn[] {
    this.i18n.locale();
    return [
      {
        key: "status",
        label: this.i18n.t("admin.status", "Status"),
        computed: (row) => this.statusLabel(row),
      },
      {
        key: "requestedByEmail",
        label: this.i18n.t("admin.requester", "Requester"),
      },
      {
        key: "approvedByEmail",
        label: this.i18n.t("admin.approver", "Approver"),
      },
      {
        key: "devices",
        label: this.i18n.t("admin.agentRootDevicesColumn", "Devices"),
      },
      {
        key: "requestedAt",
        label: this.i18n.t("admin.requested", "Requested"),
        format: "dateTime",
      },
      {
        key: "approvedAt",
        label: this.i18n.t("admin.approved", "Approved"),
        format: "dateTime",
      },
      {
        key: "validBefore",
        label: this.i18n.t("admin.validUntil", "Valid until"),
        format: "dateTime",
      },
      {
        key: "remainingMinutes",
        label: this.i18n.t("admin.remaining", "Remaining"),
        computed: (row) =>
          row["active"]
            ? this.durationLabel(Number(row["remainingMinutes"]))
            : "",
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
    this.loadDevices();
    this.loading = true;
    this.tableError = "";
    this.api.list().subscribe({
      next: (list) => {
        this.grants = list.items;
        this.activeDevices = list.activeDevices ?? [];
        // Drop choices that are no longer active; preselect the K3s manager on first load.
        const active = new Set(this.activeDevices.map((device) => device.id));
        const kept = this.form.controls.deviceIds.value.filter((id) =>
          active.has(id),
        );
        if (!kept.length) {
          const manager = this.activeDevices.find((device) =>
            device.name.startsWith("k3s-manager"),
          );
          if (manager) kept.push(manager.id);
        }
        this.form.controls.deviceIds.setValue(kept);
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

  loadDevices(): void {
    this.api.listDevices().subscribe({
      next: (devices) => {
        this.devices = devices;
        this.cdr.markForCheck();
      },
      error: (error: HttpErrorResponse) => {
        this.message = this.describe(error);
        this.cdr.markForCheck();
      },
    });
  }

  addDevice(): void {
    if (!this.canAddDevice) return;
    const value = this.deviceForm.getRawValue();
    this.busy = true;
    this.message = "";
    this.api
      .addDevice({
        name: value.name.trim(),
        clientId: value.clientId.trim(),
        publicKey: value.publicKey.trim(),
      })
      .subscribe({
        next: () => {
          this.busy = false;
          this.deviceForm.reset();
          this.load();
        },
        error: (error: HttpErrorResponse) => this.fail(error),
      });
  }

  toggleDevice(row: Record<string, unknown>): void {
    this.act(
      this.api.setDeviceActive(String(row["id"]), row["isActive"] !== true),
    );
  }

  submit(): void {
    if (!this.canSubmit) return;
    const value = this.form.getRawValue();
    this.busy = true;
    this.message = "";
    this.api
      .create({
        durationMinutes: value.durationMinutes,
        reason: value.reason.trim(),
        deviceIds: value.deviceIds,
      })
      .subscribe({
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
    if (minutes < 60)
      return this.i18n
        .t("admin.minutes", "{{n}} min")
        .replace("{{n}}", String(minutes));
    const hours = Math.floor(minutes / 60);
    const rest = minutes % 60;
    const label = this.i18n
      .t("admin.hours", "{{n}} h")
      .replace("{{n}}", String(hours));
    return rest
      ? `${label} ${this.i18n.t("admin.minutes", "{{n}} min").replace("{{n}}", String(rest))}`
      : label;
  }

  private act(request: Observable<unknown>): void {
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
    if (status === "issued" && !row["active"])
      return this.i18n.t("admin.agentRootStatusExpired", "expired");
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
        return this.i18n.t(
          "admin.agentRootDisabled",
          "Agent root access is disabled on this IdentityService.",
        );
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
        return this.i18n.t(
          "admin.agentRootNotPending",
          "This grant is no longer pending.",
        );
      case "agent_root_grant_closed":
        return this.i18n.t(
          "admin.agentRootClosed",
          "This grant is already closed.",
        );
      case "agent_root_no_devices":
        return this.i18n.t(
          "admin.agentRootNoDevices",
          "No active device yet: add one under Agent root devices before requesting access.",
        );
      case "agent_root_device_exists":
        return this.i18n.t(
          "admin.agentRootDeviceExists",
          "A device with this name already exists.",
        );
      case "agent_root_key_in_use":
        return this.i18n.t(
          "admin.agentRootKeyInUse",
          "This public key is already registered.",
        );
      case "agent_root_client_in_use":
        return this.i18n.t(
          "admin.agentRootClientInUse",
          "That client already belongs to an active device; create one client per device.",
        );
    }
    if (error.status === 400) {
      const fields = Object.values(
        (error.error as { errors?: Record<string, string[]> } | null)?.errors ??
          {},
      ).flat();
      if (fields.length) return fields.join(" ");
      return this.i18n.t(
        "admin.agentRootInvalid",
        "Choose a duration and give a reason of 10 to 500 characters.",
      );
    }
    if (error.status === 403)
      return this.i18n.t(
        "admin.agentRootForbidden",
        "Only super admins can manage agent root access.",
      );
    return this.i18n.t(
      "admin.agentRootFailed",
      "The agent root request failed.",
    );
  }
}

export function toRow(grant: AgentRootGrant): Record<string, unknown> {
  return {
    ...grant,
    approvedByEmail: grant.approvedByEmail ?? "",
    // Issued grants show who actually got a certificate; others show what was requested.
    devices: (grant.certificates?.length
      ? grant.certificates.map((item) => item.deviceName)
      : (grant.requestedDevices ?? [])
    ).join(", "),
  };
}
