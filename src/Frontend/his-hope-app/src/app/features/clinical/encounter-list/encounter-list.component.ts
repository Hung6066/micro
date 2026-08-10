import { Component, OnInit, OnDestroy, ChangeDetectionStrategy, ChangeDetectorRef, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterModule } from '@angular/router';
import { Subject, takeUntil } from 'rxjs';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { MatTooltipModule } from '@angular/material/tooltip';
import { ClinicalService } from '@core/services/clinical.service';
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
    selector: 'app-encounter-list',
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
                      [title]="'encounters.title' | hhTranslate:'Hồ sơ lâm sàng'"
                      [subtitle]="'encounters.subtitle' | hhTranslate:'Quản lý hồ sơ khám bệnh và điều trị'">
        <button mat-raised-button color="primary">
          <mat-icon>add</mat-icon> {{ 'encounters.new' | hhTranslate:'Tạo hồ sơ lâm sàng' }}
        </button>
      </hh-page-header>

      <hh-toolbar hhPageToolbar [label]="'encounters.toolbar' | hhTranslate:'Hồ sơ lâm sàng'">
        <span hhToolbarTitle>{{ totalItems }} {{ 'encounters.count' | hhTranslate:'hồ sơ' }}</span>
      </hh-toolbar>

      <hh-data-table [label]="'encounters.tableLabel' | hhTranslate:'Hồ sơ lâm sàng'"
                     [loading]="loading" [error]="error"
                     [empty]="rows.length === 0 && !loading"
                     [emptyMessage]="'encounters.empty' | hhTranslate:'Không tìm thấy hồ sơ lâm sàng nào.'"
                     [searchPlaceholder]="'encounters.search' | hhTranslate:'Tìm theo mã bệnh nhân...'"
                     [columns]="columns" [rows]="rows"
                     [pageSize]="20" [totalItems]="totalItems"
                     [query]="query" [mode]="'server'" [urlSync]="false"
                     [selection]="true" [rowClickable]="true"
                     (queryChange)="onQueryChange($event)" (rowClick)="onRowClick($event)"
                     (retry)="loadData()">
        <ng-template hhDataTableCell="encounterDate" let-row>{{ row['encounterDate'] | date:'medium' }}</ng-template>
        <ng-template hhDataTableCell="patientId" let-row>{{ row['patientId'] }}</ng-template>
        <ng-template hhDataTableCell="encounterType" let-row>{{ row['encounterTypeName'] || row['encounterType'] }}</ng-template>
        <ng-template hhDataTableCell="status" let-row>
          <hh-status-badge [status]="statusOf(row)" [label]="statusLabel(row)" />
        </ng-template>
        <ng-template hhDataTableCell="chiefComplaint" let-row>{{ row['chiefComplaint'] || '-' }}</ng-template>
        <ng-template hhDataTableCell="actions" let-row>
          <a mat-icon-button [routerLink]="['/clinical', idOf(row)]" [matTooltip]="'common.details' | hhTranslate:'Xem chi tiết'"
             [attr.aria-label]="'common.details' | hhTranslate:'Xem chi tiết'" (click)="$event.stopPropagation()">
            <mat-icon>visibility</mat-icon>
          </a>
        </ng-template>
      </hh-data-table>
    </hh-page-layout>
  `,
})
export class EncounterListComponent implements OnInit, OnDestroy {
  private readonly destroy$ = new Subject<void>();
  private readonly i18n = inject(HisHopeI18nService);

  columns: HisHopeDataTableColumn[] = [
    { key: 'encounterDate', label: this.i18n.t('encounters.column.date', 'Ngày khám'), sortable: true },
    { key: 'patientId', label: this.i18n.t('encounters.column.patient', 'Bệnh nhân') },
    { key: 'encounterType', label: this.i18n.t('encounters.column.type', 'Loại') },
    { key: 'status', label: this.i18n.t('encounters.column.status', 'Trạng thái'), status: true },
    { key: 'chiefComplaint', label: this.i18n.t('encounters.column.chiefComplaint', 'Triệu chứng chính') },
    { key: 'actions', label: this.i18n.t('common.actions', 'Thao tác'), hideable: false, align: 'end' as const },
  ];

  rows: Record<string, unknown>[] = [];
  totalItems = 0;
  loading = false;
  error = '';
  query: HisHopePageQuery = { page: 1, pageSize: 20 };

  constructor(
    private clinicalService: ClinicalService,
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
      ? this.clinicalService.search(search.trim(), page, pageSize)
      : this.clinicalService.list(page, pageSize);
    request.pipe(takeUntil(this.destroy$)).subscribe({
      next: (result) => {
        this.rows = result.items.map(item => ({ ...item }));
        this.totalItems = result.totalCount;
        this.loading = false;
        this.cdr.markForCheck();
      },
      error: () => {
        this.error = this.i18n.t('encounters.loadFailed', 'Không thể tải danh sách hồ sơ lâm sàng.');
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
    this.router.navigate(['/clinical', id]);
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
