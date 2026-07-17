// @ts-nocheck
import { Component, Inject, OnDestroy, ChangeDetectionStrategy, ChangeDetectorRef } from '@angular/core';
import { FormBuilder, FormGroup, Validators, ReactiveFormsModule } from '@angular/forms';
import { MatDialogRef, MAT_DIALOG_DATA, MatDialogModule } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { CommonModule } from '@angular/common';
import { Subject, takeUntil } from 'rxjs';
import { LabService } from '@core/services/lab.service';
import { AuthService } from '@core/services/auth.service';

export interface OrderLabData {
  patientId: string;
  patientName: string;
}

const AVAILABLE_TESTS = [
  { code: 'CBC', name: 'Công thức máu', specimen: 'blood' },
  { code: 'BMP', name: 'Điện giải đồ', specimen: 'blood' },
  { code: 'CRP', name: 'C-Reactive Protein', specimen: 'blood' },
  { code: 'HbA1c', name: 'HbA1c (Đường huyết trung bình)', specimen: 'blood' },
  { code: 'UA', name: 'Tổng phân tích nước tiểu', specimen: 'urine' },
  { code: 'BNP', name: 'BNP (Peptide lợi niệu)', specimen: 'blood' },
  { code: 'LIPID', name: 'Mỡ máu toàn phần', specimen: 'blood' },
  { code: 'Cr', name: 'Creatinin máu', specimen: 'blood' },
  { code: 'IgE', name: 'IgE toàn phần', specimen: 'blood' },
  { code: 'LFT', name: 'Chức năng gan', specimen: 'blood' },
];

@Component({
  selector: 'app-order-lab-dialog',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    MatDialogModule,
    MatButtonModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatIconModule,
    MatProgressSpinnerModule,
    MatSnackBarModule,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h2 mat-dialog-title>Chỉ định xét nghiệm</h2>
    <mat-dialog-content>
      <div class="patient-info" *ngIf="data.patientName">
        <mat-icon>person</mat-icon>
        <span>{{ data.patientName }}</span>
      </div>
      <form [formGroup]="form" class="dialog-form">
        <mat-form-field appearance="outline">
          <mat-label>Loại xét nghiệm</mat-label>
          <mat-select formControlName="testCode" required>
            <mat-option *ngFor="let t of availableTests" [value]="t.code">
              {{ t.name }}
            </mat-option>
          </mat-select>
          <mat-error *ngIf="form.get('testCode')?.hasError('required')">Vui lòng chọn xét nghiệm</mat-error>
        </mat-form-field>

        <mat-form-field appearance="outline">
          <mat-label>Mức độ ưu tiên</mat-label>
          <mat-select formControlName="priority" required>
            <mat-option value="routine">Thường quy</mat-option>
            <mat-option value="urgent">Khẩn</mat-option>
            <mat-option value="stat">Cấp cứu</mat-option>
          </mat-select>
        </mat-form-field>

        <mat-form-field appearance="outline">
          <mat-label>Ghi chú</mat-label>
          <textarea matInput formControlName="notes" rows="3"
                    placeholder="Ghi chú cho kỹ thuật viên..."></textarea>
        </mat-form-field>
      </form>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button mat-dialog-close [disabled]="saving">Hủy</button>
      <button mat-raised-button color="primary" (click)="save()" [disabled]="form.invalid || saving">
        <mat-icon>science</mat-icon>
        <span *ngIf="!saving">Gửi chỉ định</span>
        <mat-spinner *ngIf="saving" diameter="20"></mat-spinner>
      </button>
    </mat-dialog-actions>
  `,
  styles: [`
    .patient-info { display: flex; align-items: center; gap: 8px; margin-bottom: 16px; padding: 8px 12px; background: #f3e5f5; border-radius: 8px; color: #7b1fa2; font-weight: 500; }
    .dialog-form { display: flex; flex-direction: column; gap: 16px; min-width: 380px; }
  `],
})
export class OrderLabDialogComponent implements OnDestroy {
  private destroy$ = new Subject<void>();

  form: FormGroup;
  saving = false;
  availableTests = AVAILABLE_TESTS;

  constructor(
    private fb: FormBuilder,
    private dialogRef: MatDialogRef<OrderLabDialogComponent>,
    private labService: LabService,
    private authService: AuthService,
    private snackBar: MatSnackBar,
    private cdr: ChangeDetectorRef,
    @Inject(MAT_DIALOG_DATA) public data: OrderLabData,
  ) {
    this.form = this.fb.group({
      testCode: ['', Validators.required],
      priority: ['routine', Validators.required],
      notes: [''],
    });
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  save(): void {
    if (this.form.invalid || this.saving) return;
    this.saving = true;
    this.cdr.markForCheck();

    this.authService.currentUser$
      .pipe(takeUntil(this.destroy$))
      .subscribe((user) => {
        const providerId = user?.id ?? 'usr-002';
        const selected = AVAILABLE_TESTS.find(t => t.code === this.form.value.testCode);

        this.labService.createLabOrder({
          patientId: this.data.patientId,
          providerId,
          priorityCode: this.form.value.priority,
          notes: this.form.value.notes,
          tests: [{
            testCode: selected!.code,
            testName: selected!.name,
            specimenType: selected!.specimen,
          }],
        }).pipe(takeUntil(this.destroy$))
          .subscribe({
            next: () => {
              this.snackBar.open('Đã gửi chỉ định xét nghiệm', 'Đóng', { duration: 3000 });
              this.dialogRef.close(true);
            },
            error: () => {
              this.saving = false;
              this.snackBar.open('Không thể gửi chỉ định', 'Đóng', { duration: 5000 });
              this.cdr.markForCheck();
            },
          });
      });
  }
}
