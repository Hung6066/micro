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
  templateUrl: './audit-logs.component.html',
  styleUrls: ['./audit-logs.component.scss'],
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
