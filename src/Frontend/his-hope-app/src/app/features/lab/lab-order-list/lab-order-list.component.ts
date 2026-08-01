import { Component, OnInit, OnDestroy, ChangeDetectionStrategy, ChangeDetectorRef, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterModule } from '@angular/router';
import { Subject, takeUntil } from 'rxjs';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { LabService } from '@core/services/lab.service';
import { LabCriticalAlertStreamService } from '@core/services/lab-critical-alert-stream.service';
import {
  HisHopeDataTableCellDirective,
  HisHopeDataTableColumn,
  HisHopeDataTableComponent,
  HisHopeI18nService,
  HisHopePageHeaderComponent,
  HisHopePageLayoutComponent,
  HisHopePageQuery,
  HisHopeStatusBadgeComponent,
  HisHopeToolbarComponent,
  HisHopeTranslatePipe,
} from '@his-hope/frontend-foundation';

@Component({
    selector: 'app-lab-order-list',
    standalone: true,
    imports: [
        CommonModule, RouterModule,
        MatIconModule, MatButtonModule, MatSnackBarModule,
        HisHopeDataTableComponent, HisHopeDataTableCellDirective, HisHopePageHeaderComponent,
        HisHopePageLayoutComponent, HisHopeToolbarComponent, HisHopeStatusBadgeComponent, HisHopeTranslatePipe,
    ],
    changeDetection: ChangeDetectionStrategy.OnPush,
    template: `
    <hh-page-layout>
      <hh-page-header hhPageHeader
                      [title]="'labOrders.title' | hhTranslate:'Phiếu xét nghiệm'"
                      [subtitle]="'labOrders.subtitle' | hhTranslate:'Quản lý phiếu xét nghiệm và mẫu bệnh phẩm'">
        <span class="critical-badge" aria-live="polite">{{ unreadCriticalAlertCount }} {{ 'labOrders.criticalAlert' | hhTranslate:'cảnh báo mới' }}</span>
        <a mat-stroked-button routerLink="/lab/critical-alerts">{{ 'labOrders.alertBox' | hhTranslate:'Hộp cảnh báo' }}</a>
        <button mat-raised-button color="primary" routerLink="/lab/new"
                [attr.aria-label]="'labOrders.new' | hhTranslate:'Tạo phiếu xét nghiệm mới'">
          <mat-icon>add</mat-icon> {{ 'labOrders.new' | hhTranslate:'Tạo phiếu xét nghiệm' }}
        </button>
      </hh-page-header>

      <hh-toolbar hhPageToolbar [label]="'labOrders.toolbar' | hhTranslate:'Phiếu xét nghiệm'">
        <span hhToolbarTitle>{{ totalItems }} {{ 'labOrders.count' | hhTranslate:'phiếu xét nghiệm' }}</span>
        <label class="hh-status-filter" hh-toolbar-controls>
          <span>{{ 'common.status' | hhTranslate:'Trạng thái' }}</span>
          <select [value]="statusCode" (change)="onStatusFilterChange($event)"
                  [attr.aria-label]="'labOrders.statusFilter' | hhTranslate:'Lọc theo trạng thái'">
            <option value="">{{ 'common.all' | hhTranslate:'Tất cả' }}</option>
            <option value="ordered">{{ 'labOrders.status.ordered' | hhTranslate:'Đã chỉ định' }}</option>
            <option value="specimen_collected">{{ 'labOrders.status.collected' | hhTranslate:'Đã lấy mẫu' }}</option>
            <option value="in_progress">{{ 'labOrders.status.inProgress' | hhTranslate:'Đang xử lý' }}</option>
            <option value="completed">{{ 'labOrders.status.completed' | hhTranslate:'Hoàn thành' }}</option>
            <option value="cancelled">{{ 'labOrders.status.cancelled' | hhTranslate:'Đã hủy' }}</option>
          </select>
        </label>
      </hh-toolbar>

      <hh-data-table [label]="'labOrders.tableLabel' | hhTranslate:'Phiếu xét nghiệm'"
                     [loading]="loading" [error]="error"
                     [empty]="rows.length === 0 && !loading"
                     [emptyMessage]="'labOrders.empty' | hhTranslate:'Không tìm thấy phiếu xét nghiệm nào.'"
                     [searchPlaceholder]="'labOrders.search' | hhTranslate:'Mã phiếu, bệnh nhân...'"
                     [columns]="columns" [rows]="rows"
                     [pageSize]="20" [totalItems]="totalItems"
                     [query]="query" [mode]="'server'" [urlSync]="false"
                     [selection]="true" [rowClickable]="true"
                     (queryChange)="onQueryChange($event)" (rowClick)="onRowClick($event)"
                     (retry)="loadData()">
        <ng-template hhDataTableCell="id" let-row>{{ shortId(row) }}</ng-template>
        <ng-template hhDataTableCell="patientName" let-row>{{ row['patientName'] || row['patientId'] }}</ng-template>
        <ng-template hhDataTableCell="orderDate" let-row>{{ row['orderDate'] | date:'mediumDate' }}</ng-template>
        <ng-template hhDataTableCell="priorityName" let-row>
          <span class="priority-badge" [class.priority-high]="priorityOf(row) === 'high'"
                [class.priority-urgent]="priorityOf(row) === 'urgent'"
                [class.priority-routine]="priorityOf(row) === 'routine'">
            {{ row['priorityName'] }}
          </span>
        </ng-template>
        <ng-template hhDataTableCell="testsCount" let-row>{{ testsCountOf(row) }}</ng-template>
        <ng-template hhDataTableCell="statusCode" let-row>
          <hh-status-badge [status]="statusOf(row)" [label]="statusLabel(row)" />
        </ng-template>
        <ng-template hhDataTableCell="actions" let-row>
          <a mat-icon-button [routerLink]="['/lab', idOf(row)]"
             [attr.aria-label]="'common.details' | hhTranslate:'Xem chi tiết'" (click)="$event.stopPropagation()">
            <mat-icon>visibility</mat-icon>
          </a>
        </ng-template>
      </hh-data-table>
    </hh-page-layout>
  `,
    styles: [`
    .hh-status-filter { display: inline-flex; align-items: center; gap: 8px; color: var(--text-secondary); font-size: var(--font-size-caption); }
    .hh-status-filter select { min-height: var(--control-height); min-width: 180px; padding: 0 10px; border: 1px solid var(--border-default); border-radius: var(--radius-input); background: var(--surface-white); color: var(--text-primary); font: inherit; }
    .critical-badge { color: var(--color-primary); border: 1px solid var(--border-default); border-radius: var(--radius-input); padding: 8px 12px; background: var(--surface-white); }
  `],
})
export class LabOrderListComponent implements OnInit, OnDestroy {
  private readonly destroy$ = new Subject<void>();
  private readonly i18n = inject(HisHopeI18nService);

  columns: HisHopeDataTableColumn[] = [
    { key: 'id', label: this.i18n.t('labOrders.column.id', 'Mã phiếu'), sortable: true, width: '120px' },
    { key: 'patientName', label: this.i18n.t('labOrders.column.patient', 'Bệnh nhân') },
    { key: 'orderDate', label: this.i18n.t('labOrders.column.orderDate', 'Ngày chỉ định'), sortable: true },
    { key: 'priorityName', label: this.i18n.t('labOrders.column.priority', 'Mức ưu tiên') },
    { key: 'testsCount', label: this.i18n.t('labOrders.column.testsCount', 'Số xét nghiệm'), align: 'end' as const },
    { key: 'statusCode', label: this.i18n.t('labOrders.column.status', 'Trạng thái'), status: true },
    { key: 'actions', label: this.i18n.t('common.actions', 'Thao tác'), hideable: false, align: 'end' as const },
  ];

  rows: Record<string, unknown>[] = [];
  totalItems = 0;
  loading = false;
  error = '';
  query: HisHopePageQuery = { page: 1, pageSize: 20 };
  statusCode = '';
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
        this.snackBar.open(this.i18n.t('labOrders.criticalAlertToast', 'Có cảnh báo xét nghiệm nghiêm trọng mới'), this.i18n.t('common.close', 'Đóng'), { duration: 3000 });
        this.cdr.markForCheck();
      });

    this.loadData();
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
    void this.criticalAlertStreamService.disconnect();
  }

  loadData(): void {
    this.error = '';
    this.loading = true;
    this.labService.searchLabOrders({
      searchTerm: this.query.search,
      statusCode: this.statusCode || undefined,
      page: this.query.page,
      pageSize: this.query.pageSize,
    }).pipe(takeUntil(this.destroy$)).subscribe({
      next: (result) => {
        this.rows = result.items.map(item => ({ ...item }));
        this.totalItems = result.totalCount;
        this.loading = false;
        this.cdr.markForCheck();
      },
      error: () => {
        this.error = this.i18n.t('labOrders.loadFailed', 'Không thể tải danh sách phiếu xét nghiệm.');
        this.loading = false;
        this.cdr.markForCheck();
      },
    });
  }

  onQueryChange(query: HisHopePageQuery): void {
    this.query = query;
    this.loadData();
  }

  onStatusFilterChange(event: Event): void {
    this.statusCode = (event.target as HTMLSelectElement).value;
    this.query = { ...this.query, page: 1 };
    this.loadData();
    this.cdr.markForCheck();
  }

  onRowClick(row: Record<string, unknown>): void {
    this.viewDetail(this.idOf(row));
  }

  viewDetail(id: string): void {
    this.router.navigate(['/lab', id]);
  }

  shortId(row: Record<string, unknown>): string {
    const id = String(row['id'] ?? '');
    return id.length > 8 ? `${id.slice(0, 8)}...` : id;
  }

  priorityOf(row: Record<string, unknown>): string {
    return String(row['priorityCode'] ?? '');
  }

  testsCountOf(row: Record<string, unknown>): number {
    const tests = row['tests'];
    return Array.isArray(tests) ? tests.length : 0;
  }

  statusOf(row: Record<string, unknown>): string {
    return String(row['statusCode'] ?? '');
  }

  statusLabel(row: Record<string, unknown>): string {
    return String(row['statusName'] ?? row['statusCode'] ?? '');
  }

  idOf(row: Record<string, unknown>): string {
    return String(row['id'] ?? '');
  }
}
