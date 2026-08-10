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
import {
  HisHopePageLayoutComponent, HisHopePageHeaderComponent,
  HisHopeFormLayoutComponent, HisHopeFormSectionComponent, HisHopeTranslatePipe,
} from '@his-hope/frontend-foundation';

@Component({
    selector: 'app-lab-order-form',
    standalone: true,
    imports: [
        CommonModule, ReactiveFormsModule, RouterModule,
        MatCardModule, MatFormFieldModule, MatInputModule, MatIconModule,
        MatSelectModule, MatButtonModule, MatAutocompleteModule, MatProgressSpinnerModule,
        MatSnackBarModule,
        HisHopePageLayoutComponent, HisHopePageHeaderComponent,
        HisHopeFormLayoutComponent, HisHopeFormSectionComponent, HisHopeTranslatePipe,
    ],
    changeDetection: ChangeDetectionStrategy.OnPush,
    template: `
    <hh-page-layout>
      <hh-page-header hhPageHeader
                      [title]="'labOrderForm.title' | hhTranslate:'Tạo phiếu xét nghiệm mới'"
                      [subtitle]="'labOrderForm.subtitle' | hhTranslate:'Chỉ định xét nghiệm cho bệnh nhân'" />

      <form [formGroup]="labOrderForm" (ngSubmit)="onSubmit()" novalidate>
        <hh-form-layout>
          <hh-form-section [title]="'labOrderForm.section.basic' | hhTranslate:'Thông tin phiếu xét nghiệm'" [span]="2">
            <div class="form-grid">
              <mat-form-field appearance="outline">
                <mat-label>{{ 'labOrderForm.patient' | hhTranslate:'Bệnh nhân' }}</mat-label>
                <input matInput [formControl]="patientSearchControl" [placeholder]="'labOrderForm.patientPlaceholder' | hhTranslate:'Tìm bệnh nhân...'"
                       [attr.aria-label]="'labOrderForm.patientSearchAria' | hhTranslate:'Tìm kiếm bệnh nhân'" [matAutocomplete]="patientAuto">
                <mat-icon matSuffix>search</mat-icon>
                <mat-autocomplete #patientAuto="matAutocomplete" [displayWith]="displayPatientFn">
                  @for (p of filteredPatients; track p.id) {
                  <mat-option [value]="p" (onSelectionChange)="onPatientSelected(p)">
                    {{ p.fullName }} - {{ p.id | slice:0:8 }}...
                  </mat-option>
                  }
                </mat-autocomplete>
                @if (labOrderForm.get('patientId')?.hasError('required')) {
                <mat-error>{{ 'labOrderForm.patientRequired' | hhTranslate:'Vui lòng chọn bệnh nhân' }}</mat-error>
                }
              </mat-form-field>

              <mat-form-field appearance="outline">
                <mat-label>{{ 'labOrderForm.priority' | hhTranslate:'Mức ưu tiên' }}</mat-label>
                <mat-select formControlName="priorityCode" required [attr.aria-label]="'labOrderForm.priorityAria' | hhTranslate:'Chọn mức ưu tiên'">
                  <mat-option value="routine">{{ 'labOrderForm.priority.routine' | hhTranslate:'Thường' }}</mat-option>
                  <mat-option value="high">{{ 'labOrderForm.priority.high' | hhTranslate:'Cao' }}</mat-option>
                  <mat-option value="urgent">{{ 'labOrderForm.priority.urgent' | hhTranslate:'Khẩn cấp' }}</mat-option>
                </mat-select>
              </mat-form-field>

              <mat-form-field appearance="outline" class="full-width">
                <mat-label>{{ 'labOrderForm.notes' | hhTranslate:'Ghi chú' }}</mat-label>
                <textarea matInput formControlName="notes" rows="2" [placeholder]="'labOrderForm.notesPlaceholder' | hhTranslate:'Ghi chú cho phiếu xét nghiệm...'"
                          [attr.aria-label]="'labOrderForm.notesAria' | hhTranslate:'Ghi chú'"></textarea>
              </mat-form-field>
            </div>
          </hh-form-section>
        </hh-form-layout>

        <!-- Tests section -->
        <mat-card class="tests-section">
          <mat-card-header>
            <mat-card-title>{{ 'labOrderForm.tests' | hhTranslate:'Xét nghiệm' }}</mat-card-title>
          </mat-card-header>
          <mat-card-content>
            <div formArrayName="tests">
              @for (test of tests.controls; track i; let i = $index) {
              <div [formGroupName]="i" class="test-item">
                <div class="test-row">
                  <mat-form-field appearance="outline">
                    <mat-label>{{ 'labOrderForm.testCode' | hhTranslate:'Mã xét nghiệm' }}</mat-label>
                    <input matInput formControlName="testCode" required [placeholder]="'labOrderForm.testCodePlaceholder' | hhTranslate:'VD: CBC, GPT...'"
                           [attr.aria-label]="'labOrderForm.testCodeAria' | hhTranslate:'Mã xét nghiệm'">
                  </mat-form-field>

                  <mat-form-field appearance="outline">
                    <mat-label>{{ 'labOrderForm.testName' | hhTranslate:'Tên xét nghiệm' }}</mat-label>
                    <input matInput formControlName="testName" required [placeholder]="'labOrderForm.testNamePlaceholder' | hhTranslate:'VD: Tổng phân tích máu...'"
                           [attr.aria-label]="'labOrderForm.testNameAria' | hhTranslate:'Tên xét nghiệm'">
                  </mat-form-field>

                  <mat-form-field appearance="outline">
                    <mat-label>{{ 'labOrderForm.specimen' | hhTranslate:'Loại mẫu' }}</mat-label>
                    <mat-select formControlName="specimenType" required [attr.aria-label]="'labOrderForm.specimenAria' | hhTranslate:'Loại mẫu bệnh phẩm'">
                      <mat-option value="blood">{{ 'labOrderForm.specimen.blood' | hhTranslate:'Máu' }}</mat-option>
                      <mat-option value="serum">{{ 'labOrderForm.specimen.serum' | hhTranslate:'Huyết thanh' }}</mat-option>
                      <mat-option value="urine">{{ 'labOrderForm.specimen.urine' | hhTranslate:'Nước tiểu' }}</mat-option>
                      <mat-option value="stool">{{ 'labOrderForm.specimen.stool' | hhTranslate:'Phân' }}</mat-option>
                      <mat-option value="sputum">{{ 'labOrderForm.specimen.sputum' | hhTranslate:'Đờm' }}</mat-option>
                      <mat-option value="csf">{{ 'labOrderForm.specimen.csf' | hhTranslate:'Dịch não tủy' }}</mat-option>
                      <mat-option value="tissue">{{ 'labOrderForm.specimen.tissue' | hhTranslate:'Mô' }}</mat-option>
                      <mat-option value="other">{{ 'labOrderForm.specimen.other' | hhTranslate:'Khác' }}</mat-option>
                    </mat-select>
                  </mat-form-field>

                  <button mat-icon-button color="warn" type="button" (click)="removeTest(i)"
                          [attr.aria-label]="'labOrderForm.removeTest' | hhTranslate:'Xóa xét nghiệm'">
                    <mat-icon>delete</mat-icon>
                  </button>
                </div>
              </div>
              }
            </div>

            <button mat-stroked-button color="primary" type="button" (click)="addTest()"
                    [attr.aria-label]="'labOrderForm.addTest' | hhTranslate:'Thêm xét nghiệm'">
              <mat-icon>add</mat-icon> {{ 'labOrderForm.addTest' | hhTranslate:'Thêm xét nghiệm' }}
            </button>
          </mat-card-content>
        </mat-card>

        <div class="form-actions">
          <button mat-button type="button" routerLink="/lab">{{ 'common.cancel' | hhTranslate:'Hủy' }}</button>
          <button mat-raised-button color="primary" type="submit"
                  [disabled]="labOrderForm.invalid || submitting">
            @if (submitting) {
            <mat-spinner diameter="18" class="btn-spinner" [attr.aria-label]="'common.saving' | hhTranslate:'Đang lưu'"></mat-spinner>
            }
            {{ submitting ? ('common.saving' | hhTranslate:'Đang lưu...') : ('labOrderForm.save' | hhTranslate:'Tạo phiếu xét nghiệm') }}
          </button>
        </div>
      </form>
    </hh-page-layout>
  `,
    styles: [`
    .form-grid { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: var(--form-field-gap, 16px); }
    .form-grid .full-width { grid-column: 1 / -1; }
    @media (max-width: 768px) { .form-grid { grid-template-columns: 1fr; } }
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
      specimenType: ['blood', Validators.required],
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
