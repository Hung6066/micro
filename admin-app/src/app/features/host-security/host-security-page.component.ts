import {
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component,
  DestroyRef,
  OnInit,
  inject,
} from "@angular/core";
import { CommonModule } from "@angular/common";
import { HttpErrorResponse } from "@angular/common/http";
import { takeUntilDestroyed } from "@angular/core/rxjs-interop";
import { FormControl, FormGroup, FormsModule, ReactiveFormsModule } from "@angular/forms";
import { MatCardModule } from "@angular/material/card";
import { MatCheckboxModule } from "@angular/material/checkbox";
import { MatFormFieldModule } from "@angular/material/form-field";
import { MatInputModule } from "@angular/material/input";
import { MatSelectModule } from "@angular/material/select";
import { MatSlideToggleModule } from "@angular/material/slide-toggle";
import { Subscription, forkJoin, timer } from "rxjs";
import {
  HisHopeActionButtonComponent,
  HisHopePageHeaderComponent,
  HisHopePageLayoutComponent,
} from "@his-hope/frontend-foundation/ui";
import { HisHopeTranslatePipe } from "@his-hope/frontend-foundation/i18n";
import {
  HostIntegrityStatus,
  HostSecurityApiService,
  HostSecurityEvent,
  HostSecurityEventsPage,
  HostSecurityQuery,
} from "../../core/services/host-security-api.service";

/** Poll interval while "Live" is on (and the tab is visible). */
export const LIVE_INTERVAL_MS = 15_000;
export const RANGE_HOURS = [1, 6, 24, 24 * 7] as const;
export const CATEGORIES = ["integrity", "sudo", "login", "runtime"] as const;

/**
 * Host and workload security events (super-admin, server-enforced): config integrity drift,
 * sudo/su, console logins and Falco alerts with severity, host and acknowledgement state,
 * refreshed every 15 s. Acknowledging a drift covers every repeat of the same drift.
 */
@Component({
  selector: "app-host-security-page",
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    CommonModule,
    FormsModule,
    ReactiveFormsModule,
    MatCardModule,
    MatCheckboxModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatSlideToggleModule,
    HisHopeActionButtonComponent,
    HisHopePageHeaderComponent,
    HisHopePageLayoutComponent,
    HisHopeTranslatePipe,
  ],
  template: `
    <hh-page-layout>
      <hh-page-header
        hhPageHeader
        [title]="'admin.hostSecurity' | hhTranslate: 'Security'"
        [subtitle]="
          'admin.hostSecuritySubtitle'
            | hhTranslate
              : 'Config drift, sudo, console logins and Falco alerts on the OpenShip hosts, live.'
        "
      >
        <mat-slide-toggle [ngModel]="live" (ngModelChange)="setLive($event)">
          {{ "admin.hostSecurityLive" | hhTranslate: "Live" }}
        </mat-slide-toggle>
        <hh-action-button
          [disabled]="loading"
          (pressed)="reload()"
          kind="secondary"
          icon="refresh"
          [label]="'admin.refresh' | hhTranslate: 'Refresh'"
        />
      </hh-page-header>

      @if (!configured) {
        <p class="warning">
          {{ "admin.hostSecurityNotConfigured" | hhTranslate: "The Loki source is not configured on this IdentityService." }}
        </p>
      }
      @if (message) {
        <p class="error" role="alert">{{ message }}</p>
      }
      @if (page?.unavailableSources?.length) {
        <p class="warning">
          {{ "admin.hostSecurityPartial" | hhTranslate: "Some sources did not answer:" }}
          {{ page!.unavailableSources.join(", ") }}
        </p>
      }

      <section class="cards" aria-live="polite">
        <mat-card class="sev-critical"><strong>{{ page?.unacknowledged?.critical ?? 0 }}</strong><span>{{ "admin.hostSecurityOpenCritical" | hhTranslate: "Open critical" }}</span></mat-card>
        <mat-card class="sev-warning"><strong>{{ page?.unacknowledged?.warning ?? 0 }}</strong><span>{{ "admin.hostSecurityOpenWarning" | hhTranslate: "Open warning" }}</span></mat-card>
        <mat-card><strong>{{ hosts.length }}</strong><span>{{ "admin.hostSecurityHostsReporting" | hhTranslate: "Hosts reporting" }}</span></mat-card>
        <mat-card [class.sev-critical]="driftedHosts > 0"><strong>{{ driftedHosts }}</strong><span>{{ "admin.hostSecurityHostsDrifted" | hhTranslate: "Hosts with drift" }}</span></mat-card>
        <mat-card [class.sev-warning]="staleHosts > 0"><strong>{{ staleHosts }}</strong><span>{{ "admin.hostSecurityHostsStale" | hhTranslate: "Hosts silent > 20 min" }}</span></mat-card>
      </section>

      <mat-card class="hosts">
        <mat-card-header><mat-card-title>{{ "admin.hostSecurityHosts" | hhTranslate: "Integrity check per host" }}</mat-card-title></mat-card-header>
        <mat-card-content>
          <div class="host-grid">
            @for (host of hosts; track host.host) {
              <button type="button" class="host" [class]="hostClass(host)" (click)="filterHost(host.host)"
                [attr.aria-label]="host.host + ' ' + hostLabel(host)">
                <strong>{{ host.host }}</strong>
                <span>{{ hostLabel(host) }}</span>
                <small>{{ host.lastReportAt | date: "HH:mm:ss" }}</small>
              </button>
            } @empty {
              <span class="muted">{{ "admin.hostSecurityNoHosts" | hhTranslate: "No integrity reports in the last 6 hours." }}</span>
            }
          </div>
        </mat-card-content>
      </mat-card>

      <form class="filters" [formGroup]="filters" (ngSubmit)="reload()">
        <mat-form-field appearance="outline">
          <mat-label>{{ "admin.infraAuditRange" | hhTranslate: "Time range" }}</mat-label>
          <mat-select formControlName="hours">
            @for (hours of rangeHours; track hours) {
              <mat-option [value]="hours">{{ hours < 24 ? hours + " h" : hours / 24 + " d" }}</mat-option>
            }
          </mat-select>
        </mat-form-field>
        <mat-form-field appearance="outline">
          <mat-label>{{ "admin.hostSecurityMinSeverity" | hhTranslate: "Minimum severity" }}</mat-label>
          <mat-select formControlName="minSeverity">
            <mat-option value="info">info</mat-option>
            <mat-option value="warning">warning</mat-option>
            <mat-option value="critical">critical</mat-option>
          </mat-select>
        </mat-form-field>
        <mat-form-field appearance="outline">
          <mat-label>{{ "admin.hostSecurityCategories" | hhTranslate: "Categories" }}</mat-label>
          <mat-select formControlName="categories" multiple>
            @for (category of categories; track category) {
              <mat-option [value]="category">{{ category }}</mat-option>
            }
          </mat-select>
        </mat-form-field>
        <mat-form-field appearance="outline">
          <mat-label>{{ "admin.hostSecurityHost" | hhTranslate: "Host" }}</mat-label>
          <input matInput formControlName="host" maxlength="63" />
        </mat-form-field>
        <mat-form-field appearance="outline">
          <mat-label>{{ "admin.infraAuditText" | hhTranslate: "Contains" }}</mat-label>
          <input matInput formControlName="text" maxlength="200" />
        </mat-form-field>
        <mat-checkbox formControlName="unacknowledgedOnly">{{ "admin.hostSecurityOpenOnly" | hhTranslate: "Open only" }}</mat-checkbox>
        <hh-action-button type="submit" kind="primary" icon="search" [label]="'admin.apply' | hhTranslate: 'Apply'" />
      </form>

      @if (selected.size > 0) {
        <div class="ack-bar">
          <span>{{ selected.size }} {{ "admin.selected" | hhTranslate: "selected" }}</span>
          <mat-form-field appearance="outline" class="note">
            <mat-label>{{ "admin.hostSecurityAckNote" | hhTranslate: "Note (why this is expected)" }}</mat-label>
            <input matInput [(ngModel)]="ackNote" [ngModelOptions]="{ standalone: true }" maxlength="500" />
          </mat-form-field>
          <hh-action-button [disabled]="acking" (pressed)="acknowledgeSelected()" kind="primary" icon="done_all"
            [label]="'admin.hostSecurityAcknowledge' | hhTranslate: 'Acknowledge'" />
        </div>
      }

      <div class="table-wrap">
        <table class="events" [attr.aria-busy]="loading">
          <thead>
            <tr>
              <th scope="col"><span class="sr-only">{{ "admin.select" | hhTranslate: "Select" }}</span></th>
              <th scope="col">{{ "admin.time" | hhTranslate: "Time" }}</th>
              <th scope="col">{{ "admin.hostSecuritySeverity" | hhTranslate: "Severity" }}</th>
              <th scope="col">{{ "admin.hostSecurityHost" | hhTranslate: "Host" }}</th>
              <th scope="col">{{ "admin.hostSecurityEvent" | hhTranslate: "Event" }}</th>
              <th scope="col">{{ "admin.detail" | hhTranslate: "Detail" }}</th>
              <th scope="col">{{ "admin.infraAuditActor" | hhTranslate: "Person" }}</th>
              <th scope="col">{{ "admin.hostSecurityAckState" | hhTranslate: "Acknowledged" }}</th>
            </tr>
          </thead>
          <tbody>
            @for (event of page?.items ?? []; track event.id) {
              <tr [class]="'row-' + event.severity" [class.acked]="!!event.ack">
                <td>
                  @if (!event.ack) {
                    <mat-checkbox [checked]="selected.has(event.ackKey)" (change)="toggle(event)"
                      [attr.aria-label]="'select ' + event.securityEvent + ' ' + event.host" />
                  }
                </td>
                <td class="nowrap">{{ event.timestamp | date: "yyyy-MM-dd HH:mm:ss" }}</td>
                <td><span class="chip" [class]="'chip-' + event.severity">{{ event.severity }}</span></td>
                <td class="nowrap">{{ event.host }}</td>
                <td><span class="cat">{{ event.category }}</span> {{ event.securityEvent }}</td>
                <td>
                  <div>{{ event.summary }}</div>
                  @if (event.changes.length) {
                    <details>
                      <summary>{{ event.changes.length }} {{ "admin.hostSecurityChanges" | hhTranslate: "changes" }}</summary>
                      <ul>
                        @for (change of event.changes; track change.path + change.change) {
                          <li><span class="chip" [class]="'chip-' + change.severity">{{ change.category }}</span> {{ change.change }} <code>{{ change.path }}</code>
                            @if (change.detail) { <code class="muted">{{ change.detail }}</code> }</li>
                        }
                      </ul>
                    </details>
                  } @else if (event.detail) {
                    <details><summary>{{ "admin.more" | hhTranslate: "more" }}</summary><code>{{ event.detail }}</code></details>
                  }
                </td>
                <td class="actor">{{ event.actor }}</td>
                <td>
                  @if (event.ack) {
                    <span class="muted" [title]="event.ack.note ?? ''">{{ event.ack.ackedBy }} · {{ event.ack.ackedAt | date: "MM-dd HH:mm" }}</span>
                  } @else if (event.severity !== "info") {
                    <span class="open">{{ "admin.hostSecurityOpen" | hhTranslate: "open" }}</span>
                  }
                </td>
              </tr>
            } @empty {
              <tr><td colspan="8" class="muted">{{ loading ? "…" : ("admin.hostSecurityNoEvents" | hhTranslate: "No security events in this range.") }}</td></tr>
            }
          </tbody>
        </table>
      </div>
      @if (page?.truncated) {
        <p class="muted">{{ "admin.hostSecurityTruncated" | hhTranslate: "Only the newest events are shown; narrow the filters to see older ones." }}</p>
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
      .sev-critical strong { color: var(--color-danger); }
      .sev-warning strong { color: var(--color-warning); }
      .hosts { margin-bottom: var(--space-md); }
      .host-grid { display: grid; grid-template-columns: repeat(auto-fill, minmax(10rem, 1fr)); gap: var(--space-sm); }
      .host { display: flex; flex-direction: column; align-items: flex-start; gap: var(--space-hairline); padding: var(--space-sm); border-radius: var(--radius-md);
        border: var(--space-hairline) solid var(--border-subtle); background: transparent; cursor: pointer; text-align: left; font: inherit; color: inherit; }
      .host.ok { border-left: var(--space-xs) solid var(--color-success); }
      .host.drift-critical { border-left: var(--space-xs) solid var(--color-danger); }
      .host.drift-warning, .host.stale { border-left: var(--space-xs) solid var(--color-warning); }
      .filters { display: flex; flex-wrap: wrap; gap: var(--space-md); align-items: center; }
      .ack-bar { display: flex; gap: var(--space-md); align-items: center; flex-wrap: wrap; margin: var(--space-sm) 0; }
      .ack-bar .note { min-width: 20rem; }
      .table-wrap { overflow-x: auto; }
      table.events { width: 100%; border-collapse: collapse; font-size: var(--font-size-body); }
      table.events th, table.events td { text-align: left; vertical-align: top; padding: var(--space-xs) var(--space-sm); border-bottom: var(--space-hairline) solid var(--border-subtle); }
      tr.acked { opacity: 0.6; }
      .nowrap { white-space: nowrap; }
      .actor { max-width: 16rem; overflow-wrap: anywhere; }
      .chip { display: inline-block; padding: 0 var(--space-xs); border-radius: var(--radius-chip); font-size: var(--font-size-caption); font-weight: var(--font-weight-semibold); }
      .chip-critical { background: var(--color-danger); color: var(--color-on-danger); }
      .chip-warning { background: var(--color-warning); color: var(--color-on-danger); }
      .chip-info { background: var(--border-subtle); }
      .cat { color: var(--text-secondary); font-size: var(--font-size-caption); margin-right: var(--space-xs); }
      .open { color: var(--color-danger); font-weight: 600; }
      code { overflow-wrap: anywhere; }
      .warning, .error { margin: var(--space-sm) 0; }
      .error { color: var(--color-danger); }
      .sr-only { position: absolute; width: var(--space-hairline); height: var(--space-hairline); overflow: hidden; clip: rect(0 0 0 0); }
    `,
  ],
})
export class HostSecurityPageComponent implements OnInit {
  private readonly api = inject(HostSecurityApiService);
  private readonly cdr = inject(ChangeDetectorRef);
  private readonly destroyRef = inject(DestroyRef);

  readonly rangeHours = RANGE_HOURS;
  readonly categories = CATEGORIES;
  readonly filters = new FormGroup({
    hours: new FormControl<number>(24, { nonNullable: true }),
    minSeverity: new FormControl<string>("warning", { nonNullable: true }),
    categories: new FormControl<string[]>([], { nonNullable: true }),
    host: new FormControl<string>("", { nonNullable: true }),
    text: new FormControl<string>("", { nonNullable: true }),
    unacknowledgedOnly: new FormControl<boolean>(false, { nonNullable: true }),
  });

  configured = true;
  live = true;
  loading = false;
  acking = false;
  message = "";
  page: HostSecurityEventsPage | null = null;
  hosts: HostIntegrityStatus[] = [];
  lastUpdated: Date | null = null;
  selected = new Set<string>();
  ackNote = "";
  private poll: Subscription | null = null;

  get driftedHosts(): number {
    return this.hosts.filter((host) => host.lastEvent === "integrity_drift").length;
  }

  get staleHosts(): number {
    return this.hosts.filter((host) => host.stale).length;
  }

  ngOnInit(): void {
    this.api.getMeta().pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: (meta) => {
        this.configured = meta.configured;
        this.cdr.markForCheck();
      },
      error: (error: HttpErrorResponse) => {
        this.message = this.describe(error);
        this.cdr.markForCheck();
      },
    });
    this.setLive(true);
  }

  setLive(on: boolean): void {
    this.live = on;
    this.poll?.unsubscribe();
    this.poll = null;
    if (on) {
      this.poll = timer(0, LIVE_INTERVAL_MS)
        .pipe(takeUntilDestroyed(this.destroyRef))
        .subscribe(() => {
          if (typeof document === "undefined" || document.visibilityState !== "hidden") this.reload();
        });
    } else {
      this.reload();
    }
  }

  query(now = new Date()): HostSecurityQuery {
    const value = this.filters.getRawValue();
    return {
      from: new Date(now.getTime() - value.hours * 3_600_000).toISOString(),
      to: now.toISOString(),
      categories: value.categories,
      minSeverity: value.minSeverity,
      host: value.host,
      text: value.text,
      unacknowledgedOnly: value.unacknowledgedOnly,
      limit: 300,
    };
  }

  reload(): void {
    if (this.loading) return;
    this.loading = true;
    this.cdr.markForCheck();
    forkJoin({ events: this.api.getEvents(this.query()), hosts: this.api.getHosts() }).subscribe({
      next: ({ events, hosts }) => {
        this.page = events;
        this.hosts = hosts.items;
        this.message = "";
        this.lastUpdated = new Date();
        const open = new Set(events.items.filter((item) => !item.ack).map((item) => item.ackKey));
        this.selected = new Set([...this.selected].filter((key) => open.has(key)));
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

  toggle(event: HostSecurityEvent): void {
    const next = new Set(this.selected);
    if (next.has(event.ackKey)) next.delete(event.ackKey);
    else next.add(event.ackKey);
    this.selected = next;
  }

  acknowledgeSelected(): void {
    const events = (this.page?.items ?? []).filter((item) => this.selected.has(item.ackKey) && !item.ack);
    if (!events.length) return;
    this.acking = true;
    this.api.acknowledge(events, this.ackNote).subscribe({
      next: () => {
        this.acking = false;
        this.selected = new Set();
        this.ackNote = "";
        this.loading = false;
        this.reload();
      },
      error: (error: HttpErrorResponse) => {
        this.acking = false;
        this.message = this.describe(error);
        this.cdr.markForCheck();
      },
    });
  }

  filterHost(host: string): void {
    this.filters.controls.host.setValue(host);
    this.reload();
  }

  hostClass(host: HostIntegrityStatus): string {
    if (host.stale) return "host stale";
    if (host.lastEvent === "integrity_drift") return `host drift-${host.severity}`;
    return "host ok";
  }

  hostLabel(host: HostIntegrityStatus): string {
    if (host.stale) return "silent";
    if (host.lastEvent === "integrity_drift") return `${host.driftCount} drift (${host.severity})`;
    if (host.lastEvent === "integrity_ok") return "ok";
    return host.lastEvent.replace("integrity_", "").replace(/_/g, " ");
  }

  private describe(error: HttpErrorResponse): string {
    if (error.status === 403) return "Super-admin only.";
    const reason = (error.error as { reason?: string; errorCode?: string } | null)?.reason;
    if (error.status === 503) return `Security events are unavailable (${reason ?? "upstream"}).`;
    if (error.status === 400) return `Invalid filter (${reason ?? "bad request"}).`;
    return `Request failed (HTTP ${error.status || "network"}).`;
  }
}
