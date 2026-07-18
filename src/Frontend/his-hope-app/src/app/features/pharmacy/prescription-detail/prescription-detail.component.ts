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
import { Prescription } from '@core/models/prescription.model';

@Component({
    selector: 'app-prescription-detail',
    standalone: true,
    imports: [
        CommonModule, RouterModule,
        MatCardModule, MatIconModule, MatButtonModule, MatProgressSpinnerModule,
        MatSnackBarModule,
    ],
    changeDetection: ChangeDetectionStrategy.OnPush,
    template: `
    <div class="prescription-detail" *ngIf="prescription">
      <div class="header">
        <div>
          <h1>Đơn thuốc #{{ prescription.id | slice:0:8 }}...</h1>
          <p class="subtitle">
            <span class="status-badge" [class.status-active]="prescription.statusCode === 'active'"
                  [class.status-filled]="prescription.statusCode === 'filled'"
                  [class.status-partial]="prescription.statusCode === 'partially_filled'"
                  [class.status-cancelled]="prescription.statusCode === 'cancelled'"
                  [class.status-expired]="prescription.statusCode === 'expired'">
              {{ prescription.statusName }}
            </span>
          </p>
        </div>
        <div class="header-actions">
          <button mat-raised-button color="primary"
                  *ngIf="prescription.statusCode === 'active'"
                  (click)="fillPrescription()"
                  attr.aria-label="Cấp phát thuốc">
            <mat-icon>medication</mat-icon> Cấp phát
          </button>
          <button mat-stroked-button color="warn"
                  *ngIf="prescription.statusCode === 'active' || prescription.statusCode === 'partially_filled'"
                  (click)="cancelPrescription()"
                  attr.aria-label="Hủy đơn thuốc">
            <mat-icon>cancel</mat-icon> Hủy đơn
          </button>
        </div>
      </div>

      <div class="detail-grid">
        <mat-card>
          <mat-card-header><mat-card-title>Thông tin đơn thuốc</mat-card-title></mat-card-header>
          <mat-card-content>
            <p><strong>Bệnh nhân:</strong> {{ prescription.patientName || prescription.patientId }}</p>
            <p><strong>Bác sĩ:</strong> {{ prescription.providerName || prescription.providerId }}</p>
            <p><strong>Thuốc:</strong> {{ prescription.medicationName }}</p>
            <p><strong>Hàm lượng:</strong> {{ prescription.strength }}</p>
            <p><strong>Dạng bào chế:</strong> {{ prescription.dosageForm }}</p>
            <p><strong>Hướng dẫn sử dụng:</strong> {{ prescription.dosageInstructions }}</p>
            <p><strong>Đường dùng:</strong> {{ prescription.route }}</p>
            <p><strong>Số lượng:</strong> {{ prescription.quantity }}</p>
            <p><strong>Số lần tái kê:</strong> {{ prescription.refills }}</p>
          </mat-card-content>
        </mat-card>

        <mat-card>
          <mat-card-header><mat-card-title>Thời gian</mat-card-title></mat-card-header>
          <mat-card-content>
            <p><strong>Ngày kê đơn:</strong> {{ prescription.prescribedAt | date:'medium' }}</p>
            <p *ngIf="prescription.filledAt"><strong>Ngày cấp phát:</strong> {{ prescription.filledAt | date:'medium' }}</p>
            <p><strong>Ngày tạo:</strong> {{ prescription.createdAt | date:'medium' }}</p>
            <p *ngIf="prescription.updatedAt"><strong>Cập nhật:</strong> {{ prescription.updatedAt | date:'medium' }}</p>
          </mat-card-content>
        </mat-card>
      </div>
    </div>

    <div class="loading-container" *ngIf="!prescription && !loadError">
      <mat-spinner diameter="40" aria-label="Đang tải"></mat-spinner>
      <p>Đang tải thông tin đơn thuốc...</p>
    </div>

    <div class="error-container" *ngIf="loadError">
      <mat-icon color="warn">error_outline</mat-icon>
      <p>Không thể tải thông tin đơn thuốc. Vui lòng thử lại sau.</p>
      <button mat-stroked-button color="primary" (click)="loadPrescription()">Thử lại</button>
    </div>
  `,
    styles: [`
    .prescription-detail { padding: 24px; }
    .header { display: flex; justify-content: space-between; align-items: flex-start; margin-bottom: 24px; }
    .header-actions { display: flex; gap: 12px; }
    .subtitle { color: #666; font-size: 14px; }
    .detail-grid { display: grid; grid-template-columns: 1fr 1fr; gap: 20px; margin-bottom: 20px; }
    mat-card-content p { margin: 8px 0; }
    .status-badge { padding: 4px 16px; border-radius: 16px; font-weight: 500; font-size: 14px; display: inline-block; }
    .status-active { background: #e3f2fd; color: #1565c0; }
    .status-filled { background: #e8f5e9; color: #2e7d32; }
    .status-partial { background: #fff3e0; color: #e65100; }
    .status-cancelled { background: #fbe9e7; color: #c62828; }
    .status-expired { background: #f3e5f5; color: #6a1b9a; }
    .loading-container, .error-container { display: flex; flex-direction: column; align-items: center; justify-content: center; padding: 64px 24px; gap: 16px; color: #666; }
  `],
})
export class PrescriptionDetailComponent implements OnInit, OnDestroy {
  private destroy$ = new Subject<void>();

  prescription?: Prescription;
  loadError = false;
  private prescriptionId = '';

  constructor(
    private route: ActivatedRoute,
    private pharmacyService: PharmacyService,
    private snackBar: MatSnackBar,
    private router: Router,
    private cdr: ChangeDetectorRef,
  ) {}

  ngOnInit(): void {
    this.prescriptionId = this.route.snapshot.params['id'];
    this.loadPrescription();
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  loadPrescription(): void {
    this.loadError = false;
    this.pharmacyService.getPrescription(this.prescriptionId)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (p) => {
          this.prescription = p;
          this.cdr.markForCheck();
        },
        error: () => {
          this.loadError = true;
          this.cdr.markForCheck();
        },
      });
  }

  fillPrescription(): void {
    if (!this.prescription) return;
    this.pharmacyService.fillPrescription(this.prescription.id)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: () => {
          this.snackBar.open('Đã cấp phát thuốc thành công', 'Đóng', { duration: 3000 });
          this.loadPrescription();
        },
        error: () => {
          this.snackBar.open('Không thể cấp phát thuốc', 'Đóng', { duration: 5000 });
          this.cdr.markForCheck();
        },
      });
  }

  cancelPrescription(): void {
    if (!this.prescription) return;
    this.pharmacyService.cancelPrescription(this.prescription.id)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: () => {
          this.snackBar.open('Đã hủy đơn thuốc', 'Đóng', { duration: 3000 });
          this.loadPrescription();
        },
        error: () => {
          this.snackBar.open('Không thể hủy đơn thuốc', 'Đóng', { duration: 5000 });
          this.cdr.markForCheck();
        },
      });
  }
}
