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
  InfraAuditApiService,
  InfraAuditEvent,
  InfraAuditMeta,
  InfraAuditQuery,
  InfraAuditSummary,
  WarpgateOpenSession,
} from "../../core/services/infra-audit-api.service";

/** Range presets in hours; the server caps one query at 31 days and keeps 90. */
export const RANGE_PRESETS = [1, 24, 24 * 7, 24 * 30] as const;

/**
 * Infrastructure access audit (plan stage 6A). Super-admins see every source with filters,
 * a summary and CSV export (fresh MFA). Everyone else gets "My activity" only: the server
 * answers 403 on the full audit, and this page falls back to /infra-audit/me.
 */
@Component({
  selector: "app-infra-audit-page",
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
    HisHopeDataTableComponent,
    HisHopePageHeaderComponent,
    HisHopePageLayoutComponent,
    HisHopeTranslatePipe,
  ],
  template: `
    <hh-page-layout>
      <hh-page-header
        hhPageHeader
        [title]="
          (mineOnly ? 'admin.infraAuditMine' : 'admin.infraAudit')
            | hhTranslate: (mineOnly ? 'My infrastructure activity' : 'Infrastructure audit')
        "
        [subtitle]="
          'admin.infraAuditSubtitle'
            | hhTranslate
              : 'Who reached consoles, Vault, SSH and Kubernetes, and what they did. Kept 90 days.'
        "
      >
        <hh-action-button
          [disabled]="loading"
          (pressed)="reload()"
          kind="secondary"
          icon="refresh"
          [label]="'admin.refresh' | hhTranslate: 'Refresh'"
        />
        @if (!mineOnly && configured) {
          <hh-action-button
            [disabled]="exporting"
            (pressed)="exportCsv()"
            kind="secondary"
            icon="download"
            [label]="'admin.infraAuditExport' | hhTranslate: 'Export CSV'"
          />
        }
      </hh-page-header>

      @if (!configured && !mineOnly && metaLoaded) {
        <p class="warning">
          {{
            "admin.infraAuditNotConfigured"
              | hhTranslate
                : "The audit log source is not configured on this IdentityService yet."
          }}
        </p>
      }
      @if (message) {
        <p class="error" role="alert">{{ message }}</p>
      }

      <form class="filters" [formGroup]="filters" (ngSubmit)="reload()">
        <mat-form-field appearance="outline">
          <mat-label>{{ "admin.infraAuditRange" | hhTranslate: "Time range" }}</mat-label>
          <mat-select formControlName="hours">
            @for (hours of rangePresets; track hours) {
              <mat-option [value]="hours">{{ rangeLabel(hours) }}</mat-option>
            }
          </mat-select>
        </mat-form-field>
        <mat-form-field appearance="outline">
          <mat-label>{{ "admin.infraAuditSources" | hhTranslate: "Sources" }}</mat-label>
          <mat-select formControlName="sources" multiple>
            @for (source of sources; track source) {
              <mat-option [value]="source">{{ source }}</mat-option>
            }
          </mat-select>
        </mat-form-field>
        @if (!mineOnly) {
          <mat-form-field appearance="outline">
            <mat-label>{{ "admin.infraAuditResult" | hhTranslate: "Result" }}</mat-label>
            <mat-select formControlName="result">
              <mat-option [value]="''">{{ "admin.all" | hhTranslate: "All" }}</mat-option>
              @for (result of results; track result) {
                <mat-option [value]="result">{{ result }}</mat-option>
              }
            </mat-select>
          </mat-form-field>
          <mat-form-field appearance="outline">
            <mat-label>{{ "admin.infraAuditActor" | hhTranslate: "Person" }}</mat-label>
            <input matInput formControlName="actor" maxlength="200" />
          </mat-form-field>
          <mat-form-field appearance="outline">
            <mat-label>{{ "admin.infraAuditText" | hhTranslate: "Contains" }}</mat-label>
            <input matInput formControlName="text" maxlength="200" />
          </mat-form-field>
        }
        <hh-action-button
          type="submit"
          kind="primary"
          icon="search"
          [label]="'admin.apply' | hhTranslate: 'Apply'"
        />
      </form>

      @if (summary) {
        <div class="cards">
          <mat-card><mat-card-content>
            <strong>{{ summary.total }}{{ summary.truncated ? "+" : "" }}</strong>
            <span>{{ "admin.infraAuditTotal" | hhTranslate: "Events" }}</span>
          </mat-card-content></mat-card>
          <mat-card [class.alert]="(summary.byResult['denied'] || 0) > 0"><mat-card-content>
            <strong>{{ summary.byResult["denied"] || 0 }}</strong>
            <span>{{ "admin.infraAuditDenied" | hhTranslate: "Denied" }}</span>
          </mat-card-content></mat-card>
          <mat-card [class.alert]="(summary.byResult['error'] || 0) > 0"><mat-card-content>
            <strong>{{ summary.byResult["error"] || 0 }}</strong>
            <span>{{ "admin.infraAuditErrors" | hhTranslate: "Errors" }}</span>
          </mat-card-content></mat-card>
          @for (entry of sourceCounts; track entry.key) {
            <mat-card><mat-card-content>
              <strong>{{ entry.value }}</strong><span>{{ entry.key }}</span>
            </mat-card-content></mat-card>
          }
        </div>
        @if (summary.topActors.length) {
          <p class="top">
            {{ "admin.infraAuditTopActors" | hhTranslate: "Most active" }}:
            @for (actor of summary.topActors; track actor.key; let last = $last) {
              <span>{{ actor.key }} ({{ actor.value }}){{ last ? "" : ", " }}</span>
            }
          </p>
        }
      }

      @if (!mineOnly && configured && metaLoaded) {
        <mat-card class="sessions">
          <mat-card-header>
            <mat-card-title>{{
              "admin.infraAuditWarpgateSessions" | hhTranslate: "Open Warpgate sessions"
            }}</mat-card-title>
            <hh-action-button
              [disabled]="sessionsLoading"
              (pressed)="loadSessions()"
              kind="secondary"
              mode="icon-only"
              icon="refresh"
              [label]="'admin.refresh' | hhTranslate: 'Refresh'"
            />
          </mat-card-header>
          <mat-card-content>
            <p class="top">
              {{
                "admin.infraAuditWarpgateSessionsHint"
                  | hhTranslate
                    : "Sessions that started in the selected range (at least 24 hours) and have not ended."
              }}
            </p>
            @if (sessionsTruncated) {
              <p class="top">
                {{
                  "admin.infraAuditWarpgateSessionsTruncated"
                    | hhTranslate
                      : "Too many Warpgate events in this range; the oldest sessions may be missing."
                }}
              </p>
            }
            <hh-data-table
              [label]="'admin.infraAuditWarpgateSessions' | hhTranslate: 'Open Warpgate sessions'"
              [columns]="sessionColumns"
              [rows]="sessionRows"
              [loading]="sessionsLoading"
              [error]="sessionsError"
              [empty]="!sessionsLoading && !sessionsError && !sessionRows.length"
              [emptyMessage]="
                'admin.infraAuditWarpgateSessionsEmpty' | hhTranslate: 'No open Warpgate sessions.'
              "
            />
          </mat-card-content>
        </mat-card>
      }

      <hh-data-table
        [label]="'admin.infraAudit' | hhTranslate: 'Infrastructure audit'"
        [columns]="columns"
        [rows]="rows"
        [loading]="loading"
        [error]="tableError"
        [empty]="!loading && !tableError && !rows.length"
        [emptyMessage]="'admin.infraAuditEmpty' | hhTranslate: 'No events in this range.'"
      />
      @if (nextTo) {
        <hh-action-button
          [disabled]="loading"
          (pressed)="loadOlder()"
          kind="secondary"
          icon="expand_more"
          [label]="'admin.infraAuditOlder' | hhTranslate: 'Load older events'"
        />
      }
    </hh-page-layout>
  `,
  styles: [
    `
      .filters {
        display: flex;
        flex-wrap: wrap;
        gap: var(--space-md);
        align-items: center;
      }
      .cards {
        display: grid;
        grid-template-columns: repeat(auto-fill, minmax(9rem, 1fr));
        gap: var(--space-md);
        margin: var(--space-md) 0;
      }
      .cards strong {
        display: block;
        font-size: 1.5rem;
        font-weight: var(--font-weight-semibold);
      }
      .cards span,
      .top {
        color: var(--text-secondary);
        font-size: var(--font-size-caption);
      }
      .cards .alert strong {
        color: var(--color-danger);
      }
      .sessions {
        margin: var(--space-md) 0;
      }
      .sessions mat-card-header {
        display: flex;
        align-items: center;
        justify-content: space-between;
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
export class InfraAuditPageComponent implements OnInit {
  private readonly api = inject(InfraAuditApiService);
  private readonly i18n = inject(HisHopeI18nService);
  private readonly cdr = inject(ChangeDetectorRef);

  readonly rangePresets = RANGE_PRESETS;
  readonly filters = new FormGroup({
    hours: new FormControl<number>(24, { nonNullable: true }),
    sources: new FormControl<string[]>([], { nonNullable: true }),
    result: new FormControl<string>("", { nonNullable: true }),
    actor: new FormControl<string>("", { nonNullable: true }),
    text: new FormControl<string>("", { nonNullable: true }),
  });

  sources: string[] = ["console", "console-access", "vault", "identity", "headscale", "kubernetes", "ssh", "warpgate"];
  results: string[] = ["allowed", "denied", "error", "info"];
  metaLoaded = false;
  configured = true;
  mineOnly = false;
  loading = false;
  exporting = false;
  message = "";
  tableError = "";
  summary: InfraAuditSummary | null = null;
  rows: Record<string, unknown>[] = [];
  nextTo: string | null = null;
  sessionRows: Record<string, unknown>[] = [];
  sessionsLoading = false;
  sessionsError = "";
  sessionsTruncated = false;
  private window: { from: string; to: string } | null = null;

  get sourceCounts(): Array<{ key: string; value: number }> {
    return Object.entries(this.summary?.bySource ?? {})
      .map(([key, value]) => ({ key, value }))
      .sort((left, right) => right.value - left.value);
  }

  get columns(): HisHopeDataTableColumn[] {
    this.i18n.locale();
    return [
      { key: "timestamp", label: this.i18n.t("admin.time", "Time"), format: "dateTime" },
      { key: "source", label: this.i18n.t("admin.infraAuditSource", "Source") },
      { key: "actor", label: this.i18n.t("admin.infraAuditActor", "Person") },
      { key: "action", label: this.i18n.t("admin.action", "Action") },
      { key: "target", label: this.i18n.t("admin.target", "Target") },
      { key: "result", label: this.i18n.t("admin.infraAuditResult", "Result") },
      { key: "sourceIp", label: this.i18n.t("admin.sourceIp", "Source IP") },
      { key: "detail", label: this.i18n.t("admin.detail", "Detail") },
    ];
  }

  get sessionColumns(): HisHopeDataTableColumn[] {
    this.i18n.locale();
    return [
      { key: "user", label: this.i18n.t("admin.infraAuditActor", "Person") },
      { key: "target", label: this.i18n.t("admin.target", "Target") },
      { key: "startedAt", label: this.i18n.t("admin.infraAuditStartedAt", "Started"), format: "dateTime" },
      { key: "sourceIp", label: this.i18n.t("admin.sourceIp", "Source IP") },
      { key: "sessionId", label: this.i18n.t("admin.infraAuditSessionId", "Warpgate session") },
    ];
  }

  ngOnInit(): void {
    this.api.getMeta().subscribe({
      next: (meta) => this.applyMeta(meta),
      error: (error: HttpErrorResponse) => {
        // Not a super-admin: only the caller's own activity is available.
        this.mineOnly = error.status === 403 || error.status === 401;
        if (!this.mineOnly) this.message = this.describe(error);
        this.metaLoaded = true;
        this.reload();
      },
    });
  }

  rangeLabel(hours: number): string {
    return hours < 24
      ? this.i18n.t("admin.lastHour", "Last hour")
      : hours === 24
        ? this.i18n.t("admin.last24Hours", "Last 24 hours")
        : this.i18n.t("admin.lastDays", "Last {{days}} days").replace("{{days}}", String(hours / 24));
  }

  reload(): void {
    const to = new Date();
    const from = new Date(to.getTime() - this.filters.controls.hours.value * 3_600_000);
    this.window = { from: from.toISOString(), to: to.toISOString() };
    this.rows = [];
    this.nextTo = null;
    this.message = "";
    this.load(this.window.to);
    if (!this.mineOnly && this.configured) {
      this.loadSessions();
      this.api.getSummary(this.query(this.window.to)).subscribe({
        next: (summary) => {
          this.summary = summary;
          this.cdr.markForCheck();
        },
        error: () => {
          this.summary = null;
          this.cdr.markForCheck();
        },
      });
    }
  }

  /** Open Warpgate sessions over the selected range, never less than a day. */
  loadSessions(): void {
    if (this.mineOnly || !this.configured) return;
    this.sessionsLoading = true;
    this.sessionsError = "";
    this.cdr.markForCheck();
    this.api.getWarpgateSessions(Math.max(24, this.filters.controls.hours.value)).subscribe({
      next: (result) => {
        this.sessionRows = result.items.map((item) => toSessionRow(item));
        this.sessionsTruncated = result.truncated;
        this.sessionsLoading = false;
        this.cdr.markForCheck();
      },
      error: (error: HttpErrorResponse) => {
        this.sessionRows = [];
        this.sessionsTruncated = false;
        this.sessionsLoading = false;
        this.sessionsError = this.describe(error);
        this.cdr.markForCheck();
      },
    });
  }

  loadOlder(): void {
    if (this.nextTo) this.load(this.nextTo);
  }

  exportCsv(): void {
    if (!this.window) return;
    this.exporting = true;
    this.message = "";
    this.api.export(this.query(this.window.to)).subscribe({
      next: (response) => {
        const name =
          /filename="?([^";]+)"?/.exec(response.headers.get("content-disposition") ?? "")?.[1] ??
          "infra-audit.csv";
        const url = URL.createObjectURL(response.body ?? new Blob());
        const link = document.createElement("a");
        link.href = url;
        link.download = name;
        link.click();
        URL.revokeObjectURL(url);
        this.exporting = false;
        this.cdr.markForCheck();
      },
      error: (error: HttpErrorResponse) => {
        this.exporting = false;
        this.message = this.describe(error);
        this.cdr.markForCheck();
      },
    });
  }

  private applyMeta(meta: InfraAuditMeta): void {
    this.metaLoaded = true;
    this.configured = meta.configured;
    this.sources = meta.sources;
    this.results = meta.results;
    if (this.configured) this.reload();
    this.cdr.markForCheck();
  }

  private load(to: string): void {
    if (!this.window) return;
    this.loading = true;
    this.tableError = "";
    const query = this.query(to);
    const request = this.mineOnly ? this.api.getMyEvents(query) : this.api.getEvents(query);
    request.subscribe({
      next: (page) => {
        this.rows = [...this.rows, ...page.items.map((item) => toRow(item))];
        this.nextTo = page.nextTo;
        this.loading = false;
        this.cdr.markForCheck();
      },
      error: (error: HttpErrorResponse) => {
        this.loading = false;
        this.tableError = this.describe(error);
        this.cdr.markForCheck();
      },
    });
  }

  private query(to: string): InfraAuditQuery {
    const value = this.filters.getRawValue();
    return {
      from: this.window?.from,
      to,
      sources: value.sources,
      actor: this.mineOnly ? undefined : value.actor,
      text: this.mineOnly ? undefined : value.text,
      result: this.mineOnly ? undefined : value.result || undefined,
      limit: 100,
    };
  }

  private describe(error: HttpErrorResponse): string {
    const code = (error.error as { errorCode?: string } | null)?.errorCode;
    if (code === "step_up_required")
      return this.i18n.t(
        "admin.infraAuditStepUp",
        "Export needs a fresh MFA sign-in. Sign out, sign in again with MFA, then export within 15 minutes.",
      );
    if (code === "infra_audit_unavailable")
      return this.i18n.t(
        "admin.infraAuditUnavailable",
        "The audit log store is unreachable right now. Try again shortly.",
      );
    if (code === "invalid_infra_audit_query")
      return this.i18n.t("admin.infraAuditInvalid", "The filter is not valid.");
    return this.i18n.t("admin.infraAuditFailed", "Unable to load the infrastructure audit.");
  }
}

export function toRow(item: InfraAuditEvent): Record<string, unknown> {
  return {
    ...item,
    id: `${item.timestamp}|${item.correlationId ?? ""}|${item.action}`,
    actor: item.actor ?? "—",
    target: item.target ?? "",
    sourceIp: item.sourceIp ?? "",
    detail: item.detail ?? "",
  };
}

export function toSessionRow(item: WarpgateOpenSession): Record<string, unknown> {
  return {
    ...item,
    id: item.sessionId,
    user: item.user ?? "—",
    target: item.target ?? "",
    sourceIp: item.sourceIp ?? "",
  };
}
