import { Component, OnInit, ChangeDetectionStrategy, inject, ChangeDetectorRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { MatCardModule } from '@angular/material/card';
import { MatTableModule } from '@angular/material/table';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatTabsModule } from '@angular/material/tabs';
import { MatChipsModule } from '@angular/material/chips';
import { MatAutocompleteModule } from '@angular/material/autocomplete';

import { BehaviorSubject, of, combineLatest, interval, merge } from 'rxjs';
import { catchError, switchMap, finalize, debounceTime, map } from 'rxjs/operators';
import { LogsService } from '../../core/services/logs.service';
import { LogEntry } from '../../core/models/log-entry.model';
import { TimeRangePickerComponent, TimeRange } from '../../shared/time-range-picker/time-range-picker.component';
import { LogStreamViewComponent } from './log-stream-view.component';

interface ExpandedRow {
  [logId: string]: boolean;
}

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

const SERVICE_COLORS = [
  '#2F6B4A', '#5B8C5A', '#2563EB', '#6B4FA0', '#B6581C',
  '#C25450', '#0D9488',
];

@Component({
  selector: 'app-logs-page',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    RouterModule,
    MatCardModule,
    MatTableModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatButtonModule,
    MatIconModule,
    MatProgressSpinnerModule,
    MatTabsModule,
    MatChipsModule,
    MatAutocompleteModule,
    TimeRangePickerComponent,
    LogStreamViewComponent,
  ],
  template: `
    <div class="page-header">
      <h1 class="page-title">System Logs</h1>
      <div class="page-header-actions">
        <span class="result-count" *ngIf="totalCount > 0">{{ totalCount }} results</span>
        <button mat-stroked-button (click)="refresh()" [disabled]="(loading$ | async) ?? false">
          <mat-icon>refresh</mat-icon>
          Refresh
        </button>
      </div>
    </div>

    <!-- Query summary bar -->
    <div class="query-summary" *ngIf="querySummary">
      <span class="query-summary-text">{{ querySummary }}</span>
      <button mat-icon-button size="small" (click)="clearAllFilters()" title="Clear all filters">
        <mat-icon>close</mat-icon>
      </button>
    </div>

    <mat-tab-group (selectedIndexChange)="onTabChange($event)" class="logs-tabs">
      <!-- ═══════════════ Search Tab ═══════════════ -->
      <mat-tab label="Search">
        <ng-template matTabContent>
          <!-- Filters card -->
          <mat-card class="filters-card">
            <mat-card-content>
              <!-- Row 1: Time range + Service autocomplete -->
              <div class="filters-row">
                <div class="filter-group time-range-group">
                  <label class="filter-label">Time range</label>
                  <app-time-range-picker (rangeChange)="onTimeRangeChange($event)"></app-time-range-picker>
                </div>

                <mat-form-field appearance="outline" subscriptSizing="dynamic" class="service-filter">
                  <mat-label>Service</mat-label>
                  <input
                    matInput
                    [matAutocomplete]="autoService"
                    [(ngModel)]="serviceInput"
                    (ngModelChange)="onServiceInputChange()"
                    placeholder="All services"
                  />
                  <mat-icon matSuffix>search</mat-icon>
                  <mat-autocomplete #autoService="matAutocomplete" (optionSelected)="onServiceSelected($event.option.value)">
                    <mat-option value="">All services</mat-option>
                    <mat-option *ngFor="let svc of filteredServices" [value]="svc">
                      {{ svc }}
                    </mat-option>
                  </mat-autocomplete>
                </mat-form-field>
              </div>

              <!-- Row 2: Search + level chips -->
              <div class="filters-row filters-second-row">
                <mat-form-field appearance="outline" subscriptSizing="dynamic" class="search-field">
                  <mat-label>Full-text search</mat-label>
                  <input matInput [(ngModel)]="searchQuery" (ngModelChange)="onSearchChange()" placeholder="Keywords, trace ID, message..." />
                  <button matSuffix mat-icon-button *ngIf="searchQuery" (click)="clearSearch()" type="button">
                    <mat-icon>close</mat-icon>
                  </button>
                </mat-form-field>

                <div class="level-filter-group">
                  <label class="filter-label">Level</label>
                  <div class="level-chips">
                    <button
                      *ngFor="let level of levelOptions"
                      class="level-chip"
                      [class.selected]="selectedLevels.includes(level.value)"
                      (click)="toggleLevel(level.value)">
                      {{ level.label }}
                    </button>
                  </div>
                </div>

                <button mat-raised-button color="primary" (click)="search()" class="search-btn">
                  <mat-icon>search</mat-icon>
                  Search
                </button>
              </div>
            </mat-card-content>
          </mat-card>

          <!-- Results card -->
          <mat-card>
            <mat-card-content class="table-content">
              <!-- Loading -->
              <div class="loading-state" *ngIf="(loading$ | async) && !(error$ | async)">
                <mat-spinner diameter="28"></mat-spinner>
              </div>

              <!-- Error -->
              <div class="error-inline" *ngIf="error$ | async as err">
                <span class="error-text">{{ err }}</span>
                <button mat-stroked-button size="small" (click)="refresh()">Retry</button>
              </div>

              <!-- Table -->
              <table mat-table [dataSource]="logs" class="mat-elevation-z0" multiTemplateDataRows>
                <ng-container matColumnDef="timestamp">
                  <th mat-header-cell *matHeaderCellDef>Time</th>
                  <td mat-cell *matCellDef="let l" class="cell-timestamp">{{ l.timestamp | date:'dd/MM HH:mm:ss' }}</td>
                </ng-container>

                <ng-container matColumnDef="level">
                  <th mat-header-cell *matHeaderCellDef>Level</th>
                  <td mat-cell *matCellDef="let l">
                    <span class="level-badge" [class]="'level-' + l.level.toLowerCase()">
                      {{ l.level }}
                    </span>
                  </td>
                </ng-container>

                <ng-container matColumnDef="service">
                  <th mat-header-cell *matHeaderCellDef>Service</th>
                  <td mat-cell *matCellDef="let l">
                    <span class="service-chip" [style.--chip-color]="getServiceColor(l.service)">
                      {{ l.service }}
                    </span>
                  </td>
                </ng-container>

                <ng-container matColumnDef="message">
                  <th mat-header-cell *matHeaderCellDef>Message</th>
                  <td mat-cell *matCellDef="let l" class="cell-message">
                    <span class="message-text">{{ l.message }}</span>
                    <span class="trace-link" *ngIf="l.traceId" (click)="goToTrace($event, l.traceId!)">
                      [{{ l.traceId | slice:0:8 }}...]
                    </span>
                  </td>
                </ng-container>

                <ng-container matColumnDef="expand">
                  <th mat-header-cell *matHeaderCellDef></th>
                  <td mat-cell *matCellDef="let l">
                    <button mat-icon-button size="small" (click)="toggleExpand($event, l)">
                      <mat-icon>{{ expanded[l.id] ? 'expand_less' : 'expand_more' }}</mat-icon>
                    </button>
                  </td>
                </ng-container>

                <!-- Expanded detail row -->
                <ng-container matColumnDef="expandedDetail">
                  <td mat-cell *matCellDef="let l" [attr.colspan]="displayedColumns.length">
                    <div class="expanded-detail" *ngIf="expanded[l.id]">
                      <div class="detail-field" *ngIf="l.traceId">
                        <span class="detail-field-label">Trace ID</span>
                        <code class="detail-field-value trace-link-inline" (click)="goToTrace($event, l.traceId!)">
                          {{ l.traceId }}
                        </code>
                      </div>
                      <div class="detail-field" *ngIf="l.spanId">
                        <span class="detail-field-label">Span ID</span>
                        <code class="detail-field-value">{{ l.spanId }}</code>
                      </div>
                      <div class="detail-field" *ngIf="l.exception">
                        <span class="detail-field-label">Exception</span>
                        <pre class="detail-field-value exception">{{ l.exception }}</pre>
                      </div>
                      <div class="detail-field" *ngIf="l.properties">
                        <span class="detail-field-label">Properties (JSON)</span>
                        <pre class="detail-field-value json">{{ l.properties | json }}</pre>
                      </div>
                    </div>
                  </td>
                </ng-container>

                <tr mat-header-row *matHeaderRowDef="displayedColumns"></tr>
                <tr mat-row *matRowDef="let row; columns: displayedColumns"
                    (click)="toggleExpand($event, row)" class="log-row"></tr>
                <tr mat-row *matRowDef="let row; columns: ['expandedDetail']" class="detail-row"></tr>

                <tr class="mat-row" *matNoDataRow>
                  <td class="mat-cell empty-state" [attr.colspan]="displayedColumns.length">
                    <mat-icon>article</mat-icon>
                    <p>{{ (loading$ | async) ? 'Loading...' : 'No logs found' }}</p>
                  </td>
                </tr>
              </table>

              <!-- Load more -->
              <div class="load-more" *ngIf="hasMore && !(loading$ | async)">
                <button mat-stroked-button (click)="loadMore()" [disabled]="loadingMore.value">
                  <mat-icon *ngIf="!loadingMore.value">expand_more</mat-icon>
                  <mat-spinner *ngIf="loadingMore.value" diameter="16"></mat-spinner>
                  Load More
                </button>
              </div>
            </mat-card-content>
          </mat-card>
        </ng-template>
      </mat-tab>

      <!-- ═══════════════ Stream Tab ═══════════════ -->
      <mat-tab label="Stream">
        <ng-template matTabContent>
          <mat-card class="stream-card">
            <mat-card-content class="stream-card-content">
              <app-log-stream-view
                [service]="selectedService"
                [level]="selectedLevels.length === 1 ? selectedLevels[0] : ''">
              </app-log-stream-view>
            </mat-card-content>
          </mat-card>
        </ng-template>
      </mat-tab>
    </mat-tab-group>
  `,
  styles: [`
    .page-header {
      display: flex;
      justify-content: space-between;
      align-items: center;
      margin-bottom: 8px;
      flex-wrap: wrap;
      gap: 12px;
    }
    .page-header-actions {
      display: flex;
      align-items: center;
      gap: 12px;
    }
    .result-count {
      font-size: 13px;
      color: var(--text-secondary, #787774);
      background: #F0F0EE;
      padding: 4px 12px;
      border-radius: 4px;
      font-weight: 500;
    }
    .query-summary {
      display: flex;
      align-items: center;
      justify-content: space-between;
      padding: 8px 12px;
      background: #F0F0EE;
      border-radius: 4px;
      margin-bottom: 16px;
      font-size: 12px;
      color: var(--text-secondary, #787774);
    }
    .query-summary-text {
      flex: 1;
    }
    .logs-tabs {
      margin-top: 0;
    }

    /* ── Filters ── */
    .filters-row {
      display: flex;
      gap: 16px;
      align-items: flex-start;
      flex-wrap: wrap;
    }
    .filters-second-row {
      margin-top: 12px;
      align-items: center;
    }
    .filter-group {
      display: flex;
      flex-direction: column;
      gap: 4px;
    }
    .filter-label {
      font-size: 11px;
      font-weight: 600;
      text-transform: uppercase;
      letter-spacing: 0.04em;
      color: var(--text-muted, #A1A09B);
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
      border-radius: 4px;
      border: 1px solid var(--border-default, #EAEAEA);
      background: transparent;
      font-size: 12px;
      font-weight: 500;
      color: var(--text-secondary, #787774);
      cursor: pointer;
      transition: all 150ms ease;
      line-height: 1.4;
    }
    .level-chip:hover {
      background: rgba(0, 0, 0, 0.03);
    }
    .level-chip.selected {
      background: var(--color-primary, #2F6B4A);
      color: #fff;
      border-color: var(--color-primary, #2F6B4A);
    }
    .search-btn {
      margin-top: 18px;
    }

    /* ── Table ── */
    .table-content {
      padding: 0 !important;
    }
    table {
      width: 100%;
    }
    .cell-timestamp {
      white-space: nowrap;
      font-family: var(--font-mono, monospace);
      font-size: 12px;
    }
    .cell-message {
      max-width: 360px;
      overflow: hidden;
      text-overflow: ellipsis;
      white-space: nowrap;
    }
    .message-text {
      font-family: var(--font-mono, 'Cascadia Mono', Consolas, monospace);
      font-size: 12px;
      line-height: 1.5;
    }
    .trace-link {
      color: #2563EB;
      font-family: var(--font-mono, monospace);
      font-size: 11px;
      cursor: pointer;
      margin-left: 6px;
      white-space: nowrap;
      text-decoration: none;
      border-bottom: 1px dashed #2563EB;
    }
    .trace-link:hover {
      color: #1D4ED8;
    }
    .trace-link-inline {
      color: #2563EB !important;
      cursor: pointer !important;
      text-decoration: none !important;
      border-bottom: 1px dashed #2563EB !important;
    }
    .trace-link-inline:hover {
      color: #1D4ED8 !important;
    }
    .log-row {
      cursor: pointer;
      transition: background-color 150ms ease;
    }
    .log-row:hover {
      background-color: rgba(0, 0, 0, 0.02);
    }
    .detail-row {
      background: var(--bg-warm, #F7F6F3);
    }
    .detail-row > td {
      padding: 0 !important;
      border-bottom: 1px solid var(--border-default, #EAEAEA);
    }
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
      font-weight: 600;
      text-transform: uppercase;
      letter-spacing: 0.04em;
      color: var(--text-muted, #A1A09B);
    }
    .detail-field-value {
      font-size: 13px;
      color: var(--text-primary, #1A1A1A);
      margin: 0;
    }
    .detail-field-value.exception {
      color: #C25450;
      white-space: pre-wrap;
      font-family: var(--font-mono, monospace);
      font-size: 12px;
      background: #FDEBEC;
      padding: 8px 12px;
      border-radius: 4px;
      line-height: 1.5;
    }
    .detail-field-value.json {
      font-family: var(--font-mono, monospace);
      font-size: 12px;
      background: var(--bg-warm, #F7F6F3);
      padding: 8px 12px;
      border-radius: 4px;
      line-height: 1.5;
      max-height: 200px;
      overflow-y: auto;
    }
    .detail-field-value code {
      font-family: var(--font-mono, monospace);
      font-size: 12px;
      background: var(--bg-warm, #F7F6F3);
      padding: 2px 6px;
      border-radius: 3px;
    }

    /* ── Level badges ── */
    .level-badge {
      display: inline-block;
      padding: 1px 8px;
      border-radius: 4px;
      font-size: 11px;
      font-weight: 500;
      letter-spacing: 0.02em;
    }
    .level-error, .level-critical {
      background: #FDEBEC;
      color: #C25450;
    }
    .level-warning {
      background: #FDF0E2;
      color: #B6581C;
    }
    .level-information {
      /* INFO: no background per design spec */
      color: #2563EB;
      background: transparent;
    }
    .level-debug {
      background: transparent;
      color: var(--text-muted, #A1A09B);
    }

    /* ── Service chips ── */
    .service-chip {
      display: inline-block;
      padding: 1px 8px;
      border-radius: 4px;
      font-size: 11px;
      font-weight: 500;
      color: var(--chip-color, #787774);
      background: color-mix(in srgb, var(--chip-color, #787774) 12%, transparent);
      max-width: 140px;
      overflow: hidden;
      text-overflow: ellipsis;
      white-space: nowrap;
    }

    /* ── Loading / Error / Empty ── */
    .loading-state {
      display: flex;
      justify-content: center;
      padding: 32px;
    }
    .error-inline {
      display: flex;
      align-items: center;
      justify-content: center;
      gap: 12px;
      padding: 16px;
      background: #FDEBEC;
      color: #C25450;
      font-size: 13px;
    }
    .error-text {
      font-size: 13px;
    }
    .load-more {
      display: flex;
      justify-content: center;
      padding: 16px;
      border-top: 1px solid var(--border-default, #EAEAEA);
    }
    .load-more button {
      min-width: 140px;
      display: flex;
      align-items: center;
      gap: 6px;
    }
    .load-more mat-spinner {
      display: inline-block;
    }
    .empty-state {
      display: flex;
      flex-direction: column;
      align-items: center;
      padding: 48px 24px;
      color: var(--text-muted, #A1A09B);
      text-align: center;
    }
    .empty-state mat-icon {
      font-size: 48px;
      width: 48px;
      height: 48px;
      margin-bottom: 16px;
      opacity: 0.4;
    }

    /* ── Stream tab ── */
    .stream-card-content {
      padding: 0 !important;
    }
  `],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class LogsPageComponent implements OnInit {
  private readonly logsService = inject(LogsService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly cdr = inject(ChangeDetectorRef);

  private readonly refreshTrigger = new BehaviorSubject<void>(undefined);
  private readonly pageTrigger = new BehaviorSubject<number>(0);
  private readonly searchDebounce = new BehaviorSubject<string>('');

  readonly loading$ = new BehaviorSubject<boolean>(true);
  readonly loadingMore = new BehaviorSubject<boolean>(false);
  readonly error$ = new BehaviorSubject<string | null>(null);

  selectedService = '';
  selectedLevels: string[] = [];
  searchQuery = '';
  serviceInput = '';

  logs: LogEntry[] = [];
  hasMore = true;
  expanded: ExpandedRow = {};
  totalCount = 0;
  querySummary = '';
  private currentPage = 0;

  private timeRange: TimeRange = { from: new Date(Date.now() - 60 * 60 * 1000), to: new Date(), label: 'Last 1h' };

  readonly displayedColumns = ['timestamp', 'level', 'service', 'message', 'expand'];
  readonly levelOptions = LEVELS;
  readonly allServices = ALL_SERVICES;

  filteredServices: string[] = ALL_SERVICES;

  private readonly queryParams$ = combineLatest([
    merge(this.refreshTrigger, interval(10000).pipe(map(() => void 0))),
    this.pageTrigger,
  ]).pipe(
    debounceTime(100),
    switchMap(([, page]) => {
      const loading = page === 0 ? this.loading$ : this.loadingMore;
      loading.next(true);
      return this.logsService.query({
        service: this.selectedService || undefined,
        level: this.selectedLevels.length > 0 ? this.selectedLevels.join(',') : undefined,
        query: this.searchQuery || undefined,
        from: (page * PAGE_SIZE).toString(),
        size: PAGE_SIZE,
        fromDate: this.timeRange.from.toISOString(),
        toDate: this.timeRange.to.toISOString(),
      }).pipe(
        catchError(err => {
          const msg = err?.message ?? err?.statusText ?? 'Failed to load logs.';
          this.error$.next(msg);
          loading.next(false);
          return of([] as LogEntry[]);
        }),
        finalize(() => {
          if (page === 0) {
            this.loading$.next(false);
          } else {
            this.loadingMore.next(false);
          }
        }),
      );
    }),
  );

  ngOnInit(): void {
    // Wire up debounced search
    this.searchDebounce.pipe(
      debounceTime(300),
    ).subscribe(() => {
      this.refresh();
    });

    this.queryParams$.subscribe(entries => {
      if (this.currentPage === 0) {
        this.logs = entries;
      } else {
        this.logs = [...this.logs, ...entries];
      }
      this.hasMore = entries.length >= PAGE_SIZE;
      this.totalCount = this.logs.length;
      this.updateQuerySummary();
      this.cdr.markForCheck();
    });

    // Read service query param from Resource card quick-link
    const serviceParam = this.route.snapshot.queryParamMap.get('service');
    if (serviceParam) {
      this.selectedService = serviceParam;
      this.serviceInput = serviceParam;
    }

    // Initial load
    this.refresh();
  }

  private updateQuerySummary(): void {
    const parts: string[] = [];
    if (this.searchQuery) parts.push(`query: "${this.searchQuery}"`);
    if (this.selectedService) parts.push(`service: ${this.selectedService}`);
    if (this.selectedLevels.length > 0) parts.push(`level: ${this.selectedLevels.join(', ')}`);
    if (this.timeRange.label) parts.push(`range: ${this.timeRange.label}`);
    this.querySummary = parts.length > 0 ? parts.join(' | ') : '';
  }

  clearAllFilters(): void {
    this.searchQuery = '';
    this.selectedService = '';
    this.serviceInput = '';
    this.selectedLevels = [];
    this.timeRange = { from: new Date(Date.now() - 60 * 60 * 1000), to: new Date(), label: 'Last 1h' };
    this.refresh();
  }

  clearSearch(): void {
    this.searchQuery = '';
    this.onSearchChange();
  }

  refresh(): void {
    this.currentPage = 0;
    this.logs = [];
    this.hasMore = true;
    this.error$.next(null);
    this.refreshTrigger.next();
  }

  search(): void {
    this.refresh();
  }

  loadMore(): void {
    if (this.loadingMore.value || !this.hasMore) return;
    this.currentPage++;
    this.pageTrigger.next(this.currentPage);
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
      ? ALL_SERVICES.filter(s => s.toLowerCase().includes(input))
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
      this.selectedLevels = this.selectedLevels.filter(l => l !== level);
    } else {
      this.selectedLevels = [...this.selectedLevels, level];
    }
    this.refresh();
  }

  toggleExpand(event: MouseEvent, entry: LogEntry): void {
    event.stopPropagation();
    this.expanded[entry.id] = !this.expanded[entry.id];
    this.expanded = { ...this.expanded };
  }

  onStreamLog(entry: LogEntry): void {
    this.logs = [entry, ...this.logs];
    this.totalCount = this.logs.length;
    this.cdr.markForCheck();
  }

  onTabChange(index: number): void {
    // No special handling needed
  }

  goToTrace(event: MouseEvent, traceId: string): void {
    event.stopPropagation();
    this.router.navigate(['/traces', traceId]);
  }

  getServiceColor(service: string): string {
    let hash = 0;
    for (let i = 0; i < service.length; i++) {
      hash = ((hash << 5) - hash) + service.charCodeAt(i);
      hash |= 0;
    }
    const idx = Math.abs(hash) % SERVICE_COLORS.length;
    return SERVICE_COLORS[idx];
  }
}
