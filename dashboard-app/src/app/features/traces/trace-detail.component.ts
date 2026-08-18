import {
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component,
  DestroyRef,
  OnDestroy,
  OnInit,
  effect,
  inject,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, RouterModule } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatDividerModule } from '@angular/material/divider';
import { BehaviorSubject, Subject } from 'rxjs';
import { takeUntil } from 'rxjs/operators';
import { TracesService } from '../../core/services/traces.service';
import { TraceDetail, TraceSpan } from '../../core/models/trace.model';
import {
  HisHopePageHeaderComponent,
  HisHopePageLayoutComponent,
  HisHopePageSectionComponent,
  HisHopeStateComponent,
  HisHopeResourceState,
  HisHopeStatusBadgeComponent,
  HisHopeTranslatePipe,
} from '@his-hope/frontend-foundation';

interface ExpandedSpan {
  [spanId: string]: boolean;
}

/** Theme-token color variables used for service identity in the waterfall. */
const SERVICE_COLORS = [
  'var(--color-primary)',
  'var(--color-info)',
  'var(--color-success)',
  'var(--color-warning)',
  'var(--color-danger)',
];

@Component({
  selector: 'app-trace-detail',
  standalone: true,
  imports: [
    CommonModule,
    RouterModule,
    MatButtonModule,
    MatIconModule,
    MatDividerModule,
    HisHopePageHeaderComponent,
    HisHopePageLayoutComponent,
    HisHopePageSectionComponent,
    HisHopeStateComponent,
    HisHopeStatusBadgeComponent,
    HisHopeTranslatePipe,
  ],
  template: `
    <hh-page-layout>
      <hh-page-header
        hhPageHeader
        [title]="'dashboard.traces.detailTitle' | hhTranslate: 'Trace Detail'"
        [subtitle]="trace ? trace.traceId : ''"
      >
        <button mat-stroked-button routerLink="/traces" class="back-btn">
          <mat-icon>arrow_back</mat-icon>
          {{ 'dashboard.common.back' | hhTranslate: 'Back' }}
        </button>
        <button
          mat-stroked-button
          (click)="refresh()"
          [disabled]="resource.loading()"
        >
          <mat-icon>refresh</mat-icon>
          {{ 'dashboard.common.refresh' | hhTranslate: 'Refresh' }}
        </button>
      </hh-page-header>

      @let loading = resource.loading();
      @let error = resource.error() ? resource.errorMessage() : '';

      <!-- Loading -->
      @if (loading && !trace) {
        <hh-state
          kind="loading"
          [message]="
            'dashboard.traces.loadingTrace' | hhTranslate: 'Loading trace...'
          "
        />
      }

      <!-- Error -->
      @if (error) {
        <hh-state kind="error" icon="error_outline" [message]="error">
          <button mat-raised-button color="primary" (click)="refresh()">
            {{ 'dashboard.common.retry' | hhTranslate: 'Retry' }}
          </button>
        </hh-state>
      }

      <!-- Trace detail -->
      @if (trace) {
        <hh-page-section
          [title]="'dashboard.traces.overview' | hhTranslate: 'Overview'"
        >
          <div class="header-grid">
            <div class="header-field">
              <span class="field-label">{{
                'dashboard.traces.traceId' | hhTranslate: 'Trace ID'
              }}</span>
              <code class="field-value mono">{{ trace.traceId }}</code>
            </div>
            <div class="header-field">
              <span class="field-label">{{
                'dashboard.traces.time' | hhTranslate: 'Time'
              }}</span>
              <span class="field-value">{{
                trace.startTime | date: 'dd/MM/yyyy HH:mm:ss'
              }}</span>
            </div>
            <div class="header-field">
              <span class="field-label">{{
                'dashboard.traces.totalDuration' | hhTranslate: 'Total Duration'
              }}</span>
              <span class="field-value"
                >{{ trace.durationMs | number }} ms</span
              >
            </div>
            <div class="header-field">
              <span class="field-label">{{
                'dashboard.traces.services' | hhTranslate: 'Services'
              }}</span>
              <span class="field-value">{{ trace.services.length }}</span>
            </div>
            <div class="header-field">
              <span class="field-label">{{
                'dashboard.traces.spans' | hhTranslate: 'Spans'
              }}</span>
              <span class="field-value">{{ trace.spanCount }}</span>
            </div>
            <div class="header-field">
              <span class="field-label">{{
                'dashboard.common.status' | hhTranslate: 'Status'
              }}</span>
              <span class="field-value">
                <hh-status-badge
                  [status]="trace.status"
                  [label]="trace.status"
                  [tone]="trace.status === 'Error' ? 'danger' : 'success'"
                />
              </span>
            </div>
          </div>
          <div class="service-chips">
            <span class="service-chip-label">{{
              'dashboard.traces.servicesLabel' | hhTranslate: 'Services:'
            }}</span>
            @for (svc of trace.services; track svc) {
              <span
                class="service-chip"
                [style.color]="getServiceColor(svc)"
                [style.background]="serviceChipBg(svc)"
              >
                {{ svc }}
              </span>
            }
          </div>
        </hh-page-section>

        <!-- Waterfall chart -->
        <hh-page-section
          [title]="
            'dashboard.traces.waterfallChart' | hhTranslate: 'Waterfall Chart'
          "
        >
          <div class="waterfall-container">
            <div class="waterfall-labels">
              <div class="waterfall-label-row header-row">
                <span class="label-service">{{
                  'dashboard.traces.service' | hhTranslate: 'Service'
                }}</span>
                <span class="label-operation">{{
                  'dashboard.traces.operation' | hhTranslate: 'Operation'
                }}</span>
                <span class="label-duration">{{
                  'dashboard.traces.duration' | hhTranslate: 'Duration'
                }}</span>
              </div>
              @for (span of trace.spans; track span.spanId) {
                <div
                  class="waterfall-label-row"
                  (click)="toggleSpan(span.spanId)"
                  (keydown.enter)="toggleSpan(span.spanId)"
                  tabindex="0"
                  role="button"
                  [attr.aria-expanded]="expanded[span.spanId]"
                  [class.expanded]="expanded[span.spanId]"
                >
                  <span
                    class="label-service"
                    [style.color]="getServiceColor(span.service)"
                  >
                    {{ span.service }}
                  </span>
                  <span class="label-operation">{{ span.name }}</span>
                  <span class="label-duration">{{ span.durationMs }}ms</span>
                  <mat-icon class="expand-icon">
                    {{ expanded[span.spanId] ? 'expand_less' : 'expand_more' }}
                  </mat-icon>
                </div>
              }
            </div>
            <div class="waterfall-bars">
              <div class="waterfall-time-axis">
                @for (tick of timeTicks; track tick) {
                  <span class="time-tick">{{ tick }}ms</span>
                }
              </div>
              @for (span of trace.spans; track span.spanId) {
                <div
                  class="waterfall-bar-row"
                  (click)="toggleSpan(span.spanId)"
                  (keydown.enter)="toggleSpan(span.spanId)"
                  tabindex="0"
                  role="button"
                  [attr.aria-expanded]="expanded[span.spanId]"
                  [class.expanded]="expanded[span.spanId]"
                >
                  <div class="waterfall-bar-track">
                    <div
                      class="waterfall-bar"
                      [style.left.%]="getSpanLeft(span)"
                      [style.width.%]="getSpanWidth(span)"
                      [style.background]="getServiceColor(span.service)"
                      [title]="span.name + ' - ' + span.durationMs + 'ms'"
                    ></div>
                  </div>
                </div>
              }
            </div>
          </div>
        </hh-page-section>

        <!-- Expanded span details -->
        @for (span of trace.spans; track span.spanId) {
          @if (expanded[span.spanId]) {
            <hh-page-section
              [title]="
                ('dashboard.traces.span' | hhTranslate: 'Span:') +
                ' ' +
                span.spanId
              "
              [subtitle]="span.service + ' — ' + span.name"
            >
              <div class="span-meta-grid">
                <div class="detail-field">
                  <span class="field-label">{{
                    'dashboard.traces.parentSpanId'
                      | hhTranslate: 'Parent Span ID'
                  }}</span>
                  <code class="field-value mono">{{
                    span.parentSpanId || '—'
                  }}</code>
                </div>
                <div class="detail-field">
                  <span class="field-label">{{
                    'dashboard.traces.start' | hhTranslate: 'Start'
                  }}</span>
                  <span class="field-value">{{
                    span.startTime | date: 'HH:mm:ss.SSS'
                  }}</span>
                </div>
                <div class="detail-field">
                  <span class="field-label">{{
                    'dashboard.traces.end' | hhTranslate: 'End'
                  }}</span>
                  <span class="field-value">{{
                    span.endTime | date: 'HH:mm:ss.SSS'
                  }}</span>
                </div>
                <div class="detail-field">
                  <span class="field-label">{{
                    'dashboard.common.status' | hhTranslate: 'Status'
                  }}</span>
                  <span class="field-value">{{ span.status }}</span>
                </div>
              </div>

              <mat-divider class="detail-divider"></mat-divider>

              <!-- Attributes / Tags -->
              @if (span.attributes && objectKeys(span.attributes).length > 0) {
                <div class="detail-section">
                  <h4 class="detail-section-title">
                    {{ 'dashboard.traces.tags' | hhTranslate: 'Tags' }}
                  </h4>
                  <div class="tags-grid">
                    @for (key of objectKeys(span.attributes); track key) {
                      <div class="tag-item">
                        <span class="tag-key">{{ key }}</span>
                        <span class="tag-value">{{
                          span.attributes[key]
                        }}</span>
                      </div>
                    }
                  </div>
                </div>
              }

              <!-- Events / Logs -->
              @if (span.events && span.events.length > 0) {
                <div class="detail-section">
                  <h4 class="detail-section-title">
                    {{
                      'dashboard.traces.eventsLogs'
                        | hhTranslate: 'Events & Logs'
                    }}
                  </h4>
                  <div class="events-list">
                    @for (evt of span.events; track evt.timestamp) {
                      <div class="event-item">
                        <div class="event-header">
                          <span class="event-name">{{ evt.name }}</span>
                          <span class="event-time">{{
                            evt.timestamp | date: 'HH:mm:ss.SSS'
                          }}</span>
                        </div>
                        @if (
                          evt.attributes &&
                          objectKeys(evt.attributes).length > 0
                        ) {
                          <div class="event-attributes">
                            @for (
                              key of objectKeys(evt.attributes);
                              track key
                            ) {
                              <div class="tag-item">
                                <span class="tag-key">{{ key }}</span>
                                <span class="tag-value">{{
                                  evt.attributes[key]
                                }}</span>
                              </div>
                            }
                          </div>
                        }
                      </div>
                    }
                  </div>
                </div>
              }
            </hh-page-section>
          }
        }
      }
    </hh-page-layout>
  `,
  styles: [
    `
      .back-btn {
        font-size: 13px;
      }
      .back-btn mat-icon {
        font-size: 18px;
        width: 18px;
        height: 18px;
      }

      .header-grid {
        display: grid;
        grid-template-columns: repeat(auto-fill, minmax(180px, 1fr));
        gap: 16px;
        margin-bottom: 16px;
      }
      .field-label {
        display: block;
        font-size: 11px;
        font-weight: var(--font-weight-semibold);
        text-transform: uppercase;
        letter-spacing: 0.04em;
        color: var(--text-muted);
        margin-bottom: 4px;
      }
      .field-value {
        font-size: 14px;
        color: var(--text-primary);
        font-weight: var(--font-weight-medium);
      }
      .field-value.mono {
        font-family: var(--font-mono);
        font-size: 12px;
        word-break: break-all;
      }
      .service-chips {
        display: flex;
        align-items: center;
        gap: 6px;
        flex-wrap: wrap;
      }
      .service-chip-label {
        font-size: 11px;
        font-weight: var(--font-weight-semibold);
        text-transform: uppercase;
        letter-spacing: 0.04em;
        color: var(--text-muted);
        margin-right: 4px;
      }
      .service-chip {
        display: inline-block;
        padding: 2px 10px;
        border-radius: var(--radius-badge);
        font-size: 12px;
        font-weight: var(--font-weight-medium);
      }

      /* Waterfall */
      .waterfall-container {
        display: flex;
        gap: 0;
        margin-top: 8px;
      }
      .waterfall-labels {
        flex: 0 0 320px;
        min-width: 0;
        border-right: 1px solid var(--border-default);
      }
      .waterfall-bars {
        flex: 1;
        overflow-x: auto;
        min-width: 0;
      }
      .waterfall-time-axis {
        display: flex;
        justify-content: space-between;
        padding: 8px 0;
        border-bottom: 1px solid var(--border-default);
        margin-bottom: 0;
      }
      .time-tick {
        font-size: 10px;
        color: var(--text-muted);
        font-family: var(--font-mono);
      }
      .waterfall-label-row,
      .waterfall-bar-row {
        display: flex;
        align-items: center;
        padding: 8px 12px;
        border-bottom: 1px solid var(--border-light);
        cursor: pointer;
        transition: background-color 150ms ease;
        min-height: 36px;
      }
      .waterfall-label-row:hover,
      .waterfall-bar-row:hover {
        background: var(--surface-hover);
      }
      .waterfall-label-row.header-row,
      .waterfall-bar-row:first-of-type {
        border-top: none;
      }
      .waterfall-label-row.expanded,
      .waterfall-bar-row.expanded {
        background: var(--surface-muted);
      }
      .waterfall-label-row {
        gap: 8px;
        font-size: 12px;
      }
      .label-service {
        flex: 0 0 100px;
        font-weight: var(--font-weight-semibold);
        color: var(--text-primary);
        white-space: nowrap;
        overflow: hidden;
        text-overflow: ellipsis;
      }
      .label-operation {
        flex: 1;
        color: var(--text-secondary);
        white-space: nowrap;
        overflow: hidden;
        text-overflow: ellipsis;
      }
      .label-duration {
        flex: 0 0 60px;
        text-align: right;
        font-family: var(--font-mono);
        font-size: 11px;
        color: var(--text-primary);
      }
      .expand-icon {
        font-size: 16px;
        width: 16px;
        height: 16px;
        color: var(--text-muted);
        flex-shrink: 0;
      }
      .waterfall-bar-track {
        width: 100%;
        height: 16px;
        position: relative;
        background: var(--surface-muted);
        border-radius: 3px;
        overflow: hidden;
      }
      .waterfall-bar {
        position: absolute;
        height: 100%;
        border-radius: 3px;
        min-width: 2px;
        transition: opacity 150ms ease;
      }
      .waterfall-bar:hover {
        opacity: 0.85;
      }

      /* Span detail */
      .span-meta-grid {
        display: grid;
        grid-template-columns: repeat(auto-fill, minmax(180px, 1fr));
        gap: 12px;
        margin-top: 8px;
      }
      .detail-divider {
        margin: 16px 0 !important;
      }
      .detail-section {
        margin-bottom: 16px;
      }
      .detail-section:last-child {
        margin-bottom: 0;
      }
      .detail-section-title {
        font-size: 13px;
        font-weight: var(--font-weight-semibold);
        color: var(--text-primary);
        margin: 0 0 8px 0;
      }
      .tags-grid {
        display: flex;
        flex-wrap: wrap;
        gap: 6px;
      }
      .tag-item {
        display: inline-flex;
        gap: 6px;
        padding: 3px 10px;
        background: var(--surface-muted);
        border: 1px solid var(--border-default);
        border-radius: var(--radius-input);
        font-size: 12px;
      }
      .tag-key {
        font-weight: var(--font-weight-semibold);
        color: var(--text-secondary);
      }
      .tag-value {
        color: var(--text-primary);
        font-family: var(--font-mono);
        font-size: 11px;
      }
      .events-list {
        display: flex;
        flex-direction: column;
        gap: 8px;
      }
      .event-item {
        padding: 8px 12px;
        background: var(--surface-muted);
        border: 1px solid var(--border-default);
        border-radius: var(--radius-input);
      }
      .event-header {
        display: flex;
        justify-content: space-between;
        align-items: center;
        margin-bottom: 6px;
      }
      .event-name {
        font-size: 13px;
        font-weight: var(--font-weight-semibold);
        color: var(--text-primary);
      }
      .event-time {
        font-size: 11px;
        color: var(--text-muted);
        font-family: var(--font-mono);
      }
      .event-attributes {
        display: flex;
        flex-wrap: wrap;
        gap: 4px;
      }
    `,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class TraceDetailComponent implements OnInit, OnDestroy {
  private readonly route = inject(ActivatedRoute);
  private readonly tracesService = inject(TracesService);
  private readonly cdr = inject(ChangeDetectorRef);

  private readonly destroy$ = new Subject<void>();
  private readonly traceId$ = new BehaviorSubject<string>('');
  private readonly refreshTrigger = new BehaviorSubject<void>(undefined);

  private readonly destroyRef = inject(DestroyRef);

  readonly resource = new HisHopeResourceState<TraceDetail>(this.destroyRef);

  trace: TraceDetail | null = null;
  expanded: ExpandedSpan = {};
  timeTicks: number[] = [];

  private maxDuration = 0;

  constructor() {
    effect(() => {
      const trace = this.resource.data();
      if (!trace) return;
      this.trace = trace;
      this.expanded = {};
      this.computeTimeTicks(trace);
      this.cdr.markForCheck();
    });
  }

  ngOnInit(): void {
    this.route.paramMap.pipe(takeUntil(this.destroy$)).subscribe((params) => {
      const id = params.get('traceId') ?? '';
      this.traceId$.next(id);
      this.refresh();
    });
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  refresh(): void {
    const traceId = this.traceId$.value;
    if (!traceId) return;

    this.resource.load(
      this.tracesService.getById(traceId).pipe(takeUntil(this.destroy$)),
    );
  }

  private computeTimeTicks(trace: TraceDetail): void {
    if (!trace.spans || trace.spans.length === 0) {
      this.timeTicks = [0, trace.durationMs];
      this.maxDuration = trace.durationMs;
      return;
    }
    this.maxDuration = 0;
    for (const span of trace.spans) {
      if (span.durationMs > this.maxDuration) {
        this.maxDuration = span.durationMs;
      }
    }
    // Ensure at least trace.durationMs
    if (trace.durationMs > this.maxDuration) {
      this.maxDuration = trace.durationMs;
    }

    // Generate 5 ticks
    const ticks: number[] = [];
    for (let i = 0; i <= 4; i++) {
      ticks.push(Math.round((this.maxDuration * i) / 4));
    }
    this.timeTicks = ticks;
  }

  getServiceColor(service: string): string {
    let hash = 0;
    for (let i = 0; i < service.length; i++) {
      hash = (hash << 5) - hash + service.charCodeAt(i);
      hash |= 0;
    }
    const idx = Math.abs(hash) % SERVICE_COLORS.length;
    return SERVICE_COLORS[idx];
  }

  serviceChipBg(service: string): string {
    return `color-mix(in srgb, ${this.getServiceColor(service)} 12%, transparent)`;
  }

  getSpanLeft(span: TraceSpan): number {
    if (!this.trace || this.maxDuration === 0) return 0;
    const spanStart = new Date(span.startTime).getTime();
    const traceStart = new Date(this.trace.startTime).getTime();
    const offset = Math.max(0, spanStart - traceStart);
    return (offset / this.maxDuration) * 100;
  }

  getSpanWidth(span: TraceSpan): number {
    if (this.maxDuration === 0) return 0;
    return (span.durationMs / this.maxDuration) * 100;
  }

  toggleSpan(spanId: string): void {
    this.expanded[spanId] = !this.expanded[spanId];
    this.expanded = { ...this.expanded };
  }

  objectKeys(obj: Record<string, string>): string[] {
    return Object.keys(obj);
  }
}
