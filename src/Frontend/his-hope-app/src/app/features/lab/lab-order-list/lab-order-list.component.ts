import { Component, OnInit, OnDestroy, ChangeDetectionStrategy, ChangeDetectorRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterModule } from '@angular/router';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { Subject, debounceTime, distinctUntilChanged, takeUntil } from 'rxjs';
import { MatTableModule } from '@angular/material/table';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatIconModule } from '@angular/material/icon';
import { MatSelectModule } from '@angular/material/select';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatPaginatorModule } from '@angular/material/paginator';
import { MatButtonModule } from '@angular/material/button';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { LabService } from '@core/services/lab.service';
import { LabCriticalAlertStreamService } from '@core/services/lab-critical-alert-stream.service';
import { LabOrder } from '@core/models/lab-order.model';

@Component({
    selector: 'app-lab-order-list',
    standalone: true,
    imports: [
        CommonModule, RouterModule, ReactiveFormsModule,
        MatTableModule, MatFormFieldModule, MatInputModule, MatIconModule,
        MatSelectModule, MatProgressBarModule, MatPaginatorModule, MatButtonModule, MatSnackBarModule,
    ],
    changeDetection: ChangeDetectionStrategy.OnPush,
    template: `
    <div class="lab-order-list">
      <div class="header">
        <h1>Phiếu xét nghiệm</h1>
        <div class="header-actions">
          <div class="critical-badge" aria-live="polite">{{ unreadCriticalAlertCount }} cảnh báo mới</div>
          <a mat-stroked-button routerLink="/lab/critical-alerts">Hộp cảnh báo</a>
          <button mat-raised-button color="primary" routerLink="/lab/new"
                  aria-label="Tạo phiếu xét nghiệm mới">
            <mat-icon>add</mat-icon> Tạo phiếu xét nghiệm
          </button>
        </div>
      </div>

      <div class="filters">
        <mat-form-field appearance="outline" class="search-field">
          <mat-label>Tìm kiếm</mat-label>
          <input matInput [formControl]="searchControl" placeholder="Mã phiếu, bệnh nhân..."
                 aria-label="Tìm kiếm phiếu xét nghiệm">
          <mat-icon matPrefix>search</mat-icon>
        </mat-form-field>

        <mat-form-field appearance="outline" class="status-filter">
          <mat-label>Trạng thái</mat-label>
          <mat-select [formControl]="statusControl" aria-label="Lọc theo trạng thái">
            <mat-option value="">Tất cả</mat-option>
            <mat-option value="ordered">Đã chỉ định</mat-option>
            <mat-option value="specimen_collected">Đã lấy mẫu</mat-option>
            <mat-option value="in_progress">Đang xử lý</mat-option>
            <mat-option value="completed">Hoàn thành</mat-option>
            <mat-option value="cancelled">Đã hủy</mat-option>
          </mat-select>
        </mat-form-field>
      </div>

      @if (loading) {
      <div class="loading-shimmer">
        <mat-progress-bar mode="indeterminate" aria-label="Đang tải"></mat-progress-bar>
      </div>
      }

      @if (!loading && labOrders.length === 0) {
      <div class="empty-state">
        <mat-icon class="empty-icon">biotech</mat-icon>
        <p>Không tìm thấy phiếu xét nghiệm nào.</p>
      </div>
      }

      @if (!loading && labOrders.length > 0) {
      <mat-table [dataSource]="labOrders" class="mat-elevation-z2">
        <ng-container matColumnDef="id">
          <mat-header-cell *matHeaderCellDef>Mã phiếu</mat-header-cell>
          <mat-cell *matCellDef="let o">{{ o.id | slice:0:8 }}...</mat-cell>
        </ng-container>

        <ng-container matColumnDef="patientName">
          <mat-header-cell *matHeaderCellDef>Bệnh nhân</mat-header-cell>
          <mat-cell *matCellDef="let o">{{ o.patientName || o.patientId }}</mat-cell>
        </ng-container>

        <ng-container matColumnDef="orderDate">
          <mat-header-cell *matHeaderCellDef>Ngày chỉ định</mat-header-cell>
          <mat-cell *matCellDef="let o">{{ o.orderDate | date:'mediumDate' }}</mat-cell>
        </ng-container>

        <ng-container matColumnDef="priorityName">
          <mat-header-cell *matHeaderCellDef>Mức ưu tiên</mat-header-cell>
          <mat-cell *matCellDef="let o">
            <span class="priority-badge" [class.priority-high]="o.priorityCode === 'high'"
                  [class.priority-urgent]="o.priorityCode === 'urgent'"
                  [class.priority-routine]="o.priorityCode === 'routine'">
              {{ o.priorityName }}
            </span>
          </mat-cell>
        </ng-container>

        <ng-container matColumnDef="testsCount">
          <mat-header-cell *matHeaderCellDef>Số xét nghiệm</mat-header-cell>
          <mat-cell *matCellDef="let o">{{ o.tests?.length || 0 }}</mat-cell>
        </ng-container>

        <ng-container matColumnDef="statusCode">
          <mat-header-cell *matHeaderCellDef>Trạng thái</mat-header-cell>
          <mat-cell *matCellDef="let o">
            <span class="status-badge" [class.status-ordered]="o.statusCode === 'ordered'"
                  [class.status-collected]="o.statusCode === 'specimen_collected'"
                  [class.status-progress]="o.statusCode === 'in_progress'"
                  [class.status-completed]="o.statusCode === 'completed'"
                  [class.status-cancelled]="o.statusCode === 'cancelled'">
              {{ o.statusName }}
            </span>
          </mat-cell>
        </ng-container>

        <ng-container matColumnDef="actions">
          <mat-header-cell *matHeaderCellDef>Thao tác</mat-header-cell>
          <mat-cell *matCellDef="let o">
            <button mat-icon-button color="primary" (click)="viewDetail(o.id)"
                    aria-label="Xem chi tiết phiếu xét nghiệm">
              <mat-icon>visibility</mat-icon>
            </button>
          </mat-cell>
        </ng-container>

        <mat-header-row *matHeaderRowDef="displayedColumns"></mat-header-row>
        <mat-row *matRowDef="let row; columns: displayedColumns;" (click)="viewDetail(row.id)" class="clickable-row"></mat-row>
      </mat-table>
      }

      @if (!loading && totalCount > 0) {
      <mat-paginator [length]="totalCount" [pageSize]="pageSize" [pageSizeOptions]="[10, 20, 50]"
                     (page)="onPageChange($event)" [pageIndex]="page - 1" showFirstLastButtons>
      </mat-paginator>
      }
    </div>
  `,
    styles: [`
    .lab-order-list { padding: 24px; }
    .header { display: flex; justify-content: space-between; align-items: center; margin-bottom: 24px; }
    .header-actions { display: flex; align-items: center; gap: 12px; flex-wrap: wrap; }
    .filters { display: flex; gap: 16px; margin-bottom: 20px; flex-wrap: wrap; }
    .search-field { flex: 1; min-width: 250px; }
    .status-filter { width: 220px; }
    .critical-badge { color: #2F6B4A; border: 1px solid #EAEAEA; border-radius: 4px; padding: 8px 12px; background: #FFFFFF; }
    mat-table { width: 100%; cursor: pointer; }
    mat-row:hover { background: #f5f5f5; }
    .clickable-row { cursor: pointer; }

    .loading-shimmer { margin-bottom: 16px; }
    .empty-state { text-align: center; padding: 48px; color: #999; }
    .empty-icon { font-size: 48px; width: 48px; height: 48px; margin-bottom: 16px; }
  `],
})
export class LabOrderListComponent implements OnInit, OnDestroy {
  private destroy$ = new Subject<void>();

  displayedColumns = ['id', 'patientName', 'orderDate', 'priorityName', 'testsCount', 'statusCode', 'actions'];
  labOrders: LabOrder[] = [];
  totalCount = 0;
  page = 1;
  pageSize = 20;
  loading = false;
  searchControl = new FormControl('');
  statusControl = new FormControl('');
  private searchTerm = '';
  unreadCriticalAlertCount = 0;
  private lastToastAlertId: string | null = null;

  constructor(
    private labService: LabService,
    private router: Router,
    private cdr: ChangeDetectorRef,
    private criticalAlertStreamService: LabCriticalAlertStreamService,
    private snackBar: MatSnackBar,
  ) {}

  ngOnInit(): void {
    void this.criticalAlertStreamService.connect();

    this.criticalAlertStreamService.unreadCount$
      .pipe(takeUntil(this.destroy$))
      .subscribe((count) => {
        this.unreadCriticalAlertCount = count;
        this.cdr.markForCheck();
      });

    this.criticalAlertStreamService.latestAlert$
      .pipe(takeUntil(this.destroy$))
      .subscribe((alert) => {
        if (!alert || alert.id === this.lastToastAlertId) {
          return;
        }

        this.lastToastAlertId = alert.id;
        this.snackBar.open('Có cảnh báo xét nghiệm nghiêm trọng mới', 'Đóng', { duration: 3000 });
        this.cdr.markForCheck();
      });

    this.searchControl.valueChanges
      .pipe(debounceTime(300), distinctUntilChanged(), takeUntil(this.destroy$))
      .subscribe((term) => {
        this.searchTerm = term ?? '';
        this.page = 1;
        this.loadLabOrders();
        this.cdr.markForCheck();
      });

    this.statusControl.valueChanges
      .pipe(takeUntil(this.destroy$))
      .subscribe(() => {
        this.page = 1;
        this.loadLabOrders();
        this.cdr.markForCheck();
      });

    this.loadLabOrders();
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
    void this.criticalAlertStreamService.disconnect();
  }

  loadLabOrders(): void {
    this.loading = true;
    this.labService.searchLabOrders({
      searchTerm: this.searchTerm,
      statusCode: this.statusControl.value || undefined,
      page: this.page,
      pageSize: this.pageSize,
    })
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (result) => {
          this.labOrders = result.items;
          this.totalCount = result.totalCount;
          this.loading = false;
          this.cdr.markForCheck();
        },
        error: () => {
          this.loading = false;
          this.cdr.markForCheck();
        },
      });
  }

  viewDetail(id: string): void {
    this.router.navigate(['/lab', id]);
  }

  onPageChange(event: any): void {
    this.page = event.pageIndex + 1;
    this.pageSize = event.pageSize;
    this.loadLabOrders();
  }
}
