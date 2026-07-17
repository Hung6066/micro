// @ts-nocheck
import { Component, OnInit, OnDestroy, ChangeDetectionStrategy, ChangeDetectorRef } from '@angular/core';
import { Router } from '@angular/router';
import { FormControl } from '@angular/forms';
import { Subject, debounceTime, distinctUntilChanged, takeUntil } from 'rxjs';
import { LabService } from '@core/services/lab.service';
import { LabOrder } from '@core/models/lab-order.model';

@Component({
  selector: 'app-lab-order-list',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="lab-order-list">
      <div class="header">
        <h1>Phiếu xét nghiệm</h1>
        <button mat-raised-button color="primary" routerLink="/lab/new"
                attr.aria-label="Tạo phiếu xét nghiệm mới">
          <mat-icon>add</mat-icon> Tạo phiếu xét nghiệm
        </button>
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

      <div class="loading-shimmer" *ngIf="loading">
        <mat-progress-bar mode="indeterminate" aria-label="Đang tải"></mat-progress-bar>
      </div>

      <div *ngIf="!loading && labOrders.length === 0" class="empty-state">
        <mat-icon class="empty-icon">biotech</mat-icon>
        <p>Không tìm thấy phiếu xét nghiệm nào.</p>
      </div>

      <mat-table [dataSource]="labOrders" class="mat-elevation-z2" *ngIf="!loading && labOrders.length > 0">
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
                    attr.aria-label="Xem chi tiết phiếu xét nghiệm">
              <mat-icon>visibility</mat-icon>
            </button>
          </mat-cell>
        </ng-container>

        <mat-header-row *matHeaderRowDef="displayedColumns"></mat-header-row>
        <mat-row *matRowDef="let row; columns: displayedColumns;" (click)="viewDetail(row.id)" class="clickable-row"></mat-row>
      </mat-table>

      <mat-paginator [length]="totalCount" [pageSize]="pageSize" [pageSizeOptions]="[10, 20, 50]"
                     (page)="onPageChange($event)" [pageIndex]="page - 1" showFirstLastButtons
                     *ngIf="!loading && totalCount > 0">
      </mat-paginator>
    </div>
  `,
  styles: [`
    .lab-order-list { padding: 24px; }
    .header { display: flex; justify-content: space-between; align-items: center; margin-bottom: 24px; }
    .filters { display: flex; gap: 16px; margin-bottom: 20px; flex-wrap: wrap; }
    .search-field { flex: 1; min-width: 250px; }
    .status-filter { width: 220px; }
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

  constructor(
    private labService: LabService,
    private router: Router,
    private cdr: ChangeDetectorRef,
  ) {}

  ngOnInit(): void {
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
