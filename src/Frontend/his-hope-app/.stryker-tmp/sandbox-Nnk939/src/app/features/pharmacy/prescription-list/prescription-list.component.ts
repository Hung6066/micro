// @ts-nocheck
import { Component, OnInit, OnDestroy, ChangeDetectionStrategy, ChangeDetectorRef } from '@angular/core';
import { Router } from '@angular/router';
import { FormControl } from '@angular/forms';
import { Subject, debounceTime, distinctUntilChanged, takeUntil } from 'rxjs';
import { PharmacyService } from '@core/services/pharmacy.service';
import { Prescription, PrescriptionStatus } from '@core/models/prescription.model';

@Component({
  selector: 'app-prescription-list',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="prescription-list">
      <div class="header">
        <h1>Danh sách đơn thuốc</h1>
        <button mat-raised-button color="primary" routerLink="/pharmacy/prescriptions/new"
                attr.aria-label="Tạo đơn thuốc mới">
          <mat-icon>add</mat-icon> Tạo đơn thuốc
        </button>
      </div>

      <div class="filters">
        <mat-form-field appearance="outline" class="search-field">
          <mat-label>Tìm kiếm</mat-label>
          <input matInput [formControl]="searchControl" placeholder="Mã đơn, tên thuốc, bệnh nhân..."
                 aria-label="Tìm kiếm đơn thuốc">
          <mat-icon matPrefix>search</mat-icon>
        </mat-form-field>

        <mat-form-field appearance="outline" class="status-filter">
          <mat-label>Trạng thái</mat-label>
          <mat-select [formControl]="statusControl" aria-label="Lọc theo trạng thái">
            <mat-option value="">Tất cả</mat-option>
            <mat-option value="active">Đang hoạt động</mat-option>
            <mat-option value="filled">Đã cấp phát</mat-option>
            <mat-option value="partially_filled">Cấp phát một phần</mat-option>
            <mat-option value="cancelled">Đã hủy</mat-option>
            <mat-option value="expired">Hết hạn</mat-option>
          </mat-select>
        </mat-form-field>
      </div>

      <div class="loading-shimmer" *ngIf="loading">
        <mat-progress-bar mode="indeterminate" aria-label="Đang tải"></mat-progress-bar>
      </div>

      <div *ngIf="!loading && prescriptions.length === 0" class="empty-state">
        <mat-icon class="empty-icon">description</mat-icon>
        <p>Không tìm thấy đơn thuốc nào.</p>
      </div>

      <mat-table [dataSource]="prescriptions" class="mat-elevation-z2" *ngIf="!loading && prescriptions.length > 0">
        <ng-container matColumnDef="id">
          <mat-header-cell *matHeaderCellDef>Mã đơn</mat-header-cell>
          <mat-cell *matCellDef="let p">{{ p.id | slice:0:8 }}...</mat-cell>
        </ng-container>

        <ng-container matColumnDef="medicationName">
          <mat-header-cell *matHeaderCellDef>Thuốc</mat-header-cell>
          <mat-cell *matCellDef="let p">{{ p.medicationName }}</mat-cell>
        </ng-container>

        <ng-container matColumnDef="strength">
          <mat-header-cell *matHeaderCellDef>Hàm lượng</mat-header-cell>
          <mat-cell *matCellDef="let p">{{ p.strength }}</mat-cell>
        </ng-container>

        <ng-container matColumnDef="quantity">
          <mat-header-cell *matHeaderCellDef>Số lượng</mat-header-cell>
          <mat-cell *matCellDef="let p">{{ p.quantity }}</mat-cell>
        </ng-container>

        <ng-container matColumnDef="prescribedAt">
          <mat-header-cell *matHeaderCellDef>Ngày kê</mat-header-cell>
          <mat-cell *matCellDef="let p">{{ p.prescribedAt | date:'mediumDate' }}</mat-cell>
        </ng-container>

        <ng-container matColumnDef="statusCode">
          <mat-header-cell *matHeaderCellDef>Trạng thái</mat-header-cell>
          <mat-cell *matCellDef="let p">
            <span class="status-badge" [class.status-active]="p.statusCode === 'active'"
                  [class.status-filled]="p.statusCode === 'filled'"
                  [class.status-partial]="p.statusCode === 'partially_filled'"
                  [class.status-cancelled]="p.statusCode === 'cancelled'"
                  [class.status-expired]="p.statusCode === 'expired'">
              {{ p.statusName }}
            </span>
          </mat-cell>
        </ng-container>

        <ng-container matColumnDef="actions">
          <mat-header-cell *matHeaderCellDef>Thao tác</mat-header-cell>
          <mat-cell *matCellDef="let p">
            <button mat-icon-button color="primary" (click)="viewDetail(p.id)"
                    attr.aria-label="Xem chi tiết đơn thuốc">
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
    .prescription-list { padding: 24px; }
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
export class PrescriptionListComponent implements OnInit, OnDestroy {
  private destroy$ = new Subject<void>();

  displayedColumns = ['id', 'medicationName', 'strength', 'quantity', 'prescribedAt', 'statusCode', 'actions'];
  prescriptions: Prescription[] = [];
  totalCount = 0;
  page = 1;
  pageSize = 20;
  loading = false;
  searchControl = new FormControl('');
  statusControl = new FormControl('');
  private searchTerm = '';

  constructor(
    private pharmacyService: PharmacyService,
    private router: Router,
    private cdr: ChangeDetectorRef,
  ) {}

  ngOnInit(): void {
    this.searchControl.valueChanges
      .pipe(debounceTime(300), distinctUntilChanged(), takeUntil(this.destroy$))
      .subscribe((term) => {
        this.searchTerm = term ?? '';
        this.page = 1;
        this.loadPrescriptions();
        this.cdr.markForCheck();
      });

    this.statusControl.valueChanges
      .pipe(takeUntil(this.destroy$))
      .subscribe(() => {
        this.page = 1;
        this.loadPrescriptions();
        this.cdr.markForCheck();
      });

    this.loadPrescriptions();
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  loadPrescriptions(): void {
    this.loading = true;
    this.pharmacyService.searchPrescriptions({
      searchTerm: this.searchTerm,
      statusCode: this.statusControl.value || undefined,
      page: this.page,
      pageSize: this.pageSize,
    })
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (result) => {
          this.prescriptions = result.items;
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
    this.router.navigate(['/pharmacy/prescriptions', id]);
  }

  onPageChange(event: any): void {
    this.page = event.pageIndex + 1;
    this.pageSize = event.pageSize;
    this.loadPrescriptions();
  }
}
