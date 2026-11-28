import { ChangeDetectionStrategy, ChangeDetectorRef, Component, OnInit, inject } from "@angular/core";
import { CommonModule } from "@angular/common";
import { HttpErrorResponse } from "@angular/common/http";
import { FormControl, FormGroup, ReactiveFormsModule } from "@angular/forms";
import { MatCardModule } from "@angular/material/card";
import { MatFormFieldModule } from "@angular/material/form-field";
import { MatInputModule } from "@angular/material/input";
import { MatSelectModule } from "@angular/material/select";
import { forkJoin } from "rxjs";
import {
  HisHopeActionButtonComponent,
  HisHopePageHeaderComponent,
  HisHopePageLayoutComponent,
} from "@his-hope/frontend-foundation/ui";
import { HisHopeTranslatePipe } from "@his-hope/frontend-foundation/i18n";
import {
  RISK_BANDS,
  RiskApiService,
  RiskBand,
  RiskEvent,
  RiskEventsPage,
  RiskEventsQuery,
  RiskReason,
  RiskSummary,
} from "../../core/services/risk-api.service";

export const RISK_RANGE_DAYS = [1, 7, 31] as const;
export const RISK_PAGE_SIZE = 50;

/**
 * IAM → Risk (Zero Trust 3.5 R0, super-admin only, server-enforced and audited): sign-in risk
 * scores with their reasons and the counts of what the policy would have done. Report-only:
 * nothing on this page changes a sign-in.
 */
@Component({
  selector: "app-risk-page",
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
    HisHopePageHeaderComponent,
    HisHopePageLayoutComponent,
    HisHopeTranslatePipe,
  ],
  template: `
    <hh-page-layout>
      <hh-page-header
        hhPageHeader
        [title]="'admin.risk' | hhTranslate: 'Risk'"
        [subtitle]="
          'admin.riskSubtitle'
            | hhTranslate
              : 'Sign-in risk scores (report-only): nothing is blocked; counts show what the policy would have done.'
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

      @if (message) {
        <p class="error" role="alert">{{ message }}</p>
      }

      <section class="cards" aria-live="polite">
        <mat-card class="band-high"><strong>{{ summary?.windows?.["24h"]?.bands?.high ?? 0 }}</strong><span>{{ "admin.riskHigh24h" | hhTranslate: "High, 24 h" }}</span></mat-card>
        <mat-card class="band-medium"><strong>{{ summary?.windows?.["24h"]?.bands?.medium ?? 0 }}</strong><span>{{ "admin.riskMedium24h" | hhTranslate: "Medium, 24 h" }}</span></mat-card>
        <mat-card><strong>{{ summary?.windows?.["24h"]?.bands?.low ?? 0 }}</strong><span>{{ "admin.riskLow24h" | hhTranslate: "Low, 24 h" }}</span></mat-card>
        <mat-card class="band-high"><strong>{{ summary?.windows?.["7d"]?.bands?.high ?? 0 }}</strong><span>{{ "admin.riskHigh7d" | hhTranslate: "High, 7 days" }}</span></mat-card>
        <mat-card class="band-medium"><strong>{{ summary?.windows?.["7d"]?.wouldStepUp ?? 0 }}</strong><span>{{ "admin.riskWouldStepUp" | hhTranslate: "Would step up, 7 days" }}</span></mat-card>
        <mat-card class="band-high"><strong>{{ summary?.windows?.["7d"]?.wouldDeny ?? 0 }}</strong><span>{{ "admin.riskWouldDeny" | hhTranslate: "Would deny, 7 days" }}</span></mat-card>
      </section>

      <form class="filters" [formGroup]="filters" (ngSubmit)="search()">
        <mat-form-field appearance="outline">
          <mat-label>{{ "admin.infraAuditRange" | hhTranslate: "Time range" }}</mat-label>
          <mat-select formControlName="days">
            @for (days of rangeDays; track days) {
              <mat-option [value]="days">{{ days === 1 ? "24 h" : days + " d" }}</mat-option>
            }
          </mat-select>
        </mat-form-field>
        <mat-form-field appearance="outline">
          <mat-label>{{ "admin.riskBand" | hhTranslate: "Band" }}</mat-label>
          <mat-select formControlName="band">
            <mat-option value="">{{ "admin.all" | hhTranslate: "All" }}</mat-option>
            @for (band of bands; track band) {
              <mat-option [value]="band">{{ band }}</mat-option>
            }
          </mat-select>
        </mat-form-field>
        <mat-form-field appearance="outline">
          <mat-label>{{ "admin.riskUser" | hhTranslate: "User (name or id)" }}</mat-label>
          <input matInput formControlName="user" maxlength="256" />
        </mat-form-field>
        <hh-action-button type="submit" kind="primary" icon="search" [label]="'admin.apply' | hhTranslate: 'Apply'" />
      </form>

      <div class="table-wrap">
        <table class="events" [attr.aria-busy]="loading">
          <thead>
            <tr>
              <th scope="col">{{ "admin.time" | hhTranslate: "Time" }}</th>
              <th scope="col">{{ "admin.user" | hhTranslate: "User" }}</th>
              <th scope="col">{{ "admin.riskEvent" | hhTranslate: "Event" }}</th>
              <th scope="col">{{ "admin.riskScore" | hhTranslate: "Score" }}</th>
              <th scope="col">{{ "admin.riskBand" | hhTranslate: "Band" }}</th>
              <th scope="col">{{ "admin.riskReasons" | hhTranslate: "Reasons" }}</th>
              <th scope="col">{{ "admin.riskIpPrefix" | hhTranslate: "IP prefix" }}</th>
            </tr>
          </thead>
          <tbody>
            @for (event of page?.items ?? []; track event.id) {
              <tr [class.would-act]="isWouldAct(event)">
                <td class="nowrap">{{ event.timestamp | date: "yyyy-MM-dd HH:mm:ss" }}</td>
                <td class="user">
                  <button type="button" class="link" (click)="filterUser(event)">{{ event.userName ?? event.userId }}</button>
                </td>
                <td>{{ eventLabel(event) }}@if (event.outcome) { <span class="muted">({{ event.outcome }})</span> }</td>
                <td class="num">{{ event.score ?? "" }}</td>
                <td>
                  @if (event.band) {
                    <span class="chip" [class]="'chip chip-' + event.band">{{ event.band }}</span>
                  }
                </td>
                <td>
                  @for (reason of event.reasons; track reason.code) {
                    <span class="reason">{{ reasonLabel(reason) }}</span>
                  } @empty {
                    <span class="muted">-</span>
                  }
                </td>
                <td class="nowrap"><code>{{ event.ipPrefix ?? "" }}</code></td>
              </tr>
            } @empty {
              <tr><td colspan="7" class="muted">{{ loading ? "…" : ("admin.riskNoEvents" | hhTranslate: "No scored sign-ins in this range.") }}</td></tr>
            }
          </tbody>
        </table>
      </div>

      @if (page && page.totalCount > page.pageSize) {
        <div class="pager">
          <hh-action-button [disabled]="loading || page.page <= 1" (pressed)="goTo(page.page - 1)" kind="secondary" icon="chevron_left"
            [label]="'admin.previous' | hhTranslate: 'Previous'" />
          <span>{{ page.page }} / {{ pageCount }} ({{ page.totalCount }})</span>
          <hh-action-button [disabled]="loading || page.page >= pageCount" (pressed)="goTo(page.page + 1)" kind="secondary" icon="chevron_right"
            [label]="'admin.next' | hhTranslate: 'Next'" />
        </div>
      }
      @if (lastUpdated) {
        <p class="muted">{{ "admin.lastUpdated" | hhTranslate: "Last updated" }} {{ lastUpdated | date: "HH:mm:ss" }}</p>
      }
    </hh-page-layout>
  `,
  styles: [
    `
      .cards { display: grid; grid-template-columns: repeat(auto-fill, minmax(9rem, 1fr)); gap: var(--space-md); margin: var(--space-md) 0; }
      .cards mat-card { padding: var(--space-md); }
      .cards strong { display: block; font-size: 1.5rem; font-weight: var(--font-weight-semibold); }
      .cards span, .muted { color: var(--text-secondary); font-size: var(--font-size-caption); }
      .band-high strong { color: var(--color-danger); }
      .band-medium strong { color: var(--color-warning); }
      .filters { display: flex; flex-wrap: wrap; gap: var(--space-md); align-items: center; }
      .table-wrap { overflow-x: auto; }
      table.events { width: 100%; border-collapse: collapse; font-size: var(--font-size-body); }
      table.events th, table.events td { text-align: left; vertical-align: top; padding: var(--space-xs) var(--space-sm); border-bottom: var(--space-hairline) solid var(--border-subtle); }
      tr.would-act { border-left: var(--space-xs) solid var(--color-warning); }
      .nowrap { white-space: nowrap; }
      .num { text-align: right; font-variant-numeric: tabular-nums; }
      .user { max-width: 16rem; overflow-wrap: anywhere; }
      .chip { display: inline-block; padding: 0 var(--space-xs); border-radius: var(--radius-chip); font-size: var(--font-size-caption); font-weight: var(--font-weight-semibold); }
      .chip-high { background: var(--color-danger); color: var(--color-on-danger); }
      .chip-medium { background: var(--color-warning); color: var(--color-on-danger); }
      .chip-low { background: var(--border-subtle); }
      .reason { display: inline-block; margin: 0 var(--space-xs) var(--space-hairline) 0; padding: 0 var(--space-xs); border-radius: var(--radius-chip);
        border: var(--space-hairline) solid var(--border-subtle); font-size: var(--font-size-caption); }
      .link { background: none; border: none; padding: 0; color: var(--color-primary); cursor: pointer; font: inherit; text-decoration: underline; text-align: left; }
      .pager { display: flex; gap: var(--space-md); align-items: center; margin: var(--space-sm) 0; }
      code { overflow-wrap: anywhere; }
      .error { margin: var(--space-sm) 0; color: var(--color-danger); }
    `,
  ],
})
export class RiskPageComponent implements OnInit {
  private readonly api = inject(RiskApiService);
  private readonly cdr = inject(ChangeDetectorRef);

  readonly bands = RISK_BANDS;
  readonly rangeDays = RISK_RANGE_DAYS;
  readonly filters = new FormGroup({
    days: new FormControl<number>(7, { nonNullable: true }),
    band: new FormControl<RiskBand | "">("", { nonNullable: true }),
    user: new FormControl<string>("", { nonNullable: true }),
  });

  loading = false;
  message = "";
  page: RiskEventsPage | null = null;
  summary: RiskSummary | null = null;
  lastUpdated: Date | null = null;
  pageNumber = 1;

  get pageCount(): number {
    return this.page ? Math.max(1, Math.ceil(this.page.totalCount / this.page.pageSize)) : 1;
  }

  ngOnInit(): void {
    this.reload();
  }

  query(now = new Date()): RiskEventsQuery {
    const value = this.filters.getRawValue();
    return {
      band: value.band,
      user: value.user,
      from: new Date(now.getTime() - value.days * 86_400_000).toISOString(),
      to: now.toISOString(),
      page: this.pageNumber,
      pageSize: RISK_PAGE_SIZE,
    };
  }

  search(): void {
    this.pageNumber = 1;
    this.reload();
  }

  goTo(pageNumber: number): void {
    this.pageNumber = Math.max(1, pageNumber);
    this.reload();
  }

  reload(): void {
    if (this.loading) return;
    this.loading = true;
    this.cdr.markForCheck();
    forkJoin({ events: this.api.getEvents(this.query()), summary: this.api.getSummary() }).subscribe({
      next: ({ events, summary }) => {
        this.page = events;
        this.summary = summary;
        this.message = "";
        this.lastUpdated = new Date();
        this.loading = false;
        this.cdr.markForCheck();
      },
      error: (error: HttpErrorResponse) => {
        this.message = this.describe(error);
        this.loading = false;
        this.cdr.markForCheck();
      },
    });
  }

  filterUser(event: RiskEvent): void {
    this.filters.controls.user.setValue(event.userId ?? event.userName ?? "");
    this.search();
  }

  isWouldAct(event: RiskEvent): boolean {
    return event.eventType.startsWith("risk_policy_would_");
  }

  eventLabel(event: RiskEvent): string {
    switch (event.eventType) {
      case "login_success":
        return "sign-in";
      case "login_failed":
        return "sign-in failed";
      case "mfa_failed":
        return "MFA failed";
      case "risk_policy_would_step_up":
        return "would step up (report-only)";
      case "risk_policy_would_deny":
        return "would deny (report-only)";
      default:
        return event.eventType.replace(/_/g, " ");
    }
  }

  reasonLabel(reason: RiskReason): string {
    const points = reason.points > 0 ? `+${reason.points}` : `${reason.points}`;
    return `${reason.code.replace(/_/g, " ")} ${points}`;
  }

  private describe(error: HttpErrorResponse): string {
    if (error.status === 403) return "Super-admin only.";
    const reason = (error.error as { reason?: string } | null)?.reason;
    if (error.status === 400) return `Invalid filter (${reason ?? "bad request"}).`;
    return `Request failed (HTTP ${error.status || "network"}).`;
  }
}
