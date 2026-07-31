import { Component, OnInit, OnDestroy, ChangeDetectionStrategy, ChangeDetectorRef, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterModule } from '@angular/router';
import { Subject, takeUntil } from 'rxjs';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { MatTooltipModule } from '@angular/material/tooltip';
import { AppointmentService } from '@core/services/appointment.service';
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
    selector: 'app-appointment-list',
    standalone: true,
    imports: [
        CommonModule, RouterModule,
        MatIconModule, MatButtonModule, MatTooltipModule,
        HisHopeDataTableComponent, HisHopeDataTableCellDirective, HisHopePageHeaderComponent,
        HisHopePageLayoutComponent, HisHopeToolbarComponent, HisHopeStatusBadgeComponent, HisHopeTranslatePipe,
    ],
    changeDetection: ChangeDetectionStrategy.OnPush,
    template: `
    <hh-page-layout>
      <hh-page-header hhPageHeader
                      [title]="'appointments.title' | hhTranslate:'Lịch hẹn'"
                      [subtitle]="'appointments.subtitle' | hhTranslate:'Quản lý lịch hẹn khám bệnh'">
        <button mat-raised-button color="primary" routerLink="/appointments/new">
          <mat-icon>add</mat-icon> {{ 'appointments.new' | hhTranslate:'Tạo lịch hẹn' }}
        </button>
      </hh-page-header>

      <hh-toolbar hhPageToolbar [label]="'appointments.toolbar' | hhTranslate:'Danh sách lịch hẹn'">
        <span hhToolbarTitle>{{ totalItems }} {{ 'appointments.count' | hhTranslate:'cuộc hẹn' }}</span>
      </hh-toolbar>

      <hh-data-table [label]="'appointments.tableLabel' | hhTranslate:'Danh sách lịch hẹn'"
                     [loading]="loading" [error]="error"
                     [empty]="rows.length === 0 && !loading"
                     [emptyMessage]="'appointments.empty' | hhTranslate:'Không tìm thấy lịch hẹn nào.'"
                     [searchPlaceholder]="'appointments.search' | hhTranslate:'Tìm theo mã bệnh nhân...'"
                     [columns]="columns" [rows]="rows"
                     [pageSize]="20" [totalItems]="totalItems"
                     [query]="query" [mode]="'server'" [urlSync]="false"
                     [selection]="true" [rowClickable]="true"
                     (queryChange)="onQueryChange($event)" (rowClick)="onRowClick($event)"
                     (retry)="loadData()">
        <ng-template hhDataTableCell="scheduledDate" let-row>{{ row['scheduledDate'] | date:'mediumDate' }}</ng-template>
        <ng-template hhDataTableCell="startTime" let-row>{{ row['startTime'] }} – {{ row['endTime'] }}</ng-template>
        <ng-template hhDataTableCell="type" let-row>{{ row['typeName'] || row['type'] }}</ng-template>
        <ng-template hhDataTableCell="status" let-row>
          <hh-status-badge [status]="statusOf(row)" [label]="statusLabel(row)" />
        </ng-template>
        <ng-template hhDataTableCell="reason" let-row>{{ row['reason'] || '-' }}</ng-template>
        <ng-template hhDataTableCell="actions" let-row>
          <a mat-icon-button [routerLink]="['/appointments', idOf(row)]" [matTooltip]="'common.details' | hhTranslate:'Xem chi tiết'"
             [attr.aria-label]="'common.details' | hhTranslate:'Xem chi tiết'" (click)="$event.stopPropagation()">
            <mat-icon>visibility</mat-icon>
          </a>
        </ng-template>
      </hh-data-table>
    </hh-page-layout>
  `,
})
export class AppointmentListComponent implements OnInit, OnDestroy {
  private readonly destroy$ = new Subject<void>();
  private readonly i18n = inject(HisHopeI18nService);

  columns: HisHopeDataTableColumn[] = [
    { key: 'scheduledDate', label: this.i18n.t('appointments.column.date', 'Ngày hẹn'), sortable: true },
    { key: 'startTime', label: this.i18n.t('appointments.column.time', 'Giờ') },
    { key: 'type', label: this.i18n.t('appointments.column.type', 'Loại') },
    { key: 'status', label: this.i18n.t('appointments.column.status', 'Trạng thái'), status: true },
    { key: 'reason', label: this.i18n.t('appointments.column.reason', 'Lý do') },
    { key: 'actions', label: this.i18n.t('common.actions', 'Thao tác'), hideable: false, align: 'end' as const },
  ];

  rows: Record<string, unknown>[] = [];
  totalItems = 0;
  loading = false;
  error = '';
  query: HisHopePageQuery = { page: 1, pageSize: 20 };

  constructor(
    private appointmentService: AppointmentService,
    private router: Router,
    private cdr: ChangeDetectorRef,
  ) {}

  ngOnInit(): void {
    this.loadData();
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  loadData(): void {
    this.error = '';
    this.loading = true;
    const { page, pageSize, search } = this.query;
    const request = search?.trim()
      ? this.appointmentService.search(search.trim(), page, pageSize)
      : this.appointmentService.list(page, pageSize);
    request.pipe(takeUntil(this.destroy$)).subscribe({
      next: (result) => {
        this.rows = result.items.map(item => ({ ...item }));
        this.totalItems = result.totalCount;
        this.loading = false;
        this.cdr.markForCheck();
      },
      error: () => {
        this.error = this.i18n.t('appointments.loadFailed', 'Không thể tải danh sách cuộc hẹn.');
        this.loading = false;
        this.cdr.markForCheck();
      },
    });
  }

  onQueryChange(query: HisHopePageQuery): void {
    this.query = query;
    this.loadData();
  }

  onRowClick(row: Record<string, unknown>): void {
    this.viewDetail(this.idOf(row));
  }

  viewDetail(id: string): void {
    this.router.navigate(['/appointments', id]);
  }

  statusOf(row: Record<string, unknown>): string {
    return String(row['status'] ?? '');
  }

  statusLabel(row: Record<string, unknown>): string {
    return String(row['statusName'] || row['status'] || '');
  }

  idOf(row: Record<string, unknown>): string {
    return String(row['id'] ?? '');
  }
}
