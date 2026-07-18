import { Component, OnInit, OnDestroy, ChangeDetectionStrategy, ChangeDetectorRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, Validators, FormArray, FormControl, ReactiveFormsModule } from '@angular/forms';
import { Router, RouterModule } from '@angular/router';
import { Subject, debounceTime, distinctUntilChanged, takeUntil } from 'rxjs';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatIconModule } from '@angular/material/icon';
import { MatSelectModule } from '@angular/material/select';
import { MatButtonModule } from '@angular/material/button';
import { MatAutocompleteModule } from '@angular/material/autocomplete';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { LabService } from '@core/services/lab.service';
import { PatientService } from '@core/services/patient.service';
import { Patient } from '@core/models/patient.model';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';

@Component({
    selector: 'app-lab-order-form',
    standalone: true,
    imports: [
        CommonModule, ReactiveFormsModule, RouterModule,
        MatCardModule, MatFormFieldModule, MatInputModule, MatIconModule,
        MatSelectModule, MatButtonModule, MatAutocompleteModule, MatProgressSpinnerModule,
        MatSnackBarModule,
    ],
    changeDetection: ChangeDetectionStrategy.OnPush,
    template: `
    <div class="lab-order-form">
      <h1>Tạo phiếu xét nghiệm mới</h1>

      <form [formGroup]="labOrderForm" (ngSubmit)="onSubmit()">
        <div class="form-grid">
          <mat-form-field appearance="outline">
            <mat-label>Bệnh nhân</mat-label>
            <input matInput [formControl]="patientSearchControl" placeholder="Tìm bệnh nhân..."
                   aria-label="Tìm kiếm bệnh nhân" [matAutocomplete]="patientAuto">
            <mat-icon matSuffix>search</mat-icon>
            <mat-autocomplete #patientAuto="matAutocomplete" [displayWith]="displayPatientFn">
              @for (p of filteredPatients; track p.id) {
              <mat-option [value]="p" (onSelectionChange)="onPatientSelected(p)">
                {{ p.fullName }} - {{ p.id | slice:0:8 }}...
              </mat-option>
              }
            </mat-autocomplete>
            @if (labOrderForm.get('patientId')?.hasError('required')) {
            <mat-error>Vui lòng chọn bệnh nhân</mat-error>
            }
          </mat-form-field>

          <mat-form-field appearance="outline">
            <mat-label>Mức ưu tiên</mat-label>
            <mat-select formControlName="priorityCode" required aria-label="Chọn mức ưu tiên">
              <mat-option value="routine">Thường</mat-option>
              <mat-option value="high">Cao</mat-option>
              <mat-option value="urgent">Khẩn cấp</mat-option>
            </mat-select>
          </mat-form-field>

          <mat-form-field appearance="outline" class="full-width">
            <mat-label>Ghi chú</mat-label>
            <textarea matInput formControlName="notes" rows="2" placeholder="Ghi chú cho phiếu xét nghiệm..."
                      aria-label="Ghi chú"></textarea>
          </mat-form-field>
        </div>

        <!-- Tests section -->
        <mat-card class="tests-section">
          <mat-card-header>
            <mat-card-title>Xét nghiệm</mat-card-title>
          </mat-card-header>
          <mat-card-content>
            <div formArrayName="tests">
              @for (test of tests.controls; track i; let i = $index) {
              <div [formGroupName]="i" class="test-item">
                <div class="test-row">
                  <mat-form-field appearance="outline">
                    <mat-label>Mã xét nghiệm</mat-label>
                    <input matInput formControlName="testCode" required placeholder="VD: CBC, GPT..."
                           aria-label="Mã xét nghiệm">
                  </mat-form-field>

                  <mat-form-field appearance="outline">
                    <mat-label>Tên xét nghiệm</mat-label>
                    <input matInput formControlName="testName" required placeholder="VD: Tổng phân tích máu..."
                           aria-label="Tên xét nghiệm">
                  </mat-form-field>

                  <mat-form-field appearance="outline">
                    <mat-label>Loại mẫu</mat-label>
                    <mat-select formControlName="specimenType" required aria-label="Loại mẫu bệnh phẩm">
                      <mat-option value="Máu">Máu</mat-option>
                      <mat-option value="Huyết thanh">Huyết thanh</mat-option>
                      <mat-option value="Nước tiểu">Nước tiểu</mat-option>
                      <mat-option value="Phân">Phân</mat-option>
                      <mat-option value="Đờm">Đờm</mat-option>
                      <mat-option value="Dịch não tủy">Dịch não tủy</mat-option>
                      <mat-option value="Mô">Mô</mat-option>
                      <mat-option value="Khác">Khác</mat-option>
                    </mat-select>
                  </mat-form-field>

                  <button mat-icon-button color="warn" type="button" (click)="removeTest(i)"
                          attr.aria-label="Xóa xét nghiệm">
                    <mat-icon>delete</mat-icon>
                  </button>
                </div>
              </div>
              }
            </div>

            <button mat-stroked-button color="primary" type="button" (click)="addTest()"
                    attr.aria-label="Thêm xét nghiệm">
              <mat-icon>add</mat-icon> Thêm xét nghiệm
            </button>
          </mat-card-content>
        </mat-card>

        <div class="form-actions">
          <button mat-button type="button" routerLink="/lab">Hủy</button>
          <button mat-raised-button color="primary" type="submit"
                  [disabled]="labOrderForm.invalid || submitting">
            @if (submitting) {
            <mat-spinner diameter="18" class="btn-spinner" aria-label="Đang lưu"></mat-spinner>
            }
            {{ submitting ? 'Đang lưu...' : 'Tạo phiếu xét nghiệm' }}
          </button>
        </div>
      </form>
    </div>
  `,
    styles: [`
    .lab-order-form { padding: 24px; max-width: 900px; }
    .form-grid { display: grid; grid-template-columns: 1fr 1fr; gap: 16px; }
    .full-width { grid-column: 1 / -1; }
    .tests-section { margin: 20px 0; }
    .test-item { margin-bottom: 12px; }
    .test-row { display: flex; gap: 12px; align-items: flex-start; }
    .test-row mat-form-field { flex: 1; }
    .form-actions { margin-top: 24px; display: flex; gap: 12px; justify-content: flex-end; }
    .btn-spinner { display: inline-block; margin-right: 8px; }
  `],
})
export class LabOrderFormComponent implements OnInit, OnDestroy {
  private destroy$ = new Subject<void>();

  submitting = false;
  filteredPatients: Patient[] = [];
  patientSearchControl = new FormControl('');

  labOrderForm = this.fb.group({
    patientId: ['', Validators.required],
    providerId: ['current-provider'],
    priorityCode: ['routine', Validators.required],
    notes: [''],
    tests: this.fb.array([]),
  });

  get tests(): FormArray {
    return this.labOrderForm.get('tests') as FormArray;
  }

  constructor(
    private fb: FormBuilder,
    private labService: LabService,
    private patientService: PatientService,
    private router: Router,
    private snackBar: MatSnackBar,
    private cdr: ChangeDetectorRef,
  ) {}

  ngOnInit(): void {
    // Patient search with debounce
    this.patientSearchControl.valueChanges.pipe(
      debounceTime(300),
      distinctUntilChanged(),
      takeUntil(this.destroy$),
    ).subscribe((term) => {
      const searchTerm = term ?? '';
      if (searchTerm.length < 2) {
        this.filteredPatients = [];
        this.cdr.markForCheck();
        return;
      }
      this.patientService.search(searchTerm, 1, 20)
        .pipe(takeUntil(this.destroy$))
        .subscribe((result) => {
          this.filteredPatients = result.items;
          this.cdr.markForCheck();
        });
    });

    // Add first test row by default
    this.addTest();
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  displayPatientFn(patient: Patient): string {
    return patient ? `${patient.fullName} (${patient.id.slice(0, 8)}...)` : '';
  }

  onPatientSelected(patient: Patient): void {
    this.labOrderForm.patchValue({ patientId: patient.id });
  }

  addTest(): void {
    this.tests.push(this.fb.group({
      testCode: ['', Validators.required],
      testName: ['', Validators.required],
      specimenType: ['Máu', Validators.required],
    }));
    this.cdr.markForCheck();
  }

  removeTest(index: number): void {
    this.tests.removeAt(index);
    this.cdr.markForCheck();
  }

  onSubmit(): void {
    if (this.labOrderForm.invalid) return;

    this.submitting = true;
    const request = this.labOrderForm.value as any;

    this.labService.createLabOrder(request)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (order) => {
          this.snackBar.open('Đã tạo phiếu xét nghiệm thành công', 'Đóng', { duration: 3000 });
          this.router.navigate(['/lab', order.id]);
        },
        error: () => {
          this.submitting = false;
          this.snackBar.open('Không thể tạo phiếu xét nghiệm', 'Đóng', { duration: 5000 });
          this.cdr.markForCheck();
        },
      });
  }
}
