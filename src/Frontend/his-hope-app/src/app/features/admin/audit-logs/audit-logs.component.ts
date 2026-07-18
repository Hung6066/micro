import { Component, OnInit, OnDestroy, ChangeDetectionStrategy, ChangeDetectorRef } from '@angular/core';
import { FormControl, ReactiveFormsModule, FormsModule } from '@angular/forms';
import { Subject, debounceTime, distinctUntilChanged, takeUntil } from 'rxjs';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { MatTableModule } from '@angular/material/table';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatNativeDateModule } from '@angular/material/core';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatExpansionModule } from '@angular/material/expansion';
import { CommonModule } from '@angular/common';
import { AdminService } from '@core/services/admin.service';
import { AuditLog } from '@core/models/admin.model';
import { PagedResult } from '@core/models/paged-result.model';
import { LoadingSpinnerComponent } from '@shared/components/loading-spinner/loading-spinner.component';
import { EmptyStateComponent } from '@shared/components/empty-state/empty-state.component';

const ACTION_OPTIONS = [
  { value: '', label: 'Tất cả hành động' },
  { value: 'CREATE', label: 'Tạo' },
  { value: 'READ', label: 'Xem' },
  { value: 'UPDATE', label: 'Cập nhật' },
  { value: 'DELETE', label: 'Xóa' },
];

const RESOURCE_OPTIONS = [
  { value: '', label: 'Tất cả loại' },
  { value: 'user', label: 'Người dùng' },
  { value: 'role', label: 'Vai trò' },
  { value: 'patient', label: 'Bệnh nhân' },
  { value: 'appointment', label: 'Lịch hẹn' },
  { value: 'encounter', label: 'Lâm sàng' },
  { value: 'prescription', label: 'Đơn thuốc' },
  { value: 'lab_order', label: 'Xét nghiệm' },
  { value: 'lab_result', label: 'Kết quả XN' },
  { value: 'medication', label: 'Thuốc' },
  { value: 'invoice', label: 'Hóa đơn' },
  { value: 'settings', label: 'Cài đặt' },
];

@Component({
  selector: 'app-audit-logs',
  standalone: true,
  imports: [
    CommonModule, ReactiveFormsModule,
    MatSnackBarModule, MatTableModule, MatPaginatorModule, MatButtonModule,
    MatIconModule, MatFormFieldModule, MatInputModule, MatSelectModule,
    MatDatepickerModule, MatNativeDateModule, MatProgressSpinnerModule,
    MatExpansionModule,
    LoadingSpinnerComponent, EmptyStateComponent,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="audit-logs">
      <div class="page-header">
        <h1>Nhật ký truy cập</h1>
        <p class="subtitle">Xem lịch sử truy cập và hoạt động hệ thống</p>
      </div>

      <div class="filter-bar">
        <mat-form-field appearance="outline" subscriptSizing="dynamic" class="filter-user">
          <mat-icon matPrefix>search</mat-icon>
          <input matInput [formControl]="userSearchControl" placeholder="Tìm người dùng...">
        </mat-form-field>

        <mat-form-field appearance="outline" subscriptSizing="dynamic" class="filter-action">
          <mat-select [formControl]="actionFilter" placeholder="Hành động">
            <mat-option *ngFor="let a of actionOptions" [value]="a.value">{{ a.label }}</mat-option>
          </mat-select>
        </mat-form-field>

        <mat-form-field appearance="outline" subscriptSizing="dynamic" class="filter-resource">
          <mat-select [formControl]="resourceFilter" placeholder="Loại tài nguyên">
            <mat-option *ngFor="let r of resourceOptions" [value]="r.value">{{ r.label }}</mat-option>
          </mat-select>
        </mat-form-field>

        <mat-form-field appearance="outline" subscriptSizing="dynamic" class="filter-date">
          <mat-label>Từ ngày</mat-label>
          <input matInput [matDatepicker]="fromPicker" [formControl]="fromDateControl" placeholder="dd/MM/yyyy">
          <mat-datepicker-toggle matSuffix [for]="fromPicker"></mat-datepicker-toggle>
          <mat-datepicker #fromPicker></mat-datepicker>
        </mat-form-field>

        <mat-form-field appearance="outline" subscriptSizing="dynamic" class="filter-date">
          <mat-label>Đến ngày</mat-label>
          <input matInput [matDatepicker]="toPicker" [formControl]="toDateControl" placeholder="dd/MM/yyyy">
          <mat-datepicker-toggle matSuffix [for]="toPicker"></mat-datepicker-toggle>
          <mat-datepicker #toPicker></mat-datepicker>
        </mat-form-field>

        <button mat-stroked-button (click)="clearFilters()" class="clear-btn">
          <mat-icon>clear</mat-icon> Xóa lọc
        </button>
      </div>

      <div class="table-container mat-elevation-z2">
        <app-loading-spinner [loading]="loading" message="Đang tải nhật ký..."></app-loading-spinner>

        <ng-container *ngIf="!loading">
          <mat-table [dataSource]="auditLogs" class="audit-table" multiTemplateDataRows
                     *ngIf="auditLogs.length > 0; else noData">

            <ng-container matColumnDef="timestamp">
              <mat-header-cell *matHeaderCellDef>Thời gian</mat-header-cell>
              <mat-cell *matCellDef="let log">{{ log.timestamp | date:'dd/MM/yy HH:mm' }}</mat-cell>
            </ng-container>

            <ng-container matColumnDef="userName">
              <mat-header-cell *matHeaderCellDef>Người dùng</mat-header-cell>
              <mat-cell *matCellDef="let log">{{ log.userName }}</mat-cell>
            </ng-container>

            <ng-container matColumnDef="action">
              <mat-header-cell *matHeaderCellDef>Hành động</mat-header-cell>
              <mat-cell *matCellDef="let log">
                <span class="action-badge" [ngClass]="'action-' + (log.action?.toLowerCase() || '')">
                  {{ getActionLabel(log.action) }}
                </span>
              </mat-cell>
            </ng-container>

            <ng-container matColumnDef="resourceType">
              <mat-header-cell *matHeaderCellDef>Loại</mat-header-cell>
              <mat-cell *matCellDef="let log">
                <span class="resource-badge">{{ getResourceLabel(log.resourceType) }}</span>
              </mat-cell>
            </ng-container>

            <ng-container matColumnDef="resourceId">
              <mat-header-cell *matHeaderCellDef>ID Tài nguyên</mat-header-cell>
              <mat-cell *matCellDef="let log" class="cell-mono">{{ log.resourceId }}</mat-cell>
            </ng-container>

            <ng-container matColumnDef="ipAddress">
              <mat-header-cell *matHeaderCellDef>Địa chỉ IP</mat-header-cell>
              <mat-cell *matCellDef="let log" class="cell-mono">{{ log.ipAddress }}</mat-cell>
            </ng-container>

            <!-- Expanded detail column -->
            <ng-container matColumnDef="expandedDetail">
              <mat-cell *matCellDef="let log" [attr.colspan]="displayedColumns.length">
                <div class="detail-row-inner" *ngIf="log === expandedLog">
                  <div class="detail-section">
                    <span class="detail-label">User-Agent:</span>
                    <span class="detail-value">{{ log.userAgent }}</span>
                  </div>
                  <div class="detail-section">
                    <span class="detail-label">Chi tiết:</span>
                    <pre class="detail-json">{{ log.details | json }}</pre>
                  </div>
                </div>
              </mat-cell>
            </ng-container>

            <mat-header-row *matHeaderRowDef="displayedColumns"></mat-header-row>
            <mat-row *matRowDef="let row; columns: displayedColumns;" class="clickable-row"
                     (click)="toggleRowExpansion(row)"></mat-row>
            <mat-row *matRowDef="let row; columns: ['expandedDetail']" class="detail-row"></mat-row>
          </mat-table>

          <ng-template #noData>
            <app-empty-state icon="receipt_long" title="Không có nhật ký nào"
                            message="Không tìm thấy bản ghi nhật ký phù hợp với bộ lọc">
            </app-empty-state>
          </ng-template>
        </ng-container>

        <mat-paginator *ngIf="totalCount > 0" [length]="totalCount" [pageSize]="pageSize"
                       [pageSizeOptions]="[5, 10, 20, 50]" [pageIndex]="currentPage - 1"
                       (page)="onPageChange($event)" showFirstLastButtons>
        </mat-paginator>
      </div>
    </div>
  `,
  styles: [`
    .audit-logs { padding: 24px; max-width: 1200px; margin: 0 auto; }
    .page-header { margin-bottom: 20px; }
    .page-header h1 { margin: 0; font-size: 24px; font-weight: 600; color: #1A1A1A; }
    .subtitle { margin: 4px 0 0; color: #787774; font-size: 14px; }

    .filter-bar { display: flex; flex-wrap: wrap; gap: 12px; margin-bottom: 20px; align-items: flex-start; }
    .filter-user { flex: 1; min-width: 200px; max-width: 280px; }
    .filter-action { width: 160px; }
    .filter-resource { width: 180px; }
    .filter-date { width: 170px; }
    .clear-btn { margin-top: 4px; }

    .table-container { background: #FFFFFF; border-radius: 8px; border: 1px solid #EAEAEA; overflow: hidden; }
    .audit-table { width: 100%; }
    .cell-mono { font-family: 'Roboto Mono', monospace; font-size: 12px; color: #787774; }

    .action-badge { display: inline-flex; padding: 2px 10px; border-radius: 12px; font-size: 11px; font-weight: 600; text-transform: uppercase; letter-spacing: 0.5px; }
    .action-create { background: #e8f5e9; color: #2F6B4A; }
    .action-read { background: #e3f2fd; color: #1565c0; }
    .action-update { background: #fff3e0; color: #e65100; }
    .action-delete { background: #fbe9e7; color: #c62828; }

    .resource-badge { display: inline-flex; padding: 2px 8px; border-radius: 4px; font-size: 11px; background: #f5f5f5; color: #666; }

    .clickable-row { cursor: pointer; }
    .clickable-row:hover { background: #F7F6F3; }
    .detail-row { background: #FAFAF8; }
    .detail-row > td { padding: 0; }

    .detail-row-inner { padding: 16px 24px; display: flex; flex-direction: column; gap: 12px; }
    .detail-section { display: flex; flex-direction: column; gap: 4px; }
    .detail-label { font-size: 12px; font-weight: 600; color: #787774; text-transform: uppercase; letter-spacing: 0.3px; }
    .detail-value { font-size: 13px; color: #1A1A1A; word-break: break-all; }
    .detail-json { background: #F7F6F3; padding: 12px; border-radius: 6px; font-size: 12px; line-height: 1.5; overflow-x: auto; max-height: 200px; margin: 0; }

    .mat-mdc-header-cell { font-weight: 600; color: #787774; font-size: 12px; text-transform: uppercase; letter-spacing: 0.5px; }
  `],
})
export class AuditLogsComponent implements OnInit, OnDestroy {
  private destroy$ = new Subject<void>();

  auditLogs: AuditLog[] = [];
  totalCount = 0;
  currentPage = 1;
  pageSize = 10;
  loading = true;
  expandedLog: AuditLog | null = null;

  userSearchControl = new FormControl('');
  actionFilter = new FormControl('');
  resourceFilter = new FormControl('');
  fromDateControl = new FormControl<Date | null>(null);
  toDateControl = new FormControl<Date | null>(null);

  displayedColumns = ['timestamp', 'userName', 'action', 'resourceType', 'resourceId', 'ipAddress'];
  actionOptions = ACTION_OPTIONS;
  resourceOptions = RESOURCE_OPTIONS;

  constructor(
    private adminService: AdminService,
    private snackBar: MatSnackBar,
    private cdr: ChangeDetectorRef,
  ) {}

  ngOnInit(): void {
    // Debounced user search
    this.userSearchControl.valueChanges
      .pipe(debounceTime(300), distinctUntilChanged(), takeUntil(this.destroy$))
      .subscribe(() => {
        this.currentPage = 1;
        this.loadAuditLogs();
      });

    // Filter changes
    this.actionFilter.valueChanges
      .pipe(takeUntil(this.destroy$))
      .subscribe(() => { this.currentPage = 1; this.loadAuditLogs(); });

    this.resourceFilter.valueChanges
      .pipe(takeUntil(this.destroy$))
      .subscribe(() => { this.currentPage = 1; this.loadAuditLogs(); });

    this.fromDateControl.valueChanges
      .pipe(debounceTime(300), distinctUntilChanged(), takeUntil(this.destroy$))
      .subscribe(() => { this.currentPage = 1; this.loadAuditLogs(); });

    this.toDateControl.valueChanges
      .pipe(debounceTime(300), distinctUntilChanged(), takeUntil(this.destroy$))
      .subscribe(() => { this.currentPage = 1; this.loadAuditLogs(); });

    this.loadAuditLogs();
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  loadAuditLogs(): void {
    this.loading = true;
    this.cdr.markForCheck();

    this.adminService.getAuditLogs({
      userId: this.userSearchControl.value || undefined,
      action: this.actionFilter.value || undefined,
      resourceType: this.resourceFilter.value || undefined,
      fromDate: this.fromDateControl.value?.toISOString(),
      toDate: this.toDateControl.value?.toISOString(),
      page: this.currentPage,
      pageSize: this.pageSize,
    })
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (result: PagedResult<AuditLog>) => {
          this.auditLogs = result.items;
          this.totalCount = result.totalCount;
          this.loading = false;
          this.cdr.markForCheck();
        },
        error: () => {
          this.loading = false;
          this.snackBar.open('Không thể tải nhật ký truy cập', 'Đóng', { duration: 5000 });
          this.cdr.markForCheck();
        },
      });
  }

  onPageChange(event: PageEvent): void {
    this.currentPage = event.pageIndex + 1;
    this.pageSize = event.pageSize;
    this.loadAuditLogs();
  }

  clearFilters(): void {
    this.userSearchControl.setValue('');
    this.actionFilter.setValue('');
    this.resourceFilter.setValue('');
    this.fromDateControl.setValue(null);
    this.toDateControl.setValue(null);
  }

  toggleRowExpansion(log: AuditLog): void {
    this.expandedLog = this.expandedLog?.id === log.id ? null : log;
    this.cdr.markForCheck();
  }

  getActionLabel(action: string): string {
    const labels: Record<string, string> = { CREATE: 'Tạo', READ: 'Xem', UPDATE: 'Sửa', DELETE: 'Xóa' };
    return labels[action] || action;
  }

  getResourceLabel(resourceType: string): string {
    const found = RESOURCE_OPTIONS.find((r) => r.value === resourceType);
    return found ? found.label : resourceType;
  }
}
