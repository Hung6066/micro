import { Component, OnInit, OnDestroy, ChangeDetectionStrategy, ChangeDetectorRef, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterModule } from '@angular/router';
import { Subject, takeUntil } from 'rxjs';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { PharmacyService } from '@core/services/pharmacy.service';
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
    selector: 'app-medication-list',
    standalone: true,
    imports: [
        CommonModule, RouterModule,
        MatIconModule, MatButtonModule,
        HisHopeDataTableComponent, HisHopeDataTableCellDirective, HisHopePageHeaderComponent,
        HisHopePageLayoutComponent, HisHopeToolbarComponent, HisHopeStatusBadgeComponent, HisHopeTranslatePipe,
    ],
    changeDetection: ChangeDetectionStrategy.OnPush,
    template: `
    <hh-page-layout>
      <hh-page-header hhPageHeader
                      [title]="'medications.title' | hhTranslate:'Danh mục thuốc'"
                      [subtitle]="'medications.subtitle' | hhTranslate:'Quản lý danh mục thuốc của bệnh viện'">
        <button mat-raised-button color="primary" routerLink="/pharmacy/medications/new"
                [attr.aria-label]="'medications.new' | hhTranslate:'Thêm thuốc mới'">
          <mat-icon>add</mat-icon> {{ 'medications.new' | hhTranslate:'Thêm thuốc' }}
        </button>
      </hh-page-header>

      <hh-toolbar hhPageToolbar [label]="'medications.toolbar' | hhTranslate:'Danh mục thuốc'">
        <span hhToolbarTitle>{{ totalItems }} {{ 'medications.count' | hhTranslate:'loại thuốc' }}</span>
      </hh-toolbar>

      <hh-data-table [label]="'medications.tableLabel' | hhTranslate:'Danh mục thuốc'"
                     [loading]="loading" [error]="error"
                     [empty]="rows.length === 0 && !loading"
                     [emptyMessage]="'medications.empty' | hhTranslate:'Không tìm thấy thuốc nào.'"
                     [searchPlaceholder]="'medications.search' | hhTranslate:'Tên thuốc, hoạt chất, hoặc mã...'"
                     [columns]="columns" [rows]="rows"
                     [pageSize]="20" [totalItems]="totalItems"
                     [query]="query" [mode]="'server'" [urlSync]="false"
                     [selection]="true" [rowClickable]="true"
                     (queryChange)="onQueryChange($event)" (rowClick)="onRowClick($event)"
                     (retry)="loadData()">
        <ng-template hhDataTableCell="name" let-row>{{ row['name'] }}</ng-template>
        <ng-template hhDataTableCell="genericName" let-row>{{ row['genericName'] }}</ng-template>
        <ng-template hhDataTableCell="strength" let-row>{{ row['strength'] }}</ng-template>
        <ng-template hhDataTableCell="dosageForm" let-row>{{ row['dosageForm'] }}</ng-template>
        <ng-template hhDataTableCell="isActive" let-row>
          <hh-status-badge [status]="isActiveOf(row) ? 'active' : 'inactive'" [label]="isActiveLabel(row)" />
        </ng-template>
        <ng-template hhDataTableCell="actions" let-row>
          <a mat-icon-button [routerLink]="['/pharmacy/medications', idOf(row)]"
             [attr.aria-label]="'common.details' | hhTranslate:'Xem chi tiết'" (click)="$event.stopPropagation()">
            <mat-icon>visibility</mat-icon>
          </a>
          <a mat-icon-button color="accent" [routerLink]="['/pharmacy/medications', idOf(row), 'edit']"
             [attr.aria-label]="'common.edit' | hhTranslate:'Chỉnh sửa'" (click)="$event.stopPropagation()">
            <mat-icon>edit</mat-icon>
          </a>
        </ng-template>
      </hh-data-table>
    </hh-page-layout>
  `,
    styles: [``],
})
export class MedicationListComponent implements OnInit, OnDestroy {
  private readonly destroy$ = new Subject<void>();
  private readonly i18n = inject(HisHopeI18nService);

  columns: HisHopeDataTableColumn[] = [
    { key: 'name', label: this.i18n.t('medications.column.name', 'Tên thuốc'), sortable: true },
    { key: 'genericName', label: this.i18n.t('medications.column.genericName', 'Hoạt chất') },
    { key: 'strength', label: this.i18n.t('medications.column.strength', 'Hàm lượng') },
    { key: 'dosageForm', label: this.i18n.t('medications.column.dosageForm', 'Dạng bào chế') },
    { key: 'isActive', label: this.i18n.t('medications.column.status', 'Trạng thái'), status: true },
    { key: 'actions', label: this.i18n.t('common.actions', 'Thao tác'), hideable: false, align: 'end' as const },
  ];

  rows: Record<string, unknown>[] = [];
  totalItems = 0;
  loading = false;
  error = '';
  query: HisHopePageQuery = { page: 1, pageSize: 20 };

  constructor(
    private pharmacyService: PharmacyService,
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
    this.pharmacyService.searchMedications({
      searchTerm: this.query.search,
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
        this.error = this.i18n.t('medications.loadFailed', 'Không thể tải danh mục thuốc.');
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
    this.router.navigate(['/pharmacy/medications', id]);
  }

  isActiveOf(row: Record<string, unknown>): boolean {
    return row['isActive'] === true;
  }

  isActiveLabel(row: Record<string, unknown>): string {
    return this.isActiveOf(row)
      ? this.i18n.t('medications.active', 'Hoạt động')
      : this.i18n.t('medications.inactive', 'Ngừng');
  }

  idOf(row: Record<string, unknown>): string {
    return String(row['id'] ?? '');
  }
}
