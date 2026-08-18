import {
  Component,
  OnInit,
  OnDestroy,
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  inject,
} from "@angular/core";
import { CommonModule } from "@angular/common";
import { Router, RouterModule } from "@angular/router";
import { Subject, takeUntil } from "rxjs";
import { MatIconModule } from "@angular/material/icon";
import { MatButtonModule } from "@angular/material/button";
import { PharmacyService } from "@core/services/pharmacy.service";
import { HisHopePageQuery } from "@his-hope/frontend-foundation";
import {
  HisHopeI18nService,
  HisHopeTranslatePipe,
} from "@his-hope/frontend-foundation/i18n";
import {
  HisHopeDataTableCellDirective,
  HisHopeDataTableColumn,
  HisHopeDataTableComponent,
  HisHopePageHeaderComponent,
  HisHopePageLayoutComponent,
  HisHopeStatusBadgeComponent,
  HisHopeToolbarComponent,
} from "@his-hope/frontend-foundation/ui";

@Component({
  selector: "app-prescription-list",
  standalone: true,
  imports: [
    CommonModule,
    RouterModule,
    MatIconModule,
    MatButtonModule,
    HisHopeDataTableComponent,
    HisHopeDataTableCellDirective,
    HisHopePageHeaderComponent,
    HisHopePageLayoutComponent,
    HisHopeToolbarComponent,
    HisHopeStatusBadgeComponent,
    HisHopeTranslatePipe,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <hh-page-layout>
      <hh-page-header
        hhPageHeader
        [title]="'prescriptions.title' | hhTranslate: 'Danh sách đơn thuốc'"
        [subtitle]="
          'prescriptions.subtitle'
            | hhTranslate: 'Quản lý đơn thuốc theo yêu cầu'
        "
      >
        <button
          mat-raised-button
          color="primary"
          routerLink="/pharmacy/prescriptions/new"
          [attr.aria-label]="
            'prescriptions.new' | hhTranslate: 'Tạo đơn thuốc mới'
          "
        >
          <mat-icon>add</mat-icon>
          {{ "prescriptions.new" | hhTranslate: "Tạo đơn thuốc" }}
        </button>
      </hh-page-header>

      <hh-toolbar
        hhPageToolbar
        [label]="'prescriptions.toolbar' | hhTranslate: 'Danh sách đơn thuốc'"
      >
        <span hhToolbarTitle
          >{{ totalItems }}
          {{ "prescriptions.count" | hhTranslate: "đơn thuốc" }}</span
        >
        <label class="hh-status-filter" hh-toolbar-controls>
          <span>{{ "common.status" | hhTranslate: "Trạng thái" }}</span>
          <select
            [value]="statusCode"
            (change)="onStatusFilterChange($event)"
            [attr.aria-label]="
              'prescriptions.statusFilter' | hhTranslate: 'Lọc theo trạng thái'
            "
          >
            <option value="">{{ "common.all" | hhTranslate: "Tất cả" }}</option>
            <option value="active">
              {{
                "prescriptions.status.active" | hhTranslate: "Đang hoạt động"
              }}
            </option>
            <option value="filled">
              {{ "prescriptions.status.filled" | hhTranslate: "Đã cấp phát" }}
            </option>
            <option value="partially_filled">
              {{
                "prescriptions.status.partial"
                  | hhTranslate: "Cấp phát một phần"
              }}
            </option>
            <option value="cancelled">
              {{ "prescriptions.status.cancelled" | hhTranslate: "Đã hủy" }}
            </option>
            <option value="expired">
              {{ "prescriptions.status.expired" | hhTranslate: "Hết hạn" }}
            </option>
          </select>
        </label>
      </hh-toolbar>

      <hh-data-table
        [label]="
          'prescriptions.tableLabel' | hhTranslate: 'Danh sách đơn thuốc'
        "
        [loading]="loading"
        [error]="error"
        [empty]="rows.length === 0 && !loading"
        [emptyMessage]="
          'prescriptions.empty' | hhTranslate: 'Không tìm thấy đơn thuốc nào.'
        "
        [searchPlaceholder]="
          'prescriptions.search'
            | hhTranslate: 'Mã đơn, tên thuốc, bệnh nhân...'
        "
        [columns]="columns"
        [rows]="rows"
        [pageSize]="20"
        [totalItems]="totalItems"
        [query]="query"
        [mode]="'server'"
        [urlSync]="false"
        [selection]="true"
        [rowClickable]="true"
        (queryChange)="onQueryChange($event)"
        (rowClick)="onRowClick($event)"
        (retry)="loadData()"
      >
        <ng-template hhDataTableCell="id" let-row>{{
          shortId(row)
        }}</ng-template>
        <ng-template hhDataTableCell="medicationName" let-row>{{
          row["medicationName"]
        }}</ng-template>
        <ng-template hhDataTableCell="strength" let-row>{{
          row["strength"]
        }}</ng-template>
        <ng-template hhDataTableCell="quantity" let-row>{{
          row["quantity"]
        }}</ng-template>
        <ng-template hhDataTableCell="prescribedAt" let-row>{{
          row["prescribedAt"] | date: "mediumDate"
        }}</ng-template>
        <ng-template hhDataTableCell="statusCode" let-row>
          <hh-status-badge
            [status]="statusOf(row)"
            [label]="statusLabel(row)"
          />
        </ng-template>
        <ng-template hhDataTableCell="actions" let-row>
          <a
            mat-icon-button
            [routerLink]="['/pharmacy/prescriptions', idOf(row)]"
            [attr.aria-label]="'common.details' | hhTranslate: 'Xem chi tiết'"
            (click)="$event.stopPropagation()"
          >
            <mat-icon>visibility</mat-icon>
          </a>
        </ng-template>
      </hh-data-table>
    </hh-page-layout>
  `,
  styles: [
    `
      .hh-status-filter {
        display: inline-flex;
        align-items: center;
        gap: 8px;
        color: var(--text-secondary);
        font-size: var(--font-size-caption);
      }
      .hh-status-filter select {
        min-height: var(--control-height);
        min-width: 180px;
        padding: 0 10px;
        border: 1px solid var(--border-default);
        border-radius: var(--radius-input);
        background: var(--surface-white);
        color: var(--text-primary);
        font: inherit;
      }
    `,
  ],
})
export class PrescriptionListComponent implements OnInit, OnDestroy {
  private readonly destroy$ = new Subject<void>();
  private readonly i18n = inject(HisHopeI18nService);

  columns: HisHopeDataTableColumn[] = [
    {
      key: "id",
      label: this.i18n.t("prescriptions.column.id", "Mã đơn"),
      sortable: true,
      width: "120px",
    },
    {
      key: "medicationName",
      label: this.i18n.t("prescriptions.column.medicationName", "Thuốc"),
    },
    {
      key: "strength",
      label: this.i18n.t("prescriptions.column.strength", "Hàm lượng"),
    },
    {
      key: "quantity",
      label: this.i18n.t("prescriptions.column.quantity", "Số lượng"),
      align: "end" as const,
    },
    {
      key: "prescribedAt",
      label: this.i18n.t("prescriptions.column.prescribedAt", "Ngày kê"),
      sortable: true,
    },
    {
      key: "statusCode",
      label: this.i18n.t("prescriptions.column.status", "Trạng thái"),
      status: true,
    },
    {
      key: "actions",
      label: this.i18n.t("common.actions", "Thao tác"),
      hideable: false,
      align: "end" as const,
    },
  ];

  rows: Record<string, unknown>[] = [];
  totalItems = 0;
  loading = false;
  error = "";
  query: HisHopePageQuery = { page: 1, pageSize: 20 };
  statusCode = "";

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
    this.error = "";
    this.loading = true;
    this.pharmacyService
      .searchPrescriptions({
        searchTerm: this.query.search,
        statusCode: this.statusCode || undefined,
        page: this.query.page,
        pageSize: this.query.pageSize,
      })
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (result) => {
          this.rows = result.items.map((item) => ({ ...item }));
          this.totalItems = result.totalCount;
          this.loading = false;
          this.cdr.markForCheck();
        },
        error: () => {
          this.error = this.i18n.t(
            "prescriptions.loadFailed",
            "Không thể tải danh sách đơn thuốc.",
          );
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
    this.router.navigate(["/pharmacy/prescriptions", id]);
  }

  shortId(row: Record<string, unknown>): string {
    const id = String(row["id"] ?? "");
    return id.length > 8 ? `${id.slice(0, 8)}...` : id;
  }

  statusOf(row: Record<string, unknown>): string {
    return String(row["statusCode"] ?? "");
  }

  statusLabel(row: Record<string, unknown>): string {
    return String(row["statusName"] ?? row["statusCode"] ?? "");
  }

  idOf(row: Record<string, unknown>): string {
    return String(row["id"] ?? "");
  }
}
