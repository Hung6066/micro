import { Component, OnInit, ChangeDetectionStrategy, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MatCardModule } from '@angular/material/card';
import { MatTableModule } from '@angular/material/table';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';

import { BehaviorSubject, of, combineLatest } from 'rxjs';
import { catchError, switchMap, finalize, debounceTime } from 'rxjs/operators';
import { LogsService } from '../../core/services/logs.service';
import { LogEntry } from '../../core/models/log-entry.model';
import { LogLevelFilterComponent } from '../../shared/log-level-filter/log-level-filter.component';
import { LogStreamComponent } from './log-stream.component';

interface ExpandedRow {
  [logId: string]: boolean;
}

const PAGE_SIZE = 20;

@Component({
  selector: 'app-logs-page',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    MatCardModule,
    MatTableModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatButtonModule,
    MatIconModule,
    MatProgressSpinnerModule,
    LogLevelFilterComponent,
    LogStreamComponent,
  ],
  template: `
    <div class="page-header">
      <h1 class="page-title">Nhật ký hệ thống</h1>
      <button mat-stroked-button (click)="refresh()" [disabled]="(loading$ | async) ?? false">
        <mat-icon>refresh</mat-icon>
        Làm mới
      </button>
    </div>

    <!-- Filters card -->
    <mat-card class="filters-card">
      <mat-card-content>
        <div class="filters-row">
          <mat-form-field appearance="outline" subscriptSizing="dynamic">
            <mat-label>Dịch vụ</mat-label>
            <mat-select [(ngModel)]="selectedService" (selectionChange)="onFilterChange()">
              <mat-option value="">Tất cả dịch vụ</mat-option>
              <mat-option value="ApiGateway">ApiGateway</mat-option>
              <mat-option value="IdentityService">IdentityService</mat-option>
              <mat-option value="PatientService">PatientService</mat-option>
              <mat-option value="AppointmentService">AppointmentService</mat-option>
              <mat-option value="ClinicalService">ClinicalService</mat-option>
              <mat-option value="LabService">LabService</mat-option>
              <mat-option value="PharmacyService">PharmacyService</mat-option>
              <mat-option value="BillingService">BillingService</mat-option>
            </mat-select>
          </mat-form-field>

          <mat-form-field appearance="outline" subscriptSizing="dynamic">
            <mat-label>Tìm kiếm</mat-label>
            <input matInput [(ngModel)]="searchQuery" (ngModelChange)="onSearchChange()" placeholder="Từ khóa..." />
          </mat-form-field>

          <button mat-raised-button color="primary" (click)="search()">
            <mat-icon>search</mat-icon>
            Tìm kiếm
          </button>
        </div>

        <div class="filters-row filters-second-row">
          <app-log-level-filter
            [selected]="selectedLevel"
            (levelChange)="onLevelChange($event)">
          </app-log-level-filter>

          <app-log-stream
            [service]="selectedService"
            [level]="selectedLevel"
            (logReceived)="onStreamLog($event)">
          </app-log-stream>
        </div>
      </mat-card-content>
    </mat-card>

    <!-- Log table -->
    <mat-card>
      <mat-card-content class="table-content">
        <!-- Loading -->
        <div class="loading-state" *ngIf="(loading$ | async) && !(error$ | async)">
          <mat-spinner diameter="28"></mat-spinner>
        </div>

        <!-- Error -->
        <div class="error-inline" *ngIf="error$ | async as err">
          <span class="error-text">{{ err }}</span>
          <button mat-stroked-button size="small" (click)="refresh()">Thử lại</button>
        </div>

        <!-- Table -->
        <table mat-table [dataSource]="logs" class="mat-elevation-z0" multiTemplateDataRows>
          <ng-container matColumnDef="timestamp">
            <th mat-header-cell *matHeaderCellDef>Thời gian</th>
            <td mat-cell *matCellDef="let l">{{ l.timestamp | date:'dd/MM HH:mm:ss' }}</td>
          </ng-container>

          <ng-container matColumnDef="level">
            <th mat-header-cell *matHeaderCellDef>Cấp độ</th>
            <td mat-cell *matCellDef="let l">
              <span class="level-badge" [class]="'level-' + l.level.toLowerCase()">
                {{ l.level }}
              </span>
            </td>
          </ng-container>

          <ng-container matColumnDef="service">
            <th mat-header-cell *matHeaderCellDef>Dịch vụ</th>
            <td mat-cell *matCellDef="let l">{{ l.service }}</td>
          </ng-container>

          <ng-container matColumnDef="message">
            <th mat-header-cell *matHeaderCellDef>Nội dung</th>
            <td mat-cell *matCellDef="let l" class="message-cell">{{ l.message }}</td>
          </ng-container>

          <ng-container matColumnDef="expand">
            <th mat-header-cell *matHeaderCellDef></th>
            <td mat-cell *matCellDef="let l">
              <button mat-icon-button size="small" (click)="toggleExpand(l)">
                <mat-icon>{{ expanded[l.id] ? 'expand_less' : 'expand_more' }}</mat-icon>
              </button>
            </td>
          </ng-container>

          <!-- Expanded detail row -->
          <ng-container matColumnDef="expandedDetail">
            <td mat-cell *matCellDef="let l" [attr.colspan]="displayedColumns.length">
              <div class="expanded-detail" [@.trigger] *ngIf="expanded[l.id]">
                <div class="detail-field" *ngIf="l.exception">
                  <span class="detail-field-label">Exception</span>
                  <pre class="detail-field-value exception">{{ l.exception }}</pre>
                </div>
                <div class="detail-field" *ngIf="l.traceId">
                  <span class="detail-field-label">Trace ID</span>
                  <code class="detail-field-value">{{ l.traceId }}</code>
                </div>
                <div class="detail-field" *ngIf="l.spanId">
                  <span class="detail-field-label">Span ID</span>
                  <code class="detail-field-value">{{ l.spanId }}</code>
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
              (click)="toggleExpand(row)" class="log-row"></tr>
          <tr mat-row *matRowDef="let row; columns: ['expandedDetail']" class="detail-row"></tr>

          <tr class="mat-row" *matNoDataRow>
            <td class="mat-cell empty-state" [attr.colspan]="displayedColumns.length">
              <mat-icon>article</mat-icon>
              <p>{{ (loading$ | async) ? 'Đang tải...' : 'Không có nhật ký nào' }}</p>
            </td>
          </tr>
        </table>

        <!-- Load more -->
        <div class="load-more" *ngIf="hasMore && !(loading$ | async)">
          <button mat-stroked-button (click)="loadMore()" [disabled]="loadingMore">
            <mat-icon *ngIf="!loadingMore">expand_more</mat-icon>
            <mat-spinner *ngIf="loadingMore" diameter="16"></mat-spinner>
            Tải thêm
          </button>
        </div>
      </mat-card-content>
    </mat-card>
  `,
  styles: [`
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
    .filters-second-row {
      margin-top: 12px;
      align-items: center;
    }
    .table-content {
      padding: 0 !important;
    }
    table {
      width: 100%;
    }
    .message-cell {
      max-width: 400px;
      overflow: hidden;
      text-overflow: ellipsis;
      white-space: nowrap;
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
      background: #E1F3FE;
      color: #2563EB;
    }
    .level-debug {
      background: #F3EDF8;
      color: #6B4FA0;
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
  `],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class LogsPageComponent implements OnInit {
  private readonly logsService = inject(LogsService);

  private readonly refreshTrigger = new BehaviorSubject<void>(undefined);
  private readonly pageTrigger = new BehaviorSubject<number>(0);
  private searchDebounce = new BehaviorSubject<string>('');

  readonly loading$ = new BehaviorSubject<boolean>(true);
  readonly loadingMore = new BehaviorSubject<boolean>(false);
  readonly error$ = new BehaviorSubject<string | null>(null);

  selectedService = '';
  selectedLevel = '';
  searchQuery = '';

  logs: LogEntry[] = [];
  hasMore = true;
  expanded: ExpandedRow = {};
  private currentPage = 0;

  readonly displayedColumns = ['timestamp', 'level', 'service', 'message', 'expand'];

  private readonly queryParams$ = combineLatest([
    this.refreshTrigger,
    this.pageTrigger,
  ]).pipe(
    debounceTime(100),
    switchMap(([, page]) => {
      const loading = page === 0 ? this.loading$ : this.loadingMore;
      loading.next(true);
      return this.logsService.query({
        service: this.selectedService || undefined,
        level: this.selectedLevel || undefined,
        query: this.searchQuery || undefined,
        from: (page * PAGE_SIZE).toString(),
        size: PAGE_SIZE,
      }).pipe(
        catchError(err => {
          const msg = err?.message ?? err?.statusText ?? 'Không thể tải nhật ký.';
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
    this.queryParams$.subscribe(entries => {
      if (this.currentPage === 0) {
        this.logs = entries;
      } else {
        this.logs = [...this.logs, ...entries];
      }
      this.hasMore = entries.length >= PAGE_SIZE;
    });

    // Initial load
    this.refresh();
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

  onLevelChange(level: string): void {
    this.selectedLevel = level;
    this.refresh();
  }

  onSearchChange(): void {
    this.searchDebounce.next(this.searchQuery);
  }

  toggleExpand(entry: LogEntry): void {
    this.expanded[entry.id] = !this.expanded[entry.id];
    // Trigger change detection by replacing the object
    this.expanded = { ...this.expanded };
  }

  onStreamLog(entry: LogEntry): void {
    // Prepend to logs list
    this.logs = [entry, ...this.logs];
  }
}
