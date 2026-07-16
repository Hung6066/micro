import { Component, OnInit, OnDestroy, ChangeDetectionStrategy, ChangeDetectorRef } from '@angular/core';
import { Router } from '@angular/router';
import { FormControl } from '@angular/forms';
import { Subject, debounceTime, distinctUntilChanged, takeUntil } from 'rxjs';
import { BillingService } from '@core/services/billing.service';
import { Invoice } from '@core/models/invoice.model';

@Component({
  selector: 'app-invoice-list',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="invoice-list">
      <div class="header">
        <h1>Hóa đơn thanh toán</h1>
        <button mat-raised-button color="primary" routerLink="/billing/new"
                attr.aria-label="Tạo hóa đơn mới">
          <mat-icon>add</mat-icon> Tạo hóa đơn
        </button>
      </div>

      <div class="filters">
        <mat-form-field appearance="outline" class="search-field">
          <mat-label>Tìm kiếm</mat-label>
          <input matInput [formControl]="searchControl" placeholder="Số hóa đơn, bệnh nhân..."
                 aria-label="Tìm kiếm hóa đơn">
          <mat-icon matPrefix>search</mat-icon>
        </mat-form-field>

        <mat-form-field appearance="outline" class="status-filter">
          <mat-label>Trạng thái</mat-label>
          <mat-select [formControl]="statusControl" aria-label="Lọc theo trạng thái">
            <mat-option value="">Tất cả</mat-option>
            <mat-option value="draft">Nháp</mat-option>
            <mat-option value="issued">Đã phát hành</mat-option>
            <mat-option value="partially_paid">Đã thanh toán một phần</mat-option>
            <mat-option value="paid">Đã thanh toán</mat-option>
            <mat-option value="overdue">Quá hạn</mat-option>
            <mat-option value="cancelled">Đã hủy</mat-option>
            <mat-option value="voided">Vô hiệu</mat-option>
          </mat-select>
        </mat-form-field>
      </div>

      <div class="loading-shimmer" *ngIf="loading">
        <mat-progress-bar mode="indeterminate" aria-label="Đang tải"></mat-progress-bar>
      </div>

      <div *ngIf="!loading && invoices.length === 0" class="empty-state">
        <mat-icon class="empty-icon">receipt</mat-icon>
        <p>Không tìm thấy hóa đơn nào.</p>
      </div>

      <mat-table [dataSource]="invoices" class="mat-elevation-z2" *ngIf="!loading && invoices.length > 0">
        <ng-container matColumnDef="invoiceNumber">
          <mat-header-cell *matHeaderCellDef>Số hóa đơn</mat-header-cell>
          <mat-cell *matCellDef="let inv">{{ inv.invoiceNumber }}</mat-cell>
        </ng-container>

        <ng-container matColumnDef="patientName">
          <mat-header-cell *matHeaderCellDef>Bệnh nhân</mat-header-cell>
          <mat-cell *matCellDef="let inv">{{ inv.patientName || inv.patientId }}</mat-cell>
        </ng-container>

        <ng-container matColumnDef="invoiceDate">
          <mat-header-cell *matHeaderCellDef>Ngày</mat-header-cell>
          <mat-cell *matCellDef="let inv">{{ inv.invoiceDate | date:'mediumDate' }}</mat-cell>
        </ng-container>

        <ng-container matColumnDef="totalAmount">
          <mat-header-cell *matHeaderCellDef>Tổng tiền</mat-header-cell>
          <mat-cell *matCellDef="let inv">{{ inv.totalAmount | number:'1.0-0' }} đ</mat-cell>
        </ng-container>

        <ng-container matColumnDef="balanceDue">
          <mat-header-cell *matHeaderCellDef>Còn nợ</mat-header-cell>
          <mat-cell *matCellDef="let inv">
            <span [class.text-danger]="inv.balanceDue > 0" [class.text-success]="inv.balanceDue === 0">
              {{ inv.balanceDue | number:'1.0-0' }} đ
            </span>
          </mat-cell>
        </ng-container>

        <ng-container matColumnDef="statusCode">
          <mat-header-cell *matHeaderCellDef>Trạng thái</mat-header-cell>
          <mat-cell *matCellDef="let inv">
            <span class="status-badge" [class.status-draft]="inv.statusCode === 'draft'"
                  [class.status-issued]="inv.statusCode === 'issued'"
                  [class.status-partial]="inv.statusCode === 'partially_paid'"
                  [class.status-paid]="inv.statusCode === 'paid'"
                  [class.status-overdue]="inv.statusCode === 'overdue'"
                  [class.status-cancelled]="inv.statusCode === 'cancelled'"
                  [class.status-voided]="inv.statusCode === 'voided'">
              {{ inv.statusName }}
            </span>
          </mat-cell>
        </ng-container>

        <ng-container matColumnDef="actions">
          <mat-header-cell *matHeaderCellDef>Thao tác</mat-header-cell>
          <mat-cell *matCellDef="let inv">
            <button mat-icon-button color="primary" (click)="viewDetail(inv.id)"
                    attr.aria-label="Xem chi tiết hóa đơn">
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
    .invoice-list { padding: 24px; }
    .header { display: flex; justify-content: space-between; align-items: center; margin-bottom: 24px; }
    .filters { display: flex; gap: 16px; margin-bottom: 20px; flex-wrap: wrap; }
    .search-field { flex: 1; min-width: 250px; }
    .status-filter { width: 220px; }
    mat-table { width: 100%; cursor: pointer; }
    mat-row:hover { background: #f5f5f5; }
    .clickable-row { cursor: pointer; }
    .text-danger { color: var(--pastel-red-text, #C25450); font-weight: 500; }
    .text-success { color: var(--pastel-green-text, #2F6B4A); font-weight: 500; }
    .loading-shimmer { margin-bottom: 16px; }
    .empty-state { text-align: center; padding: 48px; color: #999; }
    .empty-icon { font-size: 48px; width: 48px; height: 48px; margin-bottom: 16px; }
  `],
})
export class InvoiceListComponent implements OnInit, OnDestroy {
  private destroy$ = new Subject<void>();

  displayedColumns = ['invoiceNumber', 'patientName', 'invoiceDate', 'totalAmount', 'balanceDue', 'statusCode', 'actions'];
  invoices: Invoice[] = [];
  totalCount = 0;
  page = 1;
  pageSize = 20;
  loading = false;
  searchControl = new FormControl('');
  statusControl = new FormControl('');
  private searchTerm = '';

  constructor(
    private billingService: BillingService,
    private router: Router,
    private cdr: ChangeDetectorRef,
  ) {}

  ngOnInit(): void {
    this.searchControl.valueChanges
      .pipe(debounceTime(300), distinctUntilChanged(), takeUntil(this.destroy$))
      .subscribe((term) => {
        this.searchTerm = term ?? '';
        this.page = 1;
        this.loadInvoices();
        this.cdr.markForCheck();
      });

    this.statusControl.valueChanges
      .pipe(takeUntil(this.destroy$))
      .subscribe(() => {
        this.page = 1;
        this.loadInvoices();
        this.cdr.markForCheck();
      });

    this.loadInvoices();
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  loadInvoices(): void {
    this.loading = true;
    this.billingService.searchInvoices({
      searchTerm: this.searchTerm,
      statusCode: this.statusControl.value || undefined,
      page: this.page,
      pageSize: this.pageSize,
    })
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (result) => {
          this.invoices = result.items;
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
    this.router.navigate(['/billing', id]);
  }

  onPageChange(event: any): void {
    this.page = event.pageIndex + 1;
    this.pageSize = event.pageSize;
    this.loadInvoices();
  }
}
