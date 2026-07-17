// @ts-nocheck
import { Component, OnInit, OnDestroy, ChangeDetectionStrategy, ChangeDetectorRef } from '@angular/core';
import { Router } from '@angular/router';
import { FormControl } from '@angular/forms';
import { Subject, debounceTime, distinctUntilChanged, takeUntil } from 'rxjs';
import { PharmacyService } from '@core/services/pharmacy.service';
import { Medication } from '@core/models/medication.model';

@Component({
  selector: 'app-medication-list',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="medication-list">
      <div class="header">
        <h1>Danh mục thuốc</h1>
        <button mat-raised-button color="primary" routerLink="/pharmacy/medications/new"
                attr.aria-label="Thêm thuốc mới">
          <mat-icon>add</mat-icon> Thêm thuốc
        </button>
      </div>

      <mat-form-field appearance="outline" class="search-field">
        <mat-label>Tìm kiếm thuốc</mat-label>
        <input matInput [formControl]="searchControl" placeholder="Tên thuốc, hoạt chất, hoặc mã..."
               aria-label="Tìm kiếm thuốc">
        <mat-icon matPrefix>search</mat-icon>
      </mat-form-field>

      <div class="loading-shimmer" *ngIf="loading">
        <mat-progress-bar mode="indeterminate" aria-label="Đang tải"></mat-progress-bar>
      </div>

      <div *ngIf="!loading && medications.length === 0" class="empty-state">
        <mat-icon class="empty-icon">medication</mat-icon>
        <p>Không tìm thấy thuốc nào.</p>
      </div>

      <mat-table [dataSource]="medications" class="mat-elevation-z2" *ngIf="!loading && medications.length > 0">
        <ng-container matColumnDef="name">
          <mat-header-cell *matHeaderCellDef>Tên thuốc</mat-header-cell>
          <mat-cell *matCellDef="let m">{{ m.name }}</mat-cell>
        </ng-container>

        <ng-container matColumnDef="genericName">
          <mat-header-cell *matHeaderCellDef>Hoạt chất</mat-header-cell>
          <mat-cell *matCellDef="let m">{{ m.genericName }}</mat-cell>
        </ng-container>

        <ng-container matColumnDef="strength">
          <mat-header-cell *matHeaderCellDef>Hàm lượng</mat-header-cell>
          <mat-cell *matCellDef="let m">{{ m.strength }}</mat-cell>
        </ng-container>

        <ng-container matColumnDef="dosageForm">
          <mat-header-cell *matHeaderCellDef>Dạng bào chế</mat-header-cell>
          <mat-cell *matCellDef="let m">{{ m.dosageForm }}</mat-cell>
        </ng-container>

        <ng-container matColumnDef="isActive">
          <mat-header-cell *matHeaderCellDef>Trạng thái</mat-header-cell>
          <mat-cell *matCellDef="let m">
            <span class="status-badge" [class.status-active]="m.isActive" [class.status-inactive]="!m.isActive">
              {{ m.isActive ? 'Hoạt động' : 'Ngừng' }}
            </span>
          </mat-cell>
        </ng-container>

        <ng-container matColumnDef="actions">
          <mat-header-cell *matHeaderCellDef>Thao tác</mat-header-cell>
          <mat-cell *matCellDef="let m">
            <button mat-icon-button color="primary" (click)="viewDetail(m.id)"
                    attr.aria-label="Xem chi tiết thuốc {{ m.name }}">
              <mat-icon>visibility</mat-icon>
            </button>
            <button mat-icon-button color="accent" [routerLink]="['/pharmacy/medications', m.id, 'edit']"
                    attr.aria-label="Chỉnh sửa thuốc {{ m.name }}">
              <mat-icon>edit</mat-icon>
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
    .medication-list { padding: 24px; }
    .header { display: flex; justify-content: space-between; align-items: center; margin-bottom: 24px; }
    .search-field { width: 100%; max-width: 500px; margin-bottom: 20px; }
    mat-table { width: 100%; cursor: pointer; }
    mat-row:hover { background: #f5f5f5; }
    .clickable-row { cursor: pointer; }

    .loading-shimmer { margin-bottom: 16px; }
    .empty-state { text-align: center; padding: 48px; color: #999; }
    .empty-icon { font-size: 48px; width: 48px; height: 48px; margin-bottom: 16px; }
  `],
})
export class MedicationListComponent implements OnInit, OnDestroy {
  private destroy$ = new Subject<void>();

  displayedColumns = ['name', 'genericName', 'strength', 'dosageForm', 'isActive', 'actions'];
  medications: Medication[] = [];
  totalCount = 0;
  page = 1;
  pageSize = 20;
  loading = false;
  searchControl = new FormControl('');
  private searchTerm = '';

  constructor(
    private pharmacyService: PharmacyService,
    private router: Router,
    private cdr: ChangeDetectorRef,
  ) {}

  ngOnInit(): void {
    this.searchControl.valueChanges
      .pipe(
        debounceTime(300),
        distinctUntilChanged(),
        takeUntil(this.destroy$),
      )
      .subscribe((term) => {
        this.searchTerm = term ?? '';
        this.page = 1;
        this.loadMedications();
        this.cdr.markForCheck();
      });
    this.loadMedications();
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  loadMedications(): void {
    this.loading = true;
    this.pharmacyService.searchMedications({ searchTerm: this.searchTerm, page: this.page, pageSize: this.pageSize })
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (result) => {
          this.medications = result.items;
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
    this.router.navigate(['/pharmacy/medications', id]);
  }

  onPageChange(event: any): void {
    this.page = event.pageIndex + 1;
    this.pageSize = event.pageSize;
    this.loadMedications();
  }
}
