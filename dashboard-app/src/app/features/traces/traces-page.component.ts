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
import { FormsModule } from '@angular/forms';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { RouterModule } from '@angular/router';
import { BehaviorSubject, Subject, of } from 'rxjs';
import { catchError, debounceTime, takeUntil } from 'rxjs/operators';
import { TracesService } from '../../core/services/traces.service';
import { ResourceService } from '../../core/services/resource.service';
import { TraceSummary } from '../../core/models/trace.model';
import { Resource } from '../../core/models/resource.model';
import { ActivatedRoute, Router } from '@angular/router';
import { HisHopeResourceState } from '@his-hope/frontend-foundation/query';
import {
  HisHopeDataTableCellDirective,
  HisHopeDataTableColumn,
  HisHopeDataTableComponent,
  HisHopeFilterToolbarComponent,
  HisHopePageHeaderComponent,
  HisHopePageLayoutComponent,
} from '@his-hope/frontend-foundation/ui';
import { HisHopeTranslatePipe } from '@his-hope/frontend-foundation/i18n';

@Component({
  selector: 'app-traces-page',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    RouterModule,
    MatIconModule,
    MatButtonModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    HisHopeDataTableCellDirective,
    HisHopeDataTableComponent,
    HisHopeFilterToolbarComponent,
    HisHopePageHeaderComponent,
    HisHopePageLayoutComponent,
    HisHopeTranslatePipe,
  ],
  template: `
    <hh-page-layout>
      <hh-page-header
        hhPageHeader
        [title]="'dashboard.traces.title' | hhTranslate: 'System Traces'"
        [subtitle]="
          'dashboard.traces.subtitle'
            | hhTranslate: 'Distributed request traces across all services'
        "
      >
        <button
          mat-stroked-button
          (click)="refresh()"
          [disabled]="resource.loading()"
        >
          <mat-icon>refresh</mat-icon>
          {{ 'dashboard.common.refresh' | hhTranslate: 'Refresh' }}
        </button>
      </hh-page-header>

      <hh-filter-toolbar label="Trace filters" [resultCount]="traces.length">
        <mat-form-field appearance="outline" subscriptSizing="dynamic">
          <mat-label>{{
            'dashboard.traces.service' | hhTranslate: 'Service'
          }}</mat-label>
          <mat-select
            [(ngModel)]="selectedService"
            (selectionChange)="onFilterChange()"
          >
            <mat-option value="">{{
              'dashboard.traces.allServices' | hhTranslate: 'All services'
            }}</mat-option>
            @for (svc of availableServices; track svc.name) {
              <mat-option [value]="svc.name">
                {{ svc.displayName || svc.name }}
              </mat-option>
            }
          </mat-select>
        </mat-form-field>

        <mat-form-field appearance="outline" subscriptSizing="dynamic">
          <mat-label>{{
            'dashboard.traces.timeRange' | hhTranslate: 'Time range'
          }}</mat-label>
          <mat-select
            [(ngModel)]="selectedTimeRange"
            (selectionChange)="onFilterChange()"
          >
            <mat-option value="15m">{{
              'dashboard.traces.time.15m' | hhTranslate: '15 minutes'
            }}</mat-option>
            <mat-option value="1h">{{
              'dashboard.traces.time.1h' | hhTranslate: '1 hour'
            }}</mat-option>
            <mat-option value="6h">{{
              'dashboard.traces.time.6h' | hhTranslate: '6 hours'
            }}</mat-option>
            <mat-option value="24h">{{
              'dashboard.traces.time.24h' | hhTranslate: '24 hours'
            }}</mat-option>
            <mat-option value="7d">{{
              'dashboard.traces.time.7d' | hhTranslate: '7 days'
            }}</mat-option>
          </mat-select>
        </mat-form-field>

        <mat-form-field appearance="outline" subscriptSizing="dynamic">
          <mat-label>{{
            'dashboard.traces.minDuration' | hhTranslate: 'Min duration (ms)'
          }}</mat-label>
          <input
            matInput
            type="number"
            [(ngModel)]="minDurationMs"
            (ngModelChange)="onFilterChange()"
            placeholder="0"
            min="0"
          />
        </mat-form-field>

        <button mat-raised-button color="primary" (click)="search()">
          <mat-icon>search</mat-icon>
          {{ 'dashboard.common.search' | hhTranslate: 'Search' }}
        </button>
      </hh-filter-toolbar>

      @let loading = resource.loading();
      @let error = resource.error() ? resource.errorMessage() : '';
      <hh-data-table
        label="System traces"
        [columns]="columns"
        [rows]="tableRows"
        [loading]="loading"
        [error]="error"
        [empty]="!loading && !error && traces.length === 0"
        [pageSize]="20"
        [rowClickable]="true"
        [urlSync]="false"
        [searchable]="false"
        emptyMessage="No traces found."
        (rowClick)="navigateToDetail(traceIdOf($event))"
        (retry)="refresh()"
      >
        <ng-template hhDataTableCell="traceId" let-row>
          <span class="mono"
            >{{ traceOf(row).traceId | slice: 0 : 16 }}...</span
          >
        </ng-template>
        <ng-template hhDataTableCell="durationMs" let-row>
          <span class="mono">{{ traceOf(row).durationMs | number }}ms</span>
        </ng-template>
        <ng-template hhDataTableCell="startTime" let-row>
          <span class="mono">{{
            traceOf(row).startTime | date: 'dd/MM HH:mm:ss'
          }}</span>
        </ng-template>
      </hh-data-table>
    </hh-page-layout>
  `,
  styles: [
    `
      .mono {
        font-family: var(--font-mono);
        font-size: 12px;
      }
    `,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class TracesPageComponent implements OnInit, OnDestroy {
  private readonly tracesService = inject(TracesService);
  private readonly resourceService = inject(ResourceService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly cdr = inject(ChangeDetectorRef);
  private readonly destroy$ = new Subject<void>();

  private readonly refreshTrigger = new BehaviorSubject<void>(undefined);

  private readonly destroyRef = inject(DestroyRef);

  readonly resource = new HisHopeResourceState<TraceSummary[]>(this.destroyRef);

  constructor() {
    effect(() => {
      const traces = this.resource.data();
      if (!traces) return;
      this.traces = traces;
      this.cdr.markForCheck();
    });
  }

  selectedService = '';
  selectedTimeRange = '1h';
  minDurationMs: number | null = null;

  traces: TraceSummary[] = [];
  availableServices: Resource[] = [];

  columns: HisHopeDataTableColumn[] = [
    {
      key: 'traceId',
      label: 'Trace ID',
      sortable: true,
      responsivePriority: 1,
    },
    {
      key: 'rootService',
      label: 'Service',
      sortable: true,
      responsivePriority: 2,
    },
    {
      key: 'durationMs',
      label: 'Duration',
      sortable: true,
      responsivePriority: 2,
    },
    { key: 'spanCount', label: 'Spans', sortable: true, responsivePriority: 3 },
    {
      key: 'startTime',
      label: 'Start time',
      sortable: true,
      responsivePriority: 1,
    },
  ];

  private readonly query$ = this.refreshTrigger.pipe(debounceTime(100));

  private loadTraces(): void {
    const now = new Date();
    const from = this.getFromDate(now, this.selectedTimeRange);
    this.resource.load(
      this.tracesService.search({
        service: this.selectedService || undefined,
        from: from.toISOString(),
        to: now.toISOString(),
        minDurationMs: this.minDurationMs ?? undefined,
        limit: 100,
      }),
    );
  }

  ngOnInit(): void {
    this.query$.subscribe(() => this.loadTraces());

    // Load services for dropdown from ResourceService
    this.resourceService
      .getAll()
      .pipe(
        catchError(() => of([] as Resource[])),
        takeUntil(this.destroy$),
      )
      .subscribe((resources) => {
        this.availableServices = resources.filter(
          (r) => r.type?.toLowerCase() === 'service',
        );
        this.cdr.markForCheck();
      });

    // Read service query param from Resource card quick-link
    const svc = this.route.snapshot.queryParamMap.get('service');
    if (svc) {
      this.selectedService = svc;
    }

    this.refresh();
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  private getFromDate(now: Date, range: string): Date {
    const from = new Date(now);
    switch (range) {
      case '15m':
        from.setMinutes(from.getMinutes() - 15);
        break;
      case '1h':
        from.setHours(from.getHours() - 1);
        break;
      case '6h':
        from.setHours(from.getHours() - 6);
        break;
      case '24h':
        from.setDate(from.getDate() - 1);
        break;
      case '7d':
        from.setDate(from.getDate() - 7);
        break;
      default:
        from.setHours(from.getHours() - 1);
        break;
    }
    return from;
  }

  get tableRows(): Record<string, unknown>[] {
    return this.traces.map((trace) => ({
      id: trace.traceId,
      traceId: trace.traceId,
      rootService: trace.rootService,
      durationMs: trace.durationMs,
      spanCount: trace.spanCount,
      startTime: trace.startTime,
      entity: trace,
    }));
  }

  traceOf(row: Record<string, unknown>): TraceSummary {
    return row['entity'] as TraceSummary;
  }

  traceIdOf(row: Record<string, unknown>): string {
    return String(row['traceId'] ?? '');
  }

  refresh(): void {
    this.refreshTrigger.next();
  }

  search(): void {
    this.refresh();
  }

  onFilterChange(): void {
    this.refresh();
  }

  navigateToDetail(traceId: string): void {
    this.router.navigate(['/traces', traceId]);
  }
}
