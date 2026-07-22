import { Component, OnInit, ChangeDetectionStrategy, inject, OnDestroy, ChangeDetectorRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MatCardModule } from '@angular/material/card';
import { MatTableModule } from '@angular/material/table';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { RouterModule } from '@angular/router';
import { BehaviorSubject, Observable, Subject, of, combineLatest } from 'rxjs';
import { catchError, switchMap, finalize, debounceTime, takeUntil, tap } from 'rxjs/operators';
import { TracesService } from '../../core/services/traces.service';
import { ResourceService } from '../../core/services/resource.service';
import { TraceSummary } from '../../core/models/trace.model';
import { Resource } from '../../core/models/resource.model';
import { ActivatedRoute, Router } from '@angular/router';

@Component({
  selector: 'app-traces-page',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    RouterModule,
    MatCardModule,
    MatTableModule,
    MatIconModule,
    MatButtonModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatProgressSpinnerModule,
  ],
  template: `
    <div class="page-header">
      <h1 class="page-title">System Traces</h1>
      <button mat-stroked-button (click)="refresh()" [disabled]="(loading$ | async) ?? false">
        <mat-icon>refresh</mat-icon>
        Refresh
      </button>
    </div>

    <!-- Filters card -->
    <mat-card class="filters-card">
      <mat-card-content>
        <div class="filters-row">
          <mat-form-field appearance="outline" subscriptSizing="dynamic">
            <mat-label>Service</mat-label>
            <mat-select [(ngModel)]="selectedService" (selectionChange)="onFilterChange()">
              <mat-option value="">All services</mat-option>
              <mat-option *ngFor="let svc of availableServices" [value]="svc.name">
                {{ svc.displayName || svc.name }}
              </mat-option>
            </mat-select>
          </mat-form-field>

          <mat-form-field appearance="outline" subscriptSizing="dynamic">
            <mat-label>Time range</mat-label>
            <mat-select [(ngModel)]="selectedTimeRange" (selectionChange)="onFilterChange()">
              <mat-option value="15m">15 minutes</mat-option>
              <mat-option value="1h">1 hour</mat-option>
              <mat-option value="6h">6 hours</mat-option>
              <mat-option value="24h">24 hours</mat-option>
              <mat-option value="7d">7 days</mat-option>
            </mat-select>
          </mat-form-field>

          <mat-form-field appearance="outline" subscriptSizing="dynamic">
            <mat-label>Min duration (ms)</mat-label>
            <input matInput type="number" [(ngModel)]="minDurationMs" (ngModelChange)="onFilterChange()"
                   placeholder="0" min="0" />
          </mat-form-field>

          <button mat-raised-button color="primary" (click)="search()">
            <mat-icon>search</mat-icon>
            Search
          </button>
        </div>
      </mat-card-content>
    </mat-card>

    <!-- Table card -->
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
        <table mat-table [dataSource]="traces" class="mat-elevation-z0">
          <ng-container matColumnDef="traceId">
            <th mat-header-cell *matHeaderCellDef>Trace ID</th>
            <td mat-cell *matCellDef="let t" class="mono">{{ t.traceId | slice:0:16 }}...</td>
          </ng-container>

          <ng-container matColumnDef="rootService">
            <th mat-header-cell *matHeaderCellDef>Service</th>
            <td mat-cell *matCellDef="let t">{{ t.rootService }}</td>
          </ng-container>

          <ng-container matColumnDef="duration">
            <th mat-header-cell *matHeaderCellDef>Duration</th>
            <td mat-cell *matCellDef="let t" class="mono">{{ t.durationMs | number }}ms</td>
          </ng-container>

          <ng-container matColumnDef="spans">
            <th mat-header-cell *matHeaderCellDef>Spans</th>
            <td mat-cell *matCellDef="let t">{{ t.spanCount }}</td>
          </ng-container>

          <ng-container matColumnDef="startTime">
            <th mat-header-cell *matHeaderCellDef>Start time</th>
            <td mat-cell *matCellDef="let t" class="mono">{{ t.startTime | date:'dd/MM HH:mm:ss' }}</td>
          </ng-container>

          <tr mat-header-row *matHeaderRowDef="displayedColumns"></tr>
          <tr mat-row *matRowDef="let row; columns: displayedColumns" class="trace-row"
              (click)="navigateToDetail(row.traceId)"></tr>

          <tr class="mat-row" *matNoDataRow>
            <td class="mat-cell empty-state" [attr.colspan]="displayedColumns.length">
              <mat-icon>timeline</mat-icon>
              <p>{{ (loading$ | async) ? 'Loading...' : 'No traces found' }}</p>
            </td>
          </tr>
        </table>
      </mat-card-content>
    </mat-card>
  `,
  styles: [`
    .page-header {
      display: flex;
      justify-content: space-between;
      align-items: center;
      margin-bottom: 24px;
    }
    .page-title {
      font-size: 20px;
      font-weight: 600;
      color: #1A1A1A;
      margin: 0;
    }
    .filters-card {
      margin-bottom: 16px;
    }
    .filters-row {
      display: flex;
      gap: 16px;
      align-items: flex-start;
      flex-wrap: wrap;
    }
    .filters-row mat-form-field {
      min-width: 180px;
      flex: 1;
    }
    .filters-row button {
      margin-top: 4px;
      height: 40px;
    }
    .table-content {
      padding: 0 !important;
    }
    table { width: 100%; }
    .mono {
      font-family: 'Cascadia Mono', 'Consolas', monospace;
      font-size: 12px;
    }
    .trace-row {
      cursor: pointer;
      transition: background-color 150ms ease;
    }
    .trace-row:hover {
      background-color: rgba(0, 0, 0, 0.02);
    }
    .trace-row:active {
      background-color: rgba(0, 0, 0, 0.04);
    }
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
    .empty-state {
      display: flex;
      flex-direction: column;
      align-items: center;
      padding: 48px 24px;
      color: #A1A09B;
      text-align: center;
    }
    .empty-state mat-icon {
      font-size: 48px;
      width: 48px;
      height: 48px;
      margin-bottom: 16px;
      opacity: 0.4;
    }
    .empty-state p {
      font-size: 14px;
    }
  `],
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

  readonly loading$ = new BehaviorSubject<boolean>(true);
  readonly error$ = new BehaviorSubject<string | null>(null);

  selectedService = '';
  selectedTimeRange = '1h';
  minDurationMs: number | null = null;

  traces: TraceSummary[] = [];
  availableServices: Resource[] = [];

  readonly displayedColumns = ['traceId', 'rootService', 'duration', 'spans', 'startTime'];

  private readonly query$ = this.refreshTrigger.pipe(
    debounceTime(100),
    tap(() => this.loading$.next(true)),
    switchMap(() => {
      const now = new Date();
      const from = this.getFromDate(now, this.selectedTimeRange);
      return this.tracesService.search({
        service: this.selectedService || undefined,
        from: from.toISOString(),
        to: now.toISOString(),
        minDurationMs: this.minDurationMs ?? undefined,
        limit: 100,
      }).pipe(
        catchError(err => {
          const msg = err?.message ?? err?.statusText ?? 'Failed to load traces.';
          this.error$.next(msg);
          this.loading$.next(false);
          return of([] as TraceSummary[]);
        }),
        finalize(() => this.loading$.next(false)),
      );
    }),
  );

  ngOnInit(): void {
    this.query$.subscribe(traces => {
      this.traces = traces;
      this.cdr.markForCheck();
    });

    // Load services for dropdown from ResourceService
    this.resourceService.getAll().pipe(
      catchError(() => of([] as Resource[])),
      takeUntil(this.destroy$),
    ).subscribe(resources => {
      this.availableServices = resources.filter(
        r => r.type?.toLowerCase() === 'service'
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
      case '15m': from.setMinutes(from.getMinutes() - 15); break;
      case '1h':  from.setHours(from.getHours() - 1); break;
      case '6h':  from.setHours(from.getHours() - 6); break;
      case '24h': from.setDate(from.getDate() - 1); break;
      case '7d':  from.setDate(from.getDate() - 7); break;
      default:    from.setHours(from.getHours() - 1); break;
    }
    return from;
  }

  refresh(): void {
    this.error$.next(null);
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
