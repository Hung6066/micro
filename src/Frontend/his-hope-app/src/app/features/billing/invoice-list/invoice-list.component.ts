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
import { BillingService } from "@core/services/billing.service";
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
  selector: "app-invoice-list",
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
        [title]="'invoices.title' | hhTranslate: 'Hóa đơn thanh toán'"
        [subtitle]="
          'invoices.subtitle' | hhTranslate: 'Quản lý hóa đơn và thanh toán'
        "
      >
        <button
          mat-raised-button
          color="primary"
          routerLink="/billing/new"
          [attr.aria-label]="'invoices.new' | hhTranslate: 'Tạo hóa đơn mới'"
        >
          <mat-icon>add</mat-icon>
          {{ "invoices.new" | hhTranslate: "Tạo hóa đơn" }}
        </button>
      </hh-page-header>

      <hh-toolbar
        hhPageToolbar
        [label]="'invoices.toolbar' | hhTranslate: 'Hóa đơn thanh toán'"
      >
        <span hhToolbarTitle
          >{{ totalItems }}
          {{ "invoices.count" | hhTranslate: "hóa đơn" }}</span
        >
        <label class="hh-status-filter" hh-toolbar-controls>
          <span>{{ "common.status" | hhTranslate: "Trạng thái" }}</span>
          <select
            [value]="statusCode"
            (change)="onStatusFilterChange($event)"
            [attr.aria-label]="
              'invoices.statusFilter' | hhTranslate: 'Lọc theo trạng thái'
            "
          >
            <option value="">{{ "common.all" | hhTranslate: "Tất cả" }}</option>
            <option value="draft">
              {{ "invoices.status.draft" | hhTranslate: "Nháp" }}
            </option>
            <option value="issued">
              {{ "invoices.status.issued" | hhTranslate: "Đã phát hành" }}
            </option>
            <option value="partially_paid">
              {{
                "invoices.status.partial"
                  | hhTranslate: "Đã thanh toán một phần"
              }}
            </option>
            <option value="paid">
              {{ "invoices.status.paid" | hhTranslate: "Đã thanh toán" }}
            </option>
            <option value="overdue">
              {{ "invoices.status.overdue" | hhTranslate: "Quá hạn" }}
            </option>
            <option value="cancelled">
              {{ "invoices.status.cancelled" | hhTranslate: "Đã hủy" }}
            </option>
            <option value="voided">
              {{ "invoices.status.voided" | hhTranslate: "Vô hiệu" }}
            </option>
          </select>
        </label>
      </hh-toolbar>

      <hh-data-table
        [label]="'invoices.tableLabel' | hhTranslate: 'Hóa đơn thanh toán'"
        [loading]="loading"
        [error]="error"
        [empty]="rows.length === 0 && !loading"
        [emptyMessage]="
          'invoices.empty' | hhTranslate: 'Không tìm thấy hóa đơn nào.'
        "
        [searchPlaceholder]="
          'invoices.search' | hhTranslate: 'Số hóa đơn, bệnh nhân...'
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
        <ng-template hhDataTableCell="invoiceNumber" let-row>{{
          row["invoiceNumber"]
        }}</ng-template>
        <ng-template hhDataTableCell="patientName" let-row>{{
          row["patientName"] || row["patientId"]
        }}</ng-template>
        <ng-template hhDataTableCell="invoiceDate" let-row>{{
          row["invoiceDate"] | date: "mediumDate"
        }}</ng-template>
        <ng-template hhDataTableCell="totalAmount" let-row
          >{{ amountOf(row, "totalAmount") | number: "1.0-0" }} đ</ng-template
        >
        <ng-template hhDataTableCell="balanceDue" let-row>
          <span
            [class.text-danger]="amountOf(row, 'balanceDue') > 0"
            [class.text-success]="amountOf(row, 'balanceDue') === 0"
          >
            {{ amountOf(row, "balanceDue") | number: "1.0-0" }} đ
          </span>
        </ng-template>
        <ng-template hhDataTableCell="statusCode" let-row>
          <hh-status-badge
            [status]="statusOf(row)"
            [label]="statusLabel(row)"
          />
        </ng-template>
        <ng-template hhDataTableCell="actions" let-row>
          <a
            mat-icon-button
            [routerLink]="['/billing', idOf(row)]"
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
      .text-danger {
        color: var(--pastel-red-text, #c25450);
        font-weight: 500;
      }
      .text-success {
        color: var(--pastel-green-text, #2f6b4a);
        font-weight: 500;
      }
    `,
  ],
})
export class InvoiceListComponent implements OnInit, OnDestroy {
  private readonly destroy$ = new Subject<void>();
  private readonly i18n = inject(HisHopeI18nService);

  columns: HisHopeDataTableColumn[] = [
    {
      key: "invoiceNumber",
      label: this.i18n.t("invoices.column.number", "Số hóa đơn"),
      sortable: true,
    },
    {
      key: "patientName",
      label: this.i18n.t("invoices.column.patient", "Bệnh nhân"),
    },
    {
      key: "invoiceDate",
      label: this.i18n.t("invoices.column.date", "Ngày"),
      sortable: true,
    },
    {
      key: "totalAmount",
      label: this.i18n.t("invoices.column.total", "Tổng tiền"),
      align: "end" as const,
    },
    {
      key: "balanceDue",
      label: this.i18n.t("invoices.column.balance", "Còn nợ"),
      align: "end" as const,
    },
    {
      key: "statusCode",
      label: this.i18n.t("invoices.column.status", "Trạng thái"),
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
    private billingService: BillingService,
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
    this.billingService
      .searchInvoices({
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
            "invoices.loadFailed",
            "Không thể tải danh sách hóa đơn.",
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
    this.router.navigate(["/billing", id]);
  }

  amountOf(row: Record<string, unknown>, key: string): number {
    return Number(row[key] ?? 0);
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
