import { Component, OnInit, OnDestroy, ChangeDetectionStrategy, ChangeDetectorRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { MatCardModule } from '@angular/material/card';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { Subject, takeUntil } from 'rxjs';
import { PharmacyService } from '@core/services/pharmacy.service';
import { Medication } from '@core/models/medication.model';

@Component({
    selector: 'app-medication-detail',
    standalone: true,
    imports: [
        CommonModule, RouterModule,
        MatCardModule, MatIconModule, MatButtonModule, MatProgressSpinnerModule,
        MatSnackBarModule,
    ],
    changeDetection: ChangeDetectionStrategy.OnPush,
    template: `
    @if (medication) {
    <div class="medication-detail">
      <div class="header">
        <div>
          <h1>{{ medication.name }}</h1>
          <p class="subtitle">
            Mã thuốc: {{ medication.id | slice:0:8 }}... |
            {{ medication.genericName }} |
            <span class="status-badge" [class.status-active]="medication.isActive" [class.status-inactive]="!medication.isActive">
              {{ medication.isActive ? 'Đang hoạt động' : 'Ngừng sử dụng' }}
            </span>
          </p>
        </div>
        <div class="header-actions">
          @if (medication.isActive) {
          <button mat-raised-button color="accent" [routerLink]="['/pharmacy/medications', medication.id, 'edit']"
                  [attr.aria-label]="'Chỉnh sửa thuốc ' + medication.name">
            <mat-icon>edit</mat-icon> Chỉnh sửa
          </button>
          }
          <button mat-stroked-button [color]="medication.isActive ? 'warn' : 'primary'"
                  (click)="toggleActive()" [attr.aria-label]="(medication.isActive ? 'Ngừng' : 'Kích hoạt') + ' thuốc'">
            <mat-icon>{{ medication.isActive ? 'block' : 'check_circle' }}</mat-icon>
            {{ medication.isActive ? 'Ngừng sử dụng' : 'Kích hoạt' }}
          </button>
        </div>
      </div>

      <div class="detail-grid">
        <mat-card>
          <mat-card-header><mat-card-title>Thông tin thuốc</mat-card-title></mat-card-header>
          <mat-card-content>
            <p><strong>Tên thuốc:</strong> {{ medication.name }}</p>
            <p><strong>Hoạt chất:</strong> {{ medication.genericName }}</p>
            <p><strong>Tên thương mại:</strong> {{ medication.brandName || '-' }}</p>
            <p><strong>Hàm lượng:</strong> {{ medication.strength }}</p>
            <p><strong>Dạng bào chế:</strong> {{ medication.dosageForm }}</p>
            <p><strong>Đường dùng:</strong> {{ medication.route }}</p>
            <p><strong>Kê đơn:</strong> {{ medication.requiresPrescription ? 'Cần kê đơn' : 'Không cần kê đơn' }}</p>
          </mat-card-content>
        </mat-card>

        <mat-card>
          <mat-card-header><mat-card-title>Thời gian</mat-card-title></mat-card-header>
          <mat-card-content>
            <p><strong>Ngày tạo:</strong> {{ medication.createdAt | date:'medium' }}</p>
            @if (medication.updatedAt) {
            <p><strong>Cập nhật lần cuối:</strong> {{ medication.updatedAt | date:'medium' }}</p>
            }
          </mat-card-content>
        </mat-card>
      </div>
    </div>
    }

    @if (!medication && !loadError) {
    <div class="loading-container">
      <mat-spinner diameter="40" aria-label="Đang tải"></mat-spinner>
      <p>Đang tải thông tin thuốc...</p>
    </div>
    }

    @if (loadError) {
    <div class="error-container">
      <mat-icon color="warn">error_outline</mat-icon>
      <p>Không thể tải thông tin thuốc. Vui lòng thử lại sau.</p>
      <button mat-stroked-button color="primary" (click)="loadMedication()">Thử lại</button>
    </div>
    }
  `,
    styles: [`
    .medication-detail { padding: 24px; }
    .header { display: flex; justify-content: space-between; align-items: flex-start; margin-bottom: 24px; }
    .header-actions { display: flex; gap: 12px; }
    .subtitle { color: #666; font-size: 14px; display: flex; align-items: center; gap: 8px; flex-wrap: wrap; }
    .detail-grid { display: grid; grid-template-columns: 1fr 1fr; gap: 20px; margin-bottom: 20px; }
    mat-card-content p { margin: 8px 0; }
    .loading-container, .error-container { display: flex; flex-direction: column; align-items: center; justify-content: center; padding: 64px 24px; gap: 16px; color: var(--text-secondary, #787774); }
  `],
})
export class MedicationDetailComponent implements OnInit, OnDestroy {
  private destroy$ = new Subject<void>();

  medication?: Medication;
  loadError = false;
  private medicationId = '';

  constructor(
    private route: ActivatedRoute,
    private pharmacyService: PharmacyService,
    private snackBar: MatSnackBar,
    private router: Router,
    private cdr: ChangeDetectorRef,
  ) {}

  ngOnInit(): void {
    this.medicationId = this.route.snapshot.params['id'];
    this.loadMedication();
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  loadMedication(): void {
    this.loadError = false;
    this.pharmacyService.getMedication(this.medicationId)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (m) => {
          this.medication = m;
          this.cdr.markForCheck();
        },
        error: () => {
          this.loadError = true;
          this.cdr.markForCheck();
        },
      });
  }

  toggleActive(): void {
    if (!this.medication) return;
    const action = this.medication.isActive ? 'ngừng' : 'kích hoạt';

    this.pharmacyService.deactivateMedication(this.medication.id)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: () => {
          this.medication!.isActive = !this.medication!.isActive;
          this.snackBar.open(`Đã ${action} thuốc thành công`, 'Đóng', { duration: 3000 });
          this.cdr.markForCheck();
        },
        error: () => {
          this.snackBar.open(`Không thể ${action} thuốc`, 'Đóng', { duration: 5000 });
          this.cdr.markForCheck();
        },
      });
  }
}
