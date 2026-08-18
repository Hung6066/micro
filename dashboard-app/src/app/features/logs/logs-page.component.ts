import {
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component,
  DestroyRef,
  OnInit,
  effect,
  inject,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterModule } from '@angular/router';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatTabsModule } from '@angular/material/tabs';
import { MatAutocompleteModule } from '@angular/material/autocomplete';

import {
  HisHopeDataTableCellDirective,
  HisHopeDataTableColumn,
  HisHopeDataTableComponent,
  HisHopeDataTableDetailDirective,
  HisHopeFilterToolbarComponent,
  HisHopePageHeaderComponent,
  HisHopePageLayoutComponent,
  HisHopePageQuery,
  HisHopeResourceState,
  HisHopeStatusBadgeComponent,
  HisHopeStatusTone,
  HisHopeTranslatePipe,
} from '@his-hope/frontend-foundation';
import { BehaviorSubject, interval, merge } from 'rxjs';
import { debounceTime, map } from 'rxjs/operators';
import { LogsService } from '../../core/services/logs.service';
import { LogEntry } from '../../core/models/log-entry.model';
import {
  TimeRangePickerComponent,
  TimeRange,
} from '../../shared/time-range-picker/time-range-picker.component';
import { LogStreamViewComponent } from './log-stream-view.component';

interface LevelOption {
  value: string;
  label: string;
}

const PAGE_SIZE = 20;

const LEVELS: LevelOption[] = [
  { value: 'Error', label: 'Error' },
  { value: 'Warning', label: 'Warn' },
  { value: 'Information', label: 'Info' },
  { value: 'Debug', label: 'Debug' },
];

const ALL_SERVICES = [
  'identity-service',
  'patient-service',
  'appointment-service',
  'clinical-service',
  'lab-service',
  'billing-service',
  'pharmacy-service',
];

const SERVICE_TONES: HisHopeStatusTone[] = [
  'neutral',
  'info',
  'success',
  'warning',
];

/** Map a log level to a status-badge tone. */
function levelToneFor(level: string): HisHopeStatusTone {
  const l = level.toLowerCase();
  if (l === 'error' || l === 'critical' || l === 'fatal') return 'danger';
  if (l === 'warning' || l === 'warn') return 'warning';
  if (l === 'information' || l === 'info') return 'info';
  return 'neutral';
}

/** Deterministically map a service name to a status-badge tone. */
function serviceToneFor(service: string): HisHopeStatusTone {
  let hash = 0;
  for (let i = 0; i < service.length; i++) {
    hash = (hash << 5) - hash + service.charCodeAt(i);
    hash |= 0;
  }
  return SERVICE_TONES[Math.abs(hash) % SERVICE_TONES.length];
}

@Component({
  selector: 'app-logs-page',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    RouterModule,
    MatCardModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatButtonModule,
    MatIconModule,
    MatTabsModule,
    MatAutocompleteModule,
    TimeRangePickerComponent,
    LogStreamViewComponent,
    HisHopeDataTableCellDirective,
    HisHopeDataTableComponent,
    HisHopeDataTableDetailDirective,
    HisHopeFilterToolbarComponent,
    HisHopePageHeaderComponent,
    HisHopePageLayoutComponent,
    HisHopeStatusBadgeComponent,
    HisHopeTranslatePipe,
  ],
  template: `
    <hh-page-layout>
      <hh-page-header
        hhPageHeader
        [title]="'dashboard.logs.pageTitle' | hhTranslate: 'System Logs'"
        [subtitle]="
          'dashboard.logs.pageSubtitle'
            | hhTranslate: 'Centralized log viewer across all services'
        "
      >
        <button
          mat-stroked-button
          (click)="refresh()"
          [disabled]="resource.loading()"
        >
          <mat-icon>refresh</mat-icon>
          {{ 'dashboard.logs.refresh' | hhTranslate: 'Refresh' }}
        </button>
      </hh-page-header>

      <mat-tab-group (selectedIndexChange)="onTabChange()" class="logs-tabs">
        <!-- ═══════════════ Search Tab ═══════════════ -->
        <mat-tab [label]="'dashboard.logs.tabSearch' | hhTranslate: 'Search'">
          <ng-template matTabContent>
            <hh-filter-toolbar label="Log filters" [resultCount]="totalCount">
              <!-- Time range -->
              <div class="filter-group time-range-group">
                <span class="filter-label">{{
                  'dashboard.logs.timeRange' | hhTranslate: 'Time range'
                }}</span>
                <app-time-range-picker
                  (rangeChange)="onTimeRangeChange($event)"
                ></app-time-range-picker>
              </div>

              <!-- Service autocomplete -->
              <mat-form-field
                appearance="outline"
                subscriptSizing="dynamic"
                class="service-filter"
              >
                <mat-label>{{
                  'dashboard.logs.service' | hhTranslate: 'Service'
                }}</mat-label>
                <input
                  matInput
                  [matAutocomplete]="autoService"
                  [(ngModel)]="serviceInput"
                  (ngModelChange)="onServiceInputChange()"
                  [placeholder]="
                    'dashboard.logs.allServices' | hhTranslate: 'All services'
                  "
                />
                <mat-icon matSuffix>search</mat-icon>
                <mat-autocomplete
                  #autoService="matAutocomplete"
                  (optionSelected)="onServiceSelected($event.option.value)"
                >
                  <mat-option value="">{{
                    'dashboard.logs.allServices' | hhTranslate: 'All services'
                  }}</mat-option>
                  @for (svc of filteredServices; track svc) {
                    <mat-option [value]="svc">{{ svc }}</mat-option>
                  }
                </mat-autocomplete>
              </mat-form-field>

              <!-- Full-text search -->
              <mat-form-field
                appearance="outline"
                subscriptSizing="dynamic"
                class="search-field"
              >
                <mat-label>{{
                  'dashboard.logs.fullTextSearch'
                    | hhTranslate: 'Full-text search'
                }}</mat-label>
                <input
                  matInput
                  [(ngModel)]="searchQuery"
                  (ngModelChange)="onSearchChange()"
                  [placeholder]="
                    'dashboard.logs.searchPlaceholder'
                      | hhTranslate: 'Keywords, trace ID, message...'
                  "
                />
                @if (searchQuery) {
                  <button
                    matSuffix
                    mat-icon-button
                    (click)="clearSearch()"
                    type="button"
                  >
                    <mat-icon>close</mat-icon>
                  </button>
                }
              </mat-form-field>

              <!-- Level chips -->
              <div class="level-filter-group">
                <span class="filter-label">{{
                  'dashboard.logs.level' | hhTranslate: 'Level'
                }}</span>
                <div class="level-chips">
                  @for (level of levelOptions; track level.value) {
                    <button
                      class="level-chip"
                      [class.selected]="selectedLevels.includes(level.value)"
                      (click)="toggleLevel(level.value)"
                    >
                      {{ level.label }}
                    </button>
                  }
                </div>
              </div>

              <button
                mat-raised-button
                color="primary"
                (click)="search()"
                class="search-btn"
              >
                <mat-icon>search</mat-icon>
                {{ 'dashboard.logs.searchBtn' | hhTranslate: 'Search' }}
              </button>
            </hh-filter-toolbar>

            <!-- Query summary bar -->
            @if (querySummary) {
              <div class="query-summary">
                <span class="query-summary-text">{{ querySummary }}</span>
                <button
                  mat-icon-button
                  size="small"
                  (click)="clearAllFilters()"
                  [title]="
                    'dashboard.logs.clearFilters'
                      | hhTranslate: 'Clear all filters'
                  "
                >
                  <mat-icon>close</mat-icon>
                </button>
              </div>
            }

            <!-- Results table -->
            @let loading = resource.loading();
            @let error = resource.error() ? 'dashboard.logs.loadFailed' : '';
            <hh-data-table
              label="System logs"
              [columns]="columns"
              [rows]="tableRows"
              [loading]="loading"
              [error]="error"
              [empty]="!loading && !error && logs.length === 0"
              mode="server"
              [pageSize]="20"
              [totalItems]="totalCount"
              [query]="query"
              [urlSync]="false"
              [searchable]="false"
              [expandedRowKeys]="expandedRowKeys"
              emptyMessage="No logs found."
              (queryChange)="onQueryChange($event)"
              (rowExpandChange)="onRowExpandChange($event)"
              (retry)="refresh()"
            >
              <ng-template hhDataTableCell="timestamp" let-row>
                <span class="cell-timestamp">{{
                  logOf(row).timestamp | date: 'dd/MM HH:mm:ss'
                }}</span>
              </ng-template>
              <ng-template hhDataTableCell="level" let-row>
                <hh-status-badge
                  [status]="logOf(row).level"
                  [label]="logOf(row).level"
                  [tone]="levelTone(logOf(row).level)"
                />
              </ng-template>
              <ng-template hhDataTableCell="service" let-row>
                <hh-status-badge
                  [status]="logOf(row).service"
                  [label]="logOf(row).service"
                  [tone]="serviceTone(logOf(row).service)"
                />
              </ng-template>
              <ng-template hhDataTableCell="message" let-row>
                <span class="message-text">{{ logOf(row).message }}</span>
                @if (logOf(row).traceId) {
                  <a
                    class="trace-link"
                    [routerLink]="['/traces', logOf(row).traceId!]"
                    (click)="$event.stopPropagation()"
                  >
                    [{{ logOf(row).traceId! | slice: 0 : 8 }}...]
                  </a>
                }
              </ng-template>
              <ng-template hhDataTableDetail let-row>
                @let log = logOf(row);
                <div class="expanded-detail">
                  @if (log.traceId) {
                    <div class="detail-field">
                      <span class="detail-field-label">{{
                        'dashboard.logs.traceId' | hhTranslate: 'Trace ID'
                      }}</span>
                      <a
                        class="detail-field-value trace-link-inline"
                        [routerLink]="['/traces', log.traceId!]"
                      >
                        {{ log.traceId }}
                      </a>
                    </div>
                  }
                  @if (log.spanId) {
                    <div class="detail-field">
                      <span class="detail-field-label">{{
                        'dashboard.logs.spanId' | hhTranslate: 'Span ID'
                      }}</span>
                      <code class="detail-field-value">{{ log.spanId }}</code>
                    </div>
                  }
                  @if (log.exception) {
                    <div class="detail-field">
                      <span class="detail-field-label">{{
                        'dashboard.logs.exception' | hhTranslate: 'Exception'
                      }}</span>
                      <pre class="detail-field-value exception">{{
                        log.exception
                      }}</pre>
                    </div>
                  }
                  @if (log.properties) {
                    <div class="detail-field">
                      <span class="detail-field-label">{{
                        'dashboard.logs.properties'
                          | hhTranslate: 'Properties (JSON)'
                      }}</span>
                      <pre class="detail-field-value json">{{
                        log.properties | json
                      }}</pre>
                    </div>
                  }
                </div>
              </ng-template>
            </hh-data-table>
          </ng-template>
        </mat-tab>

        <!-- ═══════════════ Stream Tab ═══════════════ -->
        <mat-tab [label]="'dashboard.logs.tabStream' | hhTranslate: 'Stream'">
          <ng-template matTabContent>
            <mat-card class="stream-card">
              <mat-card-content class="stream-card-content">
                <app-log-stream-view
                  [service]="selectedService"
                  [level]="selectedLevels.length === 1 ? selectedLevels[0] : ''"
                >
                </app-log-stream-view>
              </mat-card-content>
            </mat-card>
          </ng-template>
        </mat-tab>
      </mat-tab-group>
    </hh-page-layout>
  `,
  styles: [
    `
      .query-summary {
        display: flex;
        align-items: center;
        justify-content: space-between;
        padding: 8px 12px;
        background: var(--surface-muted);
        border: 1px solid var(--border-light);
        border-radius: var(--radius-input);
        font-size: 12px;
        color: var(--text-secondary);
      }
      .query-summary-text {
        flex: 1;
      }
      .logs-tabs {
        margin-top: 0;
      }

      /* ── Filters ── */
      .filter-group {
        display: flex;
        flex-direction: column;
        gap: 4px;
      }
      .filter-label {
        font-size: 11px;
        font-weight: var(--font-weight-semibold);
        text-transform: uppercase;
        letter-spacing: 0.04em;
        color: var(--text-muted);
      }
      .time-range-group {
        min-width: 320px;
      }
      .service-filter {
        min-width: 220px;
        flex: 1;
      }
      .search-field {
        min-width: 240px;
        flex: 2;
      }
      .level-filter-group {
        display: flex;
        flex-direction: column;
        gap: 4px;
      }
      .level-chips {
        display: flex;
        gap: 4px;
        flex-wrap: wrap;
      }
      .level-chip {
        display: inline-flex;
        align-items: center;
        padding: 4px 12px;
        border-radius: var(--radius-badge);
        border: 1px solid var(--border-default);
        background: transparent;
        font-size: 12px;
        font-weight: var(--font-weight-medium);
        color: var(--text-secondary);
        cursor: pointer;
        transition: all 150ms ease;
        line-height: 1.4;
      }
      .level-chip:hover {
        background: var(--surface-hover);
      }
      .level-chip.selected {
        background: var(--color-primary);
        color: var(--surface-white);
        border-color: var(--color-primary);
      }
      .search-btn {
        margin-top: 18px;
      }

      /* ── Table cells ── */
      .cell-timestamp {
        white-space: nowrap;
        font-family: var(--font-mono);
        font-size: 12px;
      }
      .message-text {
        font-family: var(--font-mono);
        font-size: 12px;
        line-height: 1.5;
      }
      .trace-link {
        color: var(--color-info);
        font-family: var(--font-mono);
        font-size: 11px;
        cursor: pointer;
        margin-left: 6px;
        white-space: nowrap;
        text-decoration: none;
        border-bottom: 1px dashed var(--color-info);
      }
      .trace-link:hover {
        opacity: 0.8;
      }
      .trace-link-inline {
        color: var(--color-info) !important;
        cursor: pointer !important;
        text-decoration: none !important;
        font-family: var(--font-mono);
        font-size: 12px;
        border-bottom: 1px dashed var(--color-info) !important;
      }
      .trace-link-inline:hover {
        opacity: 0.8 !important;
      }

      /* ── Expanded detail row ── */
      .expanded-detail {
        padding: 16px 24px;
        display: flex;
        flex-direction: column;
        gap: 12px;
      }
      .detail-field {
        display: flex;
        flex-direction: column;
        gap: 4px;
      }
      .detail-field-label {
        font-size: 11px;
        font-weight: var(--font-weight-semibold);
        text-transform: uppercase;
        letter-spacing: 0.04em;
        color: var(--text-muted);
      }
      .detail-field-value {
        font-size: 13px;
        color: var(--text-primary);
        margin: 0;
      }
      .detail-field-value.exception {
        color: var(--color-danger);
        white-space: pre-wrap;
        font-family: var(--font-mono);
        font-size: 12px;
        background: var(--surface-danger);
        padding: 8px 12px;
        border-radius: var(--radius-input);
        line-height: 1.5;
      }
      .detail-field-value.json {
        font-family: var(--font-mono);
        font-size: 12px;
        background: var(--surface-muted);
        padding: 8px 12px;
        border-radius: var(--radius-input);
        line-height: 1.5;
        max-height: 200px;
        overflow-y: auto;
      }
      .detail-field-value code {
        font-family: var(--font-mono);
        font-size: 12px;
        background: var(--surface-muted);
        padding: 2px 6px;
        border-radius: 3px;
      }

      /* ── Stream tab ── */
      .stream-card-content {
        padding: 0 !important;
      }
    `,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class LogsPageComponent implements OnInit {
  private readonly logsService = inject(LogsService);
  private readonly route = inject(ActivatedRoute);
  private readonly cdr = inject(ChangeDetectorRef);

  private readonly refreshTrigger = new BehaviorSubject<void>(undefined);

  private readonly destroyRef = inject(DestroyRef);

  readonly resource = new HisHopeResourceState<LogEntry[]>(this.destroyRef);

  selectedService = '';
  selectedLevels: string[] = [];
  searchQuery = '';
  serviceInput = '';

  logs: LogEntry[] = [];
  expandedRowKeys: string[] = [];
  totalCount = 0;
  querySummary = '';
  query: HisHopePageQuery = { page: 1, pageSize: PAGE_SIZE };

  columns: HisHopeDataTableColumn[] = [
    { key: 'timestamp', label: 'Time', responsivePriority: 1 },
    { key: 'level', label: 'Level', responsivePriority: 2 },
    { key: 'service', label: 'Service', responsivePriority: 2 },
    { key: 'message', label: 'Message', responsivePriority: 1 },
  ];

  private timeRange: TimeRange = {
    from: new Date(Date.now() - 60 * 60 * 1000),
    to: new Date(),
    label: 'Last 1h',
  };

  readonly levelOptions = LEVELS;
  readonly allServices = ALL_SERVICES;

  filteredServices: string[] = ALL_SERVICES;

  private readonly searchDebounce = new BehaviorSubject<string>('');

  private readonly loadTrigger$ = merge(
    this.refreshTrigger,
    interval(10000).pipe(map(() => void 0)),
  ).pipe(debounceTime(100));

  ngOnInit(): void {
    // Wire up debounced search
    this.searchDebounce.pipe(debounceTime(300)).subscribe(() => {
      this.refresh();
    });

    effect(() => {
      const entries = this.resource.data();
      if (entries) {
        this.logs = entries;
        this.totalCount = this.estimateTotal(entries.length);
        this.updateQuerySummary();
      }
    });

    // Read service query param from Resource card quick-link
    const serviceParam = this.route.snapshot.queryParamMap.get('service');
    if (serviceParam) {
      this.selectedService = serviceParam;
      this.serviceInput = serviceParam;
    }

    // Initial load
    this.loadTrigger$.subscribe(() => this.loadLogs());
  }

  /**
   * The logs API returns pages without a total count. A full page implies more
   * records exist, so expose one extra item to keep the table's "Next" active.
   */
  private estimateTotal(loaded: number): number {
    const offset = (this.query.page - 1) * this.query.pageSize;
    return loaded >= this.query.pageSize
      ? offset + loaded + 1
      : offset + loaded;
  }

  get tableRows(): Record<string, unknown>[] {
    return this.logs.map((log) => ({
      id: log.id,
      timestamp: log.timestamp,
      level: log.level,
      service: log.service,
      message: log.message,
      entity: log,
    }));
  }

  logOf(row: Record<string, unknown>): LogEntry {
    return row['entity'] as LogEntry;
  }

  levelTone(level: string): HisHopeStatusTone {
    return levelToneFor(level);
  }

  serviceTone(service: string): HisHopeStatusTone {
    return serviceToneFor(service);
  }

  onQueryChange(query: HisHopePageQuery): void {
    this.query = query;
    this.refresh();
  }

  onRowExpandChange(event: { rowKey: string; expanded: boolean }): void {
    this.expandedRowKeys = event.expanded
      ? [...this.expandedRowKeys, event.rowKey]
      : this.expandedRowKeys.filter((key) => key !== event.rowKey);
  }

  private updateQuerySummary(): void {
    const parts: string[] = [];
    if (this.searchQuery) parts.push(`query: "${this.searchQuery}"`);
    if (this.selectedService) parts.push(`service: ${this.selectedService}`);
    if (this.selectedLevels.length > 0)
      parts.push(`level: ${this.selectedLevels.join(', ')}`);
    if (this.timeRange.label) parts.push(`range: ${this.timeRange.label}`);
    this.querySummary = parts.length > 0 ? parts.join(' | ') : '';
  }

  clearAllFilters(): void {
    this.searchQuery = '';
    this.selectedService = '';
    this.serviceInput = '';
    this.selectedLevels = [];
    this.timeRange = {
      from: new Date(Date.now() - 60 * 60 * 1000),
      to: new Date(),
      label: 'Last 1h',
    };
    this.query = { page: 1, pageSize: PAGE_SIZE };
    this.refresh();
  }

  clearSearch(): void {
    this.searchQuery = '';
    this.onSearchChange();
  }

  refresh(): void {
    this.refreshTrigger.next();
  }

  private loadLogs(): void {
    const from = (this.query.page - 1) * this.query.pageSize;
    this.resource.load(
      this.logsService.query({
        service: this.selectedService || undefined,
        level:
          this.selectedLevels.length > 0
            ? this.selectedLevels.join(',')
            : undefined,
        query: this.searchQuery || undefined,
        from: from.toString(),
        size: this.query.pageSize,
        fromDate: this.timeRange.from.toISOString(),
        toDate: this.timeRange.to.toISOString(),
      }),
    );
  }

  search(): void {
    this.refresh();
  }

  onFilterChange(): void {
    this.refresh();
  }

  onSearchChange(): void {
    this.searchDebounce.next(this.searchQuery);
  }

  onTimeRangeChange(range: TimeRange): void {
    this.timeRange = range;
    this.refresh();
  }

  onServiceInputChange(): void {
    const input = this.serviceInput.toLowerCase();
    this.filteredServices = input
      ? ALL_SERVICES.filter((s) => s.toLowerCase().includes(input))
      : ALL_SERVICES;
  }

  onServiceSelected(service: string): void {
    this.selectedService = service;
    this.serviceInput = service;
    this.refresh();
  }

  toggleLevel(level: string): void {
    const idx = this.selectedLevels.indexOf(level);
    if (idx >= 0) {
      this.selectedLevels = this.selectedLevels.filter((l) => l !== level);
    } else {
      this.selectedLevels = [...this.selectedLevels, level];
    }
    this.refresh();
  }

  onTabChange(): void {
    // No special handling needed
  }
}
