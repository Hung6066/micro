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
import { MatIconModule } from "@angular/material/icon";
import { MatSelectModule } from "@angular/material/select";
import { MatSnackBar, MatSnackBarModule } from "@angular/material/snack-bar";
import { MatTabsModule } from "@angular/material/tabs";
import { catchError, firstValueFrom, of, tap } from "rxjs";
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
import {
  AuditLogRow,
  DeliveryHealth,
  DevicePostureAssessment,
  DevicePostureEvaluation,
  DevicePosturePolicy,
  IdentitySetting,
  MtlsBinding,
  ProvisioningJob,
  ProvisioningReadiness,
  RadiusEapTlsStatus,
  SecuritySignalOutboxEntry,
  SecuritySignalStatus,
} from "../../core/contracts/admin.contracts";
import { IdentityCapabilitiesApiService } from "../../core/services/identity-capabilities-api.service";
import {
  IdentityCapabilitiesService,
  IdentityCapabilityState,
} from "../../core/services/identity-capabilities.service";
import { ApiErrorMessageService } from "../../core/services/api-error-message.service";

import { HisHopeActionButtonComponent } from "@his-hope/frontend-foundation/ui";
@Component({
  selector: "app-identity-capabilities-page",
  standalone: true,
  imports: [
    HisHopeActionButtonComponent,
    CommonModule,
    FormsModule,
    MatButtonModule,
    MatCardModule,
    MatFormFieldModule,
    MatInputModule,
    MatIconModule,
    MatSelectModule,
    MatSnackBarModule,
    MatTabsModule,
    HisHopePageHeaderComponent,
    HisHopePageLayoutComponent,
    HisHopeTranslatePipe,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <hh-page-layout>
      <hh-page-header
        hhPageHeader
        [title]="
          'admin.identityCapabilities' | hhTranslate: 'Identity capabilities'
        "
        [subtitle]="
          'admin.identityCapabilitiesSubtitle'
            | hhTranslate
              : 'P0/P1 controls and P2 device posture pilot workspace.'
        "
      >
        <hh-action-button
          [disabled]="loading"
          (pressed)="reload()"
          kind="secondary"
          icon="refresh"
          [label]="'admin.refresh' | hhTranslate: 'Refresh'"
        />
      </hh-page-header>
      <mat-form-field appearance="outline" *ngIf="facilityIds.length > 1"
        ><mat-label>{{ "admin.facility" | hhTranslate: "Facility" }}</mat-label
        ><mat-select
          [(ngModel)]="selectedFacilityId"
          (selectionChange)="reload()"
          ><mat-option
            *ngFor="let facilityId of facilityIds"
            [value]="facilityId"
            >{{ facilityId }}</mat-option
          ></mat-select
        ></mat-form-field
      >
      <p class="muted" *ngIf="loading">
        {{
          "admin.loadingCapabilities"
            | hhTranslate: "Loading capability status…"
        }}
      </p>
      <div class="notice">
        <mat-icon>shield</mat-icon
        ><span>{{
          "admin.identityCapabilitiesNotice"
            | hhTranslate
              : "Secrets, certificates and vendor credentials remain server-side. P2 is observe-first."
        }}</span>
      </div>
      <mat-tab-group>
        <mat-tab
          [label]="'admin.p0P1Readiness' | hhTranslate: 'P0/P1 readiness'"
        >
          <section class="grid two-col">
            <mat-card
              ><mat-card-header
                ><mat-card-title>{{
                  "admin.securityControls" | hhTranslate: "Security controls"
                }}</mat-card-title
                ><mat-card-subtitle>{{
                  "admin.runtimeContractState"
                    | hhTranslate: "Read-only runtime contract state"
                }}</mat-card-subtitle></mat-card-header
              ><mat-card-content>
                <div class="status-row" *ngFor="let item of p0Settings">
                  <span>{{ item.key }}</span
                  ><strong [class.good]="isEnabled(item.value)">{{
                    item.value
                  }}</strong>
                </div>
              </mat-card-content></mat-card
            >
            <mat-card
              ><mat-card-header
                ><mat-card-title>{{
                  "admin.provisioning" | hhTranslate: "Provisioning"
                }}</mat-card-title
                ><mat-card-subtitle>{{
                  "admin.dryRunByDefault" | hhTranslate: "Dry-run by default"
                }}</mat-card-subtitle></mat-card-header
              ><mat-card-content>
                <div class="status-row">
                  <span>{{ "admin.mode" | hhTranslate: "Mode" }}</span
                  ><strong>{{ provisioningMode }}</strong>
                </div>
                <div
                  class="status-row"
                  *ngFor="let target of provisioningReadiness?.targets"
                >
                  <span>{{ target.target }}</span
                  ><strong
                    [class.good]="target.status === 'ready_for_dry_run'"
                    >{{ target.status }}</strong
                  >
                </div>
                <mat-form-field appearance="outline"
                  ><mat-label>{{
                    "admin.target" | hhTranslate: "Target"
                  }}</mat-label
                  ><mat-select [(ngModel)]="provisioning.target"
                    ><mat-option value="scim">{{
                      "admin.provisioningScim" | hhTranslate: "SCIM"
                    }}</mat-option
                    ><mat-option value="entra">{{
                      "admin.provisioningEntra"
                        | hhTranslate: "Microsoft Entra ID"
                    }}</mat-option
                    ><mat-option value="google-workspace">{{
                      "admin.provisioningGoogleWorkspace"
                        | hhTranslate: "Google Workspace"
                    }}</mat-option></mat-select
                  ></mat-form-field
                >
                <mat-form-field appearance="outline"
                  ><mat-label>{{
                    "admin.resourceId" | hhTranslate: "Resource ID"
                  }}</mat-label
                  ><input
                    matInput
                    [(ngModel)]="provisioning.resourceId"
                    autocomplete="off"
                /></mat-form-field>
                <hh-action-button
                  [disabled]="busy || !provisioning.resourceId"
                  (pressed)="queueProvisioning()"
                  kind="primary"
                  icon="queue"
                  [label]="
                    'admin.queueDryRun'
                      | hhTranslate: 'Queue dry-run reconciliation'
                  "
                />
                <p class="muted" *ngIf="lastJob">
                  {{ "admin.job" | hhTranslate: "Job" }} {{ lastJob.id }} ·
                  {{ lastJob.target }} · {{ lastJob.operation }}
                </p>
              </mat-card-content></mat-card
            >
            <mat-card
              ><mat-card-header
                ><mat-card-title>{{
                  "admin.deliveryHealth"
                    | hhTranslate: "Integration delivery health"
                }}</mat-card-title
                ><mat-card-subtitle>{{
                  "admin.deliveryHealthSubtitle"
                    | hhTranslate
                      : "Normalized outbox and reconciliation evidence"
                }}</mat-card-subtitle></mat-card-header
              ><mat-card-content>
                <div
                  class="status-row"
                  *ngFor="let delivery of deliveryHealth?.deliveries"
                >
                  <span>{{ delivery.channel }} · {{ delivery.target }}</span
                  ><strong
                    [class.good]="delivery.status === 'healthy'"
                    [class.warn]="delivery.status === 'pending'"
                    [class.bad]="delivery.status === 'failed'"
                    >{{ delivery.status }} ({{ delivery.pending }}/{{
                      delivery.failed
                    }})</strong
                  >
                </div>
                <p class="muted" *ngIf="!deliveryHealth">
                  {{
                    "admin.deliveryHealthUnavailable"
                      | hhTranslate: "Delivery health unavailable."
                  }}
                </p>
              </mat-card-content></mat-card
            >
          </section>
          <mat-card
            ><mat-card-header
              ><mat-card-title>{{
                "admin.recentAuditEvents" | hhTranslate: "Recent audit events"
              }}</mat-card-title></mat-card-header
            ><mat-card-content class="table-wrap"
              ><table>
                <thead>
                  <tr>
                    <th>{{ "admin.timestamp" | hhTranslate: "Time" }}</th>
                    <th>{{ "admin.user" | hhTranslate: "User" }}</th>
                    <th>{{ "admin.action" | hhTranslate: "Action" }}</th>
                    <th>{{ "admin.resource" | hhTranslate: "Resource" }}</th>
                    <th>{{ "admin.outcome" | hhTranslate: "Outcome" }}</th>
                    <th>
                      {{ "admin.correlation" | hhTranslate: "Correlation" }}
                    </th>
                  </tr>
                </thead>
                <tbody>
                  <tr *ngFor="let row of auditRows">
                    <td>{{ row.timestamp | date: "short" }}</td>
                    <td>{{ row.userName || row.userId }}</td>
                    <td>{{ row.action }}</td>
                    <td>{{ row.resourceType }} {{ row.resourceId }}</td>
                    <td>{{ row.outcome || "—" }}</td>
                    <td class="mono">{{ row.correlationId || "—" }}</td>
                  </tr>
                </tbody>
              </table>
              <p class="muted" *ngIf="!auditRows.length">
                {{
                  "admin.noAuditEvents"
                    | hhTranslate: "No audit events returned."
                }}
              </p></mat-card-content
            ></mat-card
          >
        </mat-tab>
        <mat-tab
          [label]="'admin.interoperability' | hhTranslate: 'Interoperability'"
        >
          <section class="grid two-col">
            <mat-card
              ><mat-card-header
                ><mat-card-title>{{
                  "admin.provisioningJobs" | hhTranslate: "Provisioning jobs"
                }}</mat-card-title
                ><mat-card-subtitle>{{
                  "admin.serverManagedOnly"
                    | hhTranslate
                      : "Server-managed state; payloads are not rendered"
                }}</mat-card-subtitle></mat-card-header
              ><mat-card-content class="table-wrap"
                ><table>
                  <thead>
                    <tr>
                      <th>{{ "admin.target" | hhTranslate: "Target" }}</th>
                      <th>
                        {{ "admin.operation" | hhTranslate: "Operation" }}
                      </th>
                      <th>{{ "admin.resource" | hhTranslate: "Resource" }}</th>
                      <th>{{ "admin.attempts" | hhTranslate: "Attempts" }}</th>
                      <th>{{ "admin.status" | hhTranslate: "Status" }}</th>
                      <th></th>
                    </tr>
                  </thead>
                  <tbody>
                    <tr *ngFor="let job of provisioningJobs">
                      <td>{{ job.target }}</td>
                      <td>{{ job.operation }}</td>
                      <td>{{ job.resourceType }} {{ job.resourceId }}</td>
                      <td>{{ job.attempts }}</td>
                      <td>
                        {{
                          job.status ||
                            (job.completedAt
                              ? ("admin.completed" | hhTranslate: "completed")
                              : job.lastError ||
                                ("admin.queued" | hhTranslate: "queued"))
                        }}
                      </td>
                      <td>
                        <hh-action-button
                          kind="secondary"
                          mode="icon-only"
                          icon="refresh"
                          [label]="'admin.retry' | hhTranslate: 'Retry'"
                          type="button"
                          *ngIf="
                            can('admin.provisioning.manage') &&
                            (job.status === 'failed' ||
                              (job.lastError &&
                                !job.completedAt &&
                                job.status !== 'dry-run'))
                          "
                          (pressed)="retryJob(job)"
                          [disabled]="busy"
                        />
                      </td>
                    </tr>
                  </tbody>
                </table>
                <p class="muted" *ngIf="!provisioningJobs.length">
                  {{
                    "admin.noProvisioningJobs"
                      | hhTranslate: "No provisioning jobs returned."
                  }}
                </p></mat-card-content
              ></mat-card
            >
            <mat-card
              ><mat-card-header
                ><mat-card-title>{{
                  "admin.radiusEapTls" | hhTranslate: "RADIUS EAP-TLS"
                }}</mat-card-title
                ><mat-card-subtitle>{{
                  "admin.outpostOwnsSecret"
                    | hhTranslate: "The RADIUS outpost owns the shared secret."
                }}</mat-card-subtitle></mat-card-header
              ><mat-card-content *ngIf="radiusStatus" class="form-grid"
                ><div class="status-row">
                  <span>{{ "admin.enabled" | hhTranslate: "Enabled" }}</span
                  ><strong [class.good]="radiusStatus.enabled">{{
                    radiusStatus.enabled
                      ? ("admin.yes" | hhTranslate: "yes")
                      : ("admin.no" | hhTranslate: "no")
                  }}</strong>
                </div>
                <div class="status-row">
                  <span>{{
                    "admin.trustedCa" | hhTranslate: "Trusted CA"
                  }}</span
                  ><strong>{{
                    radiusStatus.trustedCaReachable
                      ? ("admin.reachable" | hhTranslate: "reachable")
                      : radiusStatus.trustedCaConfigured
                        ? ("admin.unreachable" | hhTranslate: "unreachable")
                        : ("admin.notConfigured"
                          | hhTranslate: "not configured")
                  }}</strong>
                </div></mat-card-content
              ></mat-card
            >
          </section>
          <mat-card
            ><mat-card-header
              ><mat-card-title>{{
                "admin.mtlsBindings" | hhTranslate: "mTLS certificate bindings"
              }}</mat-card-title
              ><mat-card-subtitle>{{
                "admin.thumbprintsOnly"
                  | hhTranslate
                    : "Normalized thumbprints and certificate metadata only"
              }}</mat-card-subtitle></mat-card-header
            ><mat-card-content class="table-wrap"
              ><table>
                <thead>
                  <tr>
                    <th>{{ "admin.subject" | hhTranslate: "Subject" }}</th>
                    <th>
                      {{ "admin.thumbprint" | hhTranslate: "Thumbprint" }}
                    </th>
                    <th>{{ "admin.subject" | hhTranslate: "Subject" }}</th>
                    <th>{{ "admin.expires" | hhTranslate: "Expires" }}</th>
                    <th>{{ "admin.status" | hhTranslate: "Status" }}</th>
                    <th></th>
                  </tr>
                </thead>
                <tbody>
                  <tr *ngFor="let binding of mtlsBindings">
                    <td>{{ binding.subject || binding.userId }}</td>
                    <td class="mono">{{ binding.thumbprint }}</td>
                    <td>{{ binding.subject || "—" }}</td>
                    <td>{{ binding.notAfter | date: "short" }}</td>
                    <td>{{ binding.status }}</td>
                    <td>
                      <hh-action-button
                        kind="danger"
                        mode="icon-only"
                        icon="delete"
                        [label]="'admin.revoke' | hhTranslate: 'Revoke'"
                        color="warn"
                        type="button"
                        (pressed)="revokeBinding(binding)"
                        [disabled]="
                          binding.status === 'revoked' ||
                          busy ||
                          !can('admin.clients.write')
                        "
                      />
                    </td>
                  </tr>
                </tbody>
              </table>
              <p class="muted" *ngIf="!mtlsBindings.length">
                {{
                  "admin.noMtlsBindings"
                    | hhTranslate: "No certificate bindings returned."
                }}
              </p></mat-card-content
            ></mat-card
          >
        </mat-tab>
        <mat-tab
          [label]="
            'admin.federationCompliance'
              | hhTranslate: 'Federation & compliance'
          "
        >
          <section class="grid two-col">
            <mat-card
              ><mat-card-header
                ><mat-card-title>{{
                  "admin.externalFederation"
                    | hhTranslate: "External federation"
                }}</mat-card-title
                ><mat-card-subtitle>{{
                  "admin.federationServerManaged"
                    | hhTranslate
                      : "Issuer and account-link state are server managed."
                }}</mat-card-subtitle></mat-card-header
              ><mat-card-content
                ><div
                  class="status-row"
                  *ngFor="let item of federationSettings"
                >
                  <span>{{ item.key }}</span
                  ><strong [class.good]="isEnabled(item.value)">{{
                    item.value
                  }}</strong>
                </div>
                <p class="muted">
                  {{
                    "admin.federationNoSecrets"
                      | hhTranslate
                        : "No provider tokens or assertions are rendered in this workspace."
                  }}
                </p></mat-card-content
              ></mat-card
            >
            <mat-card
              ><mat-card-header
                ><mat-card-title>{{
                  "admin.scimSsf" | hhTranslate: "SCIM & SSF"
                }}</mat-card-title
                ><mat-card-subtitle>{{
                  "admin.adapterHealthOnly"
                    | hhTranslate
                      : "Adapter status only; credentials and signing keys remain server-side."
                }}</mat-card-subtitle></mat-card-header
              ><mat-card-content
                ><div class="status-row" *ngFor="let item of scimSsfSettings">
                  <span>{{ item.key }}</span
                  ><strong [class.good]="isEnabled(item.value)">{{
                    item.value
                  }}</strong>
                </div>
                <div *ngIf="ssfStatus" class="form-grid">
                  <div class="status-row">
                    <span>{{
                      "admin.ssfSubscriptions"
                        | hhTranslate: "SSF subscriptions"
                    }}</span
                    ><strong>{{ ssfStatus.subscriptionCount }}</strong>
                  </div>
                  <div class="status-row">
                    <span>{{
                      "admin.ssfOutbox" | hhTranslate: "Outbox pending/failed"
                    }}</span
                    ><strong
                      >{{ ssfStatus.pending }}/{{ ssfStatus.failed }}</strong
                    >
                  </div>
                </div>
                <p class="muted">
                  {{
                    "admin.ssfOutboxAudit"
                      | hhTranslate
                        : "SSF delivery is outbox-backed and audited; live receiver health requires an external receiver."
                  }}
                </p>
                <div class="table-wrap" *ngIf="ssfOutbox.length">
                  <table>
                    <thead>
                      <tr>
                        <th>{{ "admin.eventType" | hhTranslate: "Event" }}</th>
                        <th>
                          {{ "admin.attempts" | hhTranslate: "Attempts" }}
                        </th>
                        <th>
                          {{ "admin.availableAt" | hhTranslate: "Available" }}
                        </th>
                        <th>{{ "admin.error" | hhTranslate: "Error" }}</th>
                        <th></th>
                      </tr>
                    </thead>
                    <tbody>
                      <tr *ngFor="let entry of ssfOutbox">
                        <td>{{ entry.eventType }}</td>
                        <td>{{ entry.attempts }}</td>
                        <td>{{ entry.availableAt | date: "short" }}</td>
                        <td>{{ entry.lastError || "—" }}</td>
                        <td>
                          <hh-action-button
                            kind="secondary"
                            mode="icon-only"
                            icon="refresh"
                            [label]="'admin.retry' | hhTranslate: 'Retry'"
                            type="button"
                            *ngIf="can('admin.settings.write')"
                            (pressed)="retrySsf(entry)"
                            [disabled]="busy"
                          />
                        </td>
                      </tr>
                    </tbody>
                  </table></div></mat-card-content
            ></mat-card>
          </section>
          <mat-card
            ><mat-card-header
              ><mat-card-title>{{
                "admin.complianceReports" | hhTranslate: "Compliance reports"
              }}</mat-card-title
              ><mat-card-subtitle>{{
                "admin.shortLivedExport"
                  | hhTranslate
                    : "Server-generated, audited, short-lived download"
              }}</mat-card-subtitle></mat-card-header
            ><mat-card-content
              ><hh-action-button
                [disabled]="busy"
                (pressed)="downloadAuditCsv()"
                kind="primary"
                icon="download"
                [label]="
                  'admin.exportAuditCsv' | hhTranslate: 'Export audit CSV'
                " /></mat-card-content
          ></mat-card>
        </mat-tab>
        <mat-tab
          [label]="
            'admin.p2DevicePosturePilot'
              | hhTranslate: 'P2 device posture pilot'
          "
        >
          <section class="grid two-col" *ngIf="policy">
            <mat-card
              ><mat-card-header
                ><mat-card-title>{{
                  "admin.policy" | hhTranslate: "Policy"
                }}</mat-card-title
                ><mat-card-subtitle
                  >{{ "admin.version" | hhTranslate: "Version" }}
                  {{ policy.version }} ·
                  {{ "admin.updated" | hhTranslate: "updated" }}
                  {{ policy.updatedAt | date: "short" }}</mat-card-subtitle
                ></mat-card-header
              ><mat-card-content class="form-grid">
                <mat-form-field appearance="outline"
                  ><mat-label>{{
                    "admin.mode" | hhTranslate: "Mode"
                  }}</mat-label
                  ><mat-select [(ngModel)]="policy.mode"
                    ><mat-option value="observe">{{
                      "admin.safeDefault"
                        | hhTranslate: "Observe (safe default)"
                    }}</mat-option
                    ><mat-option value="stepup">{{
                      "admin.stepUpPreview" | hhTranslate: "Step-up preview"
                    }}</mat-option
                    ><mat-option value="deny">{{
                      "admin.denyPreview" | hhTranslate: "Deny preview"
                    }}</mat-option></mat-select
                  ></mat-form-field
                >
                <mat-form-field appearance="outline"
                  ><mat-label>{{
                    "admin.evidenceTtlSeconds"
                      | hhTranslate: "Evidence TTL seconds"
                  }}</mat-label
                  ><input
                    matInput
                    type="number"
                    min="60"
                    max="3600"
                    [(ngModel)]="policy.evidenceTtlSeconds"
                /></mat-form-field>
                <mat-form-field appearance="outline"
                  ><mat-label>{{
                    "admin.requiredSignals"
                      | hhTranslate: "Required signals (comma-separated)"
                  }}</mat-label
                  ><input matInput [(ngModel)]="requiredSignalsText"
                /></mat-form-field>
                <div class="provider-list">
                  <span
                    *ngFor="let provider of policy.providers"
                    class="pill"
                    >{{ provider }}</span
                  >
                </div>
                <div class="actions" *ngIf="can('admin.settings.write')">
                  <hh-action-button
                    [disabled]="busy"
                    (pressed)="savePolicy()"
                    kind="primary"
                    icon="save"
                    [label]="'admin.savePolicy' | hhTranslate: 'Save policy'"
                  /><hh-action-button
                    [disabled]="busy"
                    (pressed)="killSwitch()"
                    kind="danger"
                    icon="warning"
                    [label]="
                      'admin.killSwitch'
                        | hhTranslate: 'Set observe / kill switch'
                    "
                  /><hh-action-button
                    [disabled]="busy"
                    (pressed)="rollbackPolicy()"
                    kind="secondary"
                    icon="restore"
                    [label]="
                      'admin.rollbackPolicy'
                        | hhTranslate: 'Rollback previous policy'
                    "
                  />
                </div> </mat-card-content
            ></mat-card>
            <mat-card
              ><mat-card-header
                ><mat-card-title>{{
                  "admin.decisionPreview" | hhTranslate: "Decision preview"
                }}</mat-card-title
                ><mat-card-subtitle>{{
                  "admin.normalizedEvidenceOnly"
                    | hhTranslate
                      : "Normalized evidence only; raw attestation is not accepted."
                }}</mat-card-subtitle></mat-card-header
              ><mat-card-content class="form-grid">
                <mat-form-field appearance="outline"
                  ><mat-label>{{
                    "admin.userId" | hhTranslate: "User ID"
                  }}</mat-label
                  ><input matInput [(ngModel)]="preview.userId"
                /></mat-form-field>
                <mat-form-field appearance="outline"
                  ><mat-label>{{
                    "admin.deviceId" | hhTranslate: "Device ID"
                  }}</mat-label
                  ><input matInput [(ngModel)]="preview.deviceId"
                /></mat-form-field>
                <mat-form-field appearance="outline"
                  ><mat-label>{{
                    "admin.provider" | hhTranslate: "Provider"
                  }}</mat-label
                  ><mat-select [(ngModel)]="preview.provider"
                    ><mat-option
                      *ngFor="let provider of policy.providers"
                      [value]="provider"
                      >{{ provider }}</mat-option
                    ></mat-select
                  ></mat-form-field
                >
                <mat-form-field appearance="outline"
                  ><mat-label>{{
                    "admin.signalsJson" | hhTranslate: "Signals JSON"
                  }}</mat-label
                  ><textarea
                    matInput
                    rows="3"
                    [(ngModel)]="signalsJson"
                  ></textarea>
                </mat-form-field>
                <hh-action-button
                  [disabled]="busy"
                  (pressed)="runPreview()"
                  kind="primary"
                  icon="science"
                  [label]="'admin.preview' | hhTranslate: 'Preview'"
                />
                <div class="result" *ngIf="evaluation">
                  <strong>{{ evaluation.decision }}</strong
                  ><span
                    >{{ "admin.fresh" | hhTranslate: "Fresh" }}:
                    {{
                      evaluation.fresh
                        ? ("admin.yes" | hhTranslate: "yes")
                        : ("admin.no" | hhTranslate: "no")
                    }}
                    · {{ "admin.expires" | hhTranslate: "expires" }}
                    {{ evaluation.expiresAt | date: "short" }}</span
                  ><span class="mono"
                    >{{ "admin.hash" | hhTranslate: "hash" }}
                    {{ evaluation.evidenceHash.slice(0, 12) }}…</span
                  >
                </div>
              </mat-card-content></mat-card
            >
          </section>
          <mat-card
            ><mat-card-header
              ><mat-card-title>{{
                "admin.postureAssessments" | hhTranslate: "Posture assessments"
              }}</mat-card-title
              ><mat-card-subtitle>{{
                "admin.normalizedEvidenceOnly"
                  | hhTranslate
                    : "Normalized evidence only; raw attestation is not accepted."
              }}</mat-card-subtitle></mat-card-header
            ><mat-card-content class="table-wrap"
              ><table>
                <thead>
                  <tr>
                    <th>{{ "admin.provider" | hhTranslate: "Provider" }}</th>
                    <th>{{ "admin.deviceId" | hhTranslate: "Device ID" }}</th>
                    <th>{{ "admin.decision" | hhTranslate: "Decision" }}</th>
                    <th>{{ "admin.fresh" | hhTranslate: "Fresh" }}</th>
                    <th>{{ "admin.expires" | hhTranslate: "Expires" }}</th>
                    <th>{{ "admin.hash" | hhTranslate: "Evidence hash" }}</th>
                    <th>
                      {{ "admin.correlation" | hhTranslate: "Correlation" }}
                    </th>
                  </tr>
                </thead>
                <tbody>
                  <tr *ngFor="let assessment of assessments">
                    <td>{{ assessment.provider }}</td>
                    <td class="mono">{{ assessment.deviceId }}</td>
                    <td>{{ assessment.decision }}</td>
                    <td>
                      {{
                        assessment.fresh
                          ? ("admin.yes" | hhTranslate: "yes")
                          : ("admin.no" | hhTranslate: "no")
                      }}
                    </td>
                    <td>{{ assessment.expiresAt | date: "short" }}</td>
                    <td class="mono">{{ assessment.evidenceHashPrefix }}</td>
                    <td class="mono">{{ assessment.correlationId || "—" }}</td>
                  </tr>
                </tbody>
              </table>
              <p class="muted" *ngIf="!assessments.length">
                {{
                  "admin.noPostureAssessments"
                    | hhTranslate: "No posture assessments returned."
                }}
              </p></mat-card-content
            ></mat-card
          >
        </mat-tab>
      </mat-tab-group>
      <p class="error" *ngIf="error">{{ error }}</p>
    </hh-page-layout>
  `,
  styles: [
    `
      .notice {
        display: flex;
        gap: 10px;
        align-items: flex-start;
        padding: 14px 16px;
        margin-bottom: var(--space-4);
        border: 1px solid var(--border-default);
        border-radius: var(--radius-card);
        background: var(--surface-muted);
        color: var(--text-secondary);
      }
      .notice mat-icon {
        color: var(--color-primary);
      }
      .grid {
        display: grid;
        gap: var(--space-4);
        margin: var(--space-4) 0;
      }
      .two-col {
        grid-template-columns: repeat(2, minmax(0, 1fr));
      }
      .form-grid {
        display: grid;
        gap: var(--space-3);
      }
      .status-row {
        display: flex;
        justify-content: space-between;
        gap: var(--space-3);
        padding: 10px 0;
        border-bottom: 1px solid var(--border-subtle);
        font-size: var(--font-size-caption);
      }
      .status-row strong {
        color: var(--text-secondary);
      }
      .status-row strong.good {
        color: var(--color-success);
      }
      .status-row strong.warn {
        color: var(--color-warning);
      }
      .status-row strong.bad {
        color: var(--color-danger);
      }
      .provider-list,
      .actions {
        display: flex;
        flex-wrap: wrap;
        gap: 8px;
      }
      .pill {
        padding: 5px 9px;
        border-radius: 999px;
        background: var(--surface-muted);
        font-size: var(--font-size-caption);
      }
      .table-wrap {
        overflow: auto;
      }
      table {
        width: 100%;
        border-collapse: collapse;
        font-size: var(--font-size-caption);
      }
      th,
      td {
        text-align: left;
        padding: 9px;
        border-bottom: 1px solid var(--border-subtle);
        white-space: nowrap;
      }
      .mono {
        font-family: var(--font-mono);
        font-size: 11px;
      }
      .muted {
        color: var(--text-muted);
        font-size: var(--font-size-caption);
      }
      .result {
        display: grid;
        gap: 4px;
        padding: 12px;
        border-radius: var(--radius-input);
        background: var(--surface-muted);
      }
      .result strong {
        text-transform: uppercase;
      }
      .error {
        color: var(--color-danger);
      }
      @media (max-width: 800px) {
        .two-col {
          grid-template-columns: 1fr;
        }
      }
    `,
  ],
})
export class IdentityCapabilitiesPageComponent {
  private readonly api = inject(IdentityCapabilitiesApiService);
  private readonly capabilities = inject(IdentityCapabilitiesService);
  private readonly snackBar = inject(MatSnackBar);
  private readonly cdr = inject(ChangeDetectorRef);
  private readonly i18n = inject(HisHopeI18nService);
  private readonly permissions = inject(HisHopePermissionService);
  private readonly errorMessages = inject(ApiErrorMessageService);
  private readonly destroyRef = inject(DestroyRef);
  readonly resource = new HisHopeResourceState<IdentityCapabilityState | null>(
    this.destroyRef,
  );
  get loading(): boolean {
    return this.resource.loading();
  }
  busy = false;
  error = "";
  policy?: DevicePosturePolicy;
  assessments: DevicePostureAssessment[] = [];
  evaluation?: DevicePostureEvaluation;
  auditRows: AuditLogRow[] = [];
  lastJob?: ProvisioningJob;
  provisioningJobs: ProvisioningJob[] = [];
  provisioningReadiness?: ProvisioningReadiness;
  deliveryHealth?: DeliveryHealth;
  mtlsBindings: MtlsBinding[] = [];
  radiusStatus?: RadiusEapTlsStatus;
  ssfStatus?: SecuritySignalStatus;
  ssfOutbox: SecuritySignalOutboxEntry[] = [];
  provisioningMode = "unknown";
  requiredSignalsText = "";
  signalsJson = '{"managed":true,"encrypted":true}';
  preview = {
    userId: "",
    deviceId: "pilot-device",
    provider: "advanced-compliance",
  };
  provisioning = {
    target: "scim",
    resourceId: "",
    operation: "update",
    resourceType: "User",
  };
  p0Settings: IdentitySetting[] = [];
  federationSettings: IdentitySetting[] = [];
  scimSsfSettings: IdentitySetting[] = [];
  facilityIds = this.permissions.snapshot()?.facilityIds ?? [];
  selectedFacilityId?: string =
    this.facilityIds.length > 1 ? this.facilityIds[0] : undefined;

  constructor() {
    this.reload();
  }

  can(permission: string): boolean {
    return this.permissions.has(permission);
  }

  reload(): void {
    this.error = "";
    this.resource.load(
      this.capabilities.loadState(this.selectedFacilityId).pipe(
        tap((state) => {
          this.policy = state.policy;
          this.assessments = state.assessments;
          this.requiredSignalsText =
            state.policy?.requiredSignals.join(", ") ?? "";
          this.p0Settings = state.settings.filter((item) =>
            /PASSWORD_HISTORY|AUDIT_|FEDERATION|SCIM_M2M|SSF_|MTLS_|RADIUS_|CSV_EXPORT/.test(
              item.key,
            ),
          );
          this.federationSettings = state.settings.filter((item) =>
            /FEDERATION|EXTERNAL_FEDERATION|ACCOUNT_LINKING|SAML|ENTRA/.test(
              item.key,
            ),
          );
          this.scimSsfSettings = state.settings.filter((item) =>
            /SCIM|SSF/.test(item.key),
          );
          this.provisioningMode = String(
            state.settings.find((item) => item.key === "PROVISIONING_MODE")
              ?.value ?? "dry-run",
          );
          this.auditRows = state.auditRows;
          this.provisioningJobs = state.provisioningJobs;
          this.deliveryHealth = state.deliveryHealth;
          this.api.getProvisioningReadiness().subscribe({
            next: (readiness) => {
              this.provisioningReadiness = readiness;
              this.cdr.markForCheck();
            },
            error: () => (this.provisioningReadiness = undefined),
          });
          this.mtlsBindings = state.mtlsBindings;
          this.radiusStatus = state.radiusStatus;
          this.ssfStatus = state.ssfStatus;
          this.api.getSecuritySignalOutbox().subscribe({
            next: (entries) => {
              this.ssfOutbox = entries;
              this.cdr.markForCheck();
            },
            error: () => (this.ssfOutbox = []),
          });
          this.cdr.markForCheck();
        }),
        catchError(() => {
          this.error = this.i18n.t(
            "admin.identityCapabilitiesLoadFailed",
            "Unable to load identity capability state.",
          );
          return of(null);
        }),
      ),
    );
  }

  async savePolicy(): Promise<void> {
    if (!this.can("admin.settings.write")) return;
    if (!this.policy) return;
    if (
      !window.confirm(
        this.i18n.t(
          "admin.confirmPolicyChange",
          "Apply this device posture policy?",
        ),
      )
    )
      return;
    this.busy = true;
    this.error = "";
    try {
      this.policy = await firstValueFrom(
        this.api.updateDevicePosturePolicy(
          {
            mode: this.policy.mode,
            providers: this.policy.providers,
            evidenceTtlSeconds: Number(this.policy.evidenceTtlSeconds),
            requiredSignals: this.requiredSignalsText
              .split(",")
              .map((value) => value.trim())
              .filter(Boolean),
          },
          this.selectedFacilityId,
        ),
      );
      this.requiredSignalsText = this.policy.requiredSignals.join(", ");
      this.snackBar.open(
        this.i18n.t("admin.policySaved", "Device posture policy saved"),
        this.i18n.t("admin.close", "Close"),
        { duration: 3000 },
      );
    } catch (error) {
      this.error = this.errorMessages.message(
        error,
        "errors.api.policyRejected",
      );
    } finally {
      this.busy = false;
      this.cdr.markForCheck();
    }
  }

  killSwitch(): void {
    if (!this.can("admin.settings.write")) return;
    if (!this.policy) return;
    this.policy = { ...this.policy, mode: "observe" };
    void this.savePolicy();
  }

  async rollbackPolicy(): Promise<void> {
    if (!this.can("admin.settings.write")) return;
    if (
      !window.confirm(
        this.i18n.t(
          "admin.confirmPolicyRollback",
          "Rollback the previous posture policy?",
        ),
      )
    )
      return;
    this.busy = true;
    this.error = "";
    try {
      this.policy = await firstValueFrom(
        this.api.rollbackDevicePosturePolicy(this.selectedFacilityId),
      );
      this.requiredSignalsText = this.policy.requiredSignals.join(", ");
      this.snackBar.open(
        this.i18n.t(
          "admin.policyRolledBack",
          "Device posture policy rolled back",
        ),
        this.i18n.t("admin.close", "Close"),
        { duration: 3000 },
      );
    } catch (error) {
      this.error = this.errorMessages.message(
        error,
        "errors.api.policyRollbackRejected",
      );
    } finally {
      this.busy = false;
      this.cdr.markForCheck();
    }
  }

  async runPreview(): Promise<void> {
    if (!this.policy) return;
    this.busy = true;
    this.error = "";
    try {
      const signals = JSON.parse(this.signalsJson) as Record<string, boolean>;
      this.evaluation = await firstValueFrom(
        this.api.previewDevicePosture(
          { ...this.preview, signals, observedAt: new Date().toISOString() },
          this.selectedFacilityId,
        ),
      );
    } catch (error) {
      this.error = this.errorMessages.message(
        error,
        "errors.api.previewRejected",
      );
    } finally {
      this.busy = false;
      this.cdr.markForCheck();
    }
  }

  async queueProvisioning(): Promise<void> {
    if (!this.can("admin.provisioning.manage")) return;
    if (
      !window.confirm(
        this.i18n.t(
          "admin.confirmProvisioningQueue",
          "Queue this provisioning job in dry-run mode?",
        ),
      )
    )
      return;
    this.busy = true;
    this.error = "";
    try {
      this.lastJob = await firstValueFrom(
        this.api.queueProvisioning({
          ...this.provisioning,
          payload: { dryRun: true },
        }),
      );
      this.snackBar.open(
        this.i18n.t("admin.provisioningQueued", "Provisioning job queued"),
        this.i18n.t("admin.close", "Close"),
        { duration: 3000 },
      );
    } catch {
      this.error = this.i18n.t(
        "admin.provisioningRejected",
        "Provisioning queue request was rejected.",
      );
    } finally {
      this.busy = false;
      this.cdr.markForCheck();
    }
  }

  async revokeBinding(binding: MtlsBinding): Promise<void> {
    if (!this.can("admin.clients.write")) return;
    if (
      !window.confirm(
        this.i18n.t(
          "admin.confirmCertificateRevoke",
          "Revoke this certificate binding?",
        ),
      )
    )
      return;
    this.busy = true;
    this.error = "";
    try {
      await firstValueFrom(this.api.revokeMtlsBinding(binding.id));
      this.mtlsBindings = this.mtlsBindings.map((item) =>
        item.id === binding.id
          ? { ...item, status: "revoked", revokedAt: new Date().toISOString() }
          : item,
      );
      this.snackBar.open(
        this.i18n.t("admin.bindingRevoked", "Certificate binding revoked"),
        this.i18n.t("admin.close", "Close"),
        { duration: 3000 },
      );
    } catch {
      this.error = this.i18n.t(
        "admin.bindingRevokeRejected",
        "Certificate binding revoke was rejected.",
      );
    } finally {
      this.busy = false;
      this.cdr.markForCheck();
    }
  }

  async retrySsf(entry: SecuritySignalOutboxEntry): Promise<void> {
    if (!this.can("admin.settings.write")) return;
    if (
      !window.confirm(
        this.i18n.t("admin.confirmSsfRetry", "Retry this SSF delivery?"),
      )
    )
      return;
    this.busy = true;
    this.error = "";
    try {
      await firstValueFrom(this.api.retrySecuritySignal(entry.id));
      this.ssfOutbox = this.ssfOutbox.filter((item) => item.id !== entry.id);
      this.snackBar.open(
        this.i18n.t("admin.ssfRetryQueued", "SSF retry queued"),
        this.i18n.t("admin.close", "Close"),
        { duration: 3000 },
      );
    } catch {
      this.error = this.i18n.t(
        "admin.ssfRetryRejected",
        "SSF retry was rejected.",
      );
    } finally {
      this.busy = false;
      this.cdr.markForCheck();
    }
  }

  async retryJob(job: ProvisioningJob): Promise<void> {
    if (!this.can("admin.provisioning.manage")) return;
    if (
      !window.confirm(
        this.i18n.t(
          "admin.confirmProvisioningRetry",
          "Retry this provisioning job?",
        ),
      )
    )
      return;
    this.busy = true;
    this.error = "";
    try {
      await firstValueFrom(this.api.retryProvisioningJob(job.id));
      this.provisioningJobs = this.provisioningJobs.map((item) =>
        item.id === job.id
          ? { ...item, lastError: undefined, completedAt: undefined }
          : item,
      );
      this.snackBar.open(
        this.i18n.t("admin.retryQueued", "Retry queued"),
        this.i18n.t("admin.close", "Close"),
        { duration: 3000 },
      );
    } catch {
      this.error = this.i18n.t(
        "admin.retryRejected",
        "Retry was rejected by the server.",
      );
    } finally {
      this.busy = false;
      this.cdr.markForCheck();
    }
  }

  async downloadAuditCsv(): Promise<void> {
    if (!this.can("admin.audit.read")) return;
    this.busy = true;
    this.error = "";
    try {
      const blob = await firstValueFrom(this.api.exportAuditCsv());
      const url = URL.createObjectURL(blob);
      const anchor = document.createElement("a");
      anchor.href = url;
      anchor.download = `audit-export-${new Date().toISOString().slice(0, 10)}.csv`;
      anchor.click();
      URL.revokeObjectURL(url);
      this.snackBar.open(
        this.i18n.t("admin.exportReady", "Audit CSV export ready"),
        this.i18n.t("admin.close", "Close"),
        { duration: 3000 },
      );
    } catch {
      this.error = this.i18n.t(
        "admin.exportRejected",
        "Audit export was rejected by the server.",
      );
    } finally {
      this.busy = false;
      this.cdr.markForCheck();
    }
  }

  isEnabled(value: unknown): boolean {
    return value === true || value === "true" || value === "enabled";
  }
}
