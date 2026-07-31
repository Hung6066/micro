import { Component, OnInit, OnDestroy, ChangeDetectionStrategy, ChangeDetectorRef } from '@angular/core';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { Subject, debounceTime, distinctUntilChanged, takeUntil } from 'rxjs';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatNativeDateModule } from '@angular/material/core';
import { MatExpansionModule } from '@angular/material/expansion';
import { CommonModule } from '@angular/common';
import { AdminService } from '@core/services/admin.service';
import { AuditLog } from '@core/models/admin.model';
import { PagedResult } from '@core/models/paged-result.model';
import {
  HisHopeDataTableColumn,
  HisHopeDataTableComponent,
  HisHopeDataTableCellDirective,
  HisHopeDataTableDetailDirective,
  HisHopePageHeaderComponent,
  HisHopePageLayoutComponent,
  HisHopePageQuery,
  HisHopeTranslatePipe,
} from '@his-hope/frontend-foundation';

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
    MatSnackBarModule, MatButtonModule,
    MatIconModule, MatFormFieldModule, MatInputModule, MatSelectModule,
    MatDatepickerModule, MatNativeDateModule,
    MatExpansionModule,
    HisHopeDataTableComponent, HisHopeDataTableCellDirective, HisHopeDataTableDetailDirective,
    HisHopePageHeaderComponent, HisHopePageLayoutComponent, HisHopeTranslatePipe,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './audit-logs.component.html',
  styleUrls: ['./audit-logs.component.scss'],
})
export class AuditLogsComponent implements OnInit, OnDestroy {
  private destroy$ = new Subject<void>();

  auditLogs: AuditLog[] = [];
  totalCount = 0;
  loading = true;
  error: string | null = null;
  query: HisHopePageQuery = { page: 1, pageSize: 20 };
  expandedRowKeys: string[] = [];

  userSearchControl = new FormControl('');
  actionFilter = new FormControl('');
  resourceFilter = new FormControl('');
  fromDateControl = new FormControl<Date | null>(null);
  toDateControl = new FormControl<Date | null>(null);

  readonly columns: HisHopeDataTableColumn[] = [
    { key: 'timestamp', label: 'Thời gian' },
    { key: 'userName', label: 'Người dùng' },
    { key: 'action', label: 'Hành động' },
    { key: 'resourceType', label: 'Loại' },
    { key: 'resourceId', label: 'ID Tài nguyên' },
    { key: 'ipAddress', label: 'Địa chỉ IP' },
  ];
  rows: Record<string, unknown>[] = [];
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
        this.query = { ...this.query, page: 1 };
        this.loadAuditLogs();
      });

    // Filter changes
    this.actionFilter.valueChanges
      .pipe(takeUntil(this.destroy$))
      .subscribe(() => { this.query = { ...this.query, page: 1 }; this.loadAuditLogs(); });

    this.resourceFilter.valueChanges
      .pipe(takeUntil(this.destroy$))
      .subscribe(() => { this.query = { ...this.query, page: 1 }; this.loadAuditLogs(); });

    this.fromDateControl.valueChanges
      .pipe(debounceTime(300), distinctUntilChanged(), takeUntil(this.destroy$))
      .subscribe(() => { this.query = { ...this.query, page: 1 }; this.loadAuditLogs(); });

    this.toDateControl.valueChanges
      .pipe(debounceTime(300), distinctUntilChanged(), takeUntil(this.destroy$))
      .subscribe(() => { this.query = { ...this.query, page: 1 }; this.loadAuditLogs(); });

    this.loadAuditLogs();
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  loadAuditLogs(): void {
    this.loading = true;
    this.error = null;
    this.cdr.markForCheck();

    this.adminService.getAuditLogs({
      userId: this.userSearchControl.value || undefined,
      action: this.actionFilter.value || undefined,
      resourceType: this.resourceFilter.value || undefined,
      fromDate: this.fromDateControl.value?.toISOString(),
      toDate: this.toDateControl.value?.toISOString(),
      page: this.query.page,
      pageSize: this.query.pageSize,
    })
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (result: PagedResult<AuditLog>) => {
          this.auditLogs = result.items;
          this.totalCount = result.totalCount;
          this.rows = result.items.map((log) => ({
            id: log.id,
            timestamp: log.timestamp,
            userName: log.userName,
            action: log.action,
            actionLabel: this.getActionLabel(log.action),
            resourceType: log.resourceType,
            resourceLabel: this.getResourceLabel(log.resourceType),
            resourceId: log.resourceId,
            ipAddress: log.ipAddress,
            userAgent: log.userAgent,
            details: log.details,
          }));
          this.loading = false;
          this.cdr.markForCheck();
        },
        error: () => {
          this.loading = false;
          this.error = 'Không thể tải nhật ký truy cập';
          this.snackBar.open('Không thể tải nhật ký truy cập', 'Đóng', { duration: 5000 });
          this.cdr.markForCheck();
        },
      });
  }

  onQueryChange(query: HisHopePageQuery): void {
    this.query = query;
    this.loadAuditLogs();
  }

  onRowExpand(event: { rowKey: string; expanded: boolean }): void {
    this.expandedRowKeys = event.expanded
      ? [...this.expandedRowKeys, event.rowKey]
      : this.expandedRowKeys.filter((key) => key !== event.rowKey);
  }

  clearFilters(): void {
    this.userSearchControl.setValue('');
    this.actionFilter.setValue('');
    this.resourceFilter.setValue('');
    this.fromDateControl.setValue(null);
    this.toDateControl.setValue(null);
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
