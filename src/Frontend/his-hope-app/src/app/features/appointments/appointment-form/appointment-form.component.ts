import { Component, OnDestroy, OnInit, ChangeDetectionStrategy, ChangeDetectorRef, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, Validators, ReactiveFormsModule } from '@angular/forms';
import { Router, ActivatedRoute, RouterModule } from '@angular/router';
import { Subject, Observable, of, debounceTime, distinctUntilChanged, switchMap, takeUntil } from 'rxjs';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatIconModule } from '@angular/material/icon';
import { MatSelectModule } from '@angular/material/select';
import { MatButtonModule } from '@angular/material/button';
import { MatAutocompleteModule } from '@angular/material/autocomplete';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatNativeDateModule } from '@angular/material/core';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { AppointmentService } from '@core/services/appointment.service';
import { PatientService } from '@core/services/patient.service';
import { Patient } from '@core/models/patient.model';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import {
  HisHopePageLayoutComponent, HisHopePageHeaderComponent,
  HisHopeFormLayoutComponent, HisHopeFormSectionComponent, HisHopeTranslatePipe, HisHopeI18nService,
} from '@his-hope/frontend-foundation';

interface ProviderOption {
  id: string;
  fullName: string;
  specialty?: string;
}

@Component({
    selector: 'app-appointment-form',
    standalone: true,
    imports: [
        CommonModule, ReactiveFormsModule, RouterModule,
        MatFormFieldModule, MatInputModule, MatIconModule, MatSelectModule,
        MatButtonModule, MatAutocompleteModule, MatDatepickerModule, MatNativeDateModule,
        MatProgressSpinnerModule, MatSnackBarModule,
        HisHopePageLayoutComponent, HisHopePageHeaderComponent,
        HisHopeFormLayoutComponent, HisHopeFormSectionComponent, HisHopeTranslatePipe,
    ],
    changeDetection: ChangeDetectionStrategy.OnPush,
    template: `
    <hh-page-layout>
      <hh-page-header hhPageHeader
                      [title]="'appointmentForm.title' | hhTranslate:'Đặt lịch hẹn'"
                      [subtitle]="'appointmentForm.subtitle' | hhTranslate:'Lên lịch hẹn khám cho bệnh nhân'" />

      <form [formGroup]="appointmentForm" (ngSubmit)="onSubmit()" novalidate>
        <hh-form-layout>
          <hh-form-section [title]="'appointmentForm.section.basic' | hhTranslate:'Thông tin lịch hẹn'" [span]="2">
            <div class="form-grid">
              <!-- Patient Autocomplete -->
              <mat-form-field appearance="outline" class="full-width">
                <mat-label>{{ 'appointmentForm.patient' | hhTranslate:'Tìm bệnh nhân' }}</mat-label>
                <input matInput
                       [matAutocomplete]="patientAuto"
                       formControlName="patientSearch"
                       required
                       [placeholder]="'appointmentForm.patientPlaceholder' | hhTranslate:'Nhập tên bệnh nhân...'">
                <mat-icon matSuffix>search</mat-icon>
                <mat-hint>{{ 'appointmentForm.patientHint' | hhTranslate:'Gõ tên bệnh nhân để tìm kiếm' }}</mat-hint>
                <mat-autocomplete #patientAuto="matAutocomplete" [displayWith]="displayPatientName">
                  @for (p of filteredPatients; track p.id) {
                  <mat-option [value]="p">
                    <span>{{ p.fullName }}</span>
                    <small class="option-detail"> - {{ p.genderName }}, {{ p.age }}t - {{ p.phone }}</small>
                  </mat-option>
                  }
                  @if (filteredPatients.length === 0 && patientSearchTerm) {
                  <mat-option disabled>
                    {{ 'appointmentForm.noPatients' | hhTranslate:'Không tìm thấy bệnh nhân' }}
                  </mat-option>
                  }
                </mat-autocomplete>
              </mat-form-field>

              <!-- Hidden patient ID field -->
              <input type="hidden" formControlName="patientId">

              <!-- Provider Select -->
              <mat-form-field appearance="outline">
                <mat-label>{{ 'appointmentForm.provider' | hhTranslate:'Bác sĩ' }}</mat-label>
                <mat-select formControlName="providerId" required>
                  <mat-option [value]="" disabled>{{ 'appointmentForm.providerPlaceholder' | hhTranslate:'Chọn bác sĩ' }}</mat-option>
                  @for (prov of providers; track prov.id) {
                  <mat-option [value]="prov.id">
                    {{ prov.fullName }} @if (prov.specialty) { <small>- {{ prov.specialty }}</small> }
                  </mat-option>
                  }
                </mat-select>
              </mat-form-field>

              <mat-form-field appearance="outline">
                <mat-label>{{ 'appointmentForm.date' | hhTranslate:'Ngày' }}</mat-label>
                <input matInput [matDatepicker]="picker" formControlName="scheduledDate" required>
                <mat-datepicker-toggle matSuffix [for]="picker"></mat-datepicker-toggle>
                <mat-datepicker #picker></mat-datepicker>
              </mat-form-field>

              <mat-form-field appearance="outline">
                <mat-label>{{ 'appointmentForm.startTime' | hhTranslate:'Giờ bắt đầu' }}</mat-label>
                <input matInput type="time" formControlName="startTime" required>
              </mat-form-field>

              <mat-form-field appearance="outline">
                <mat-label>{{ 'appointmentForm.duration' | hhTranslate:'Thời lượng (phút)' }}</mat-label>
                <input matInput type="number" formControlName="durationMinutes" value="30" required>
              </mat-form-field>

              <mat-form-field appearance="outline">
                <mat-label>{{ 'appointmentForm.type' | hhTranslate:'Loại' }}</mat-label>
                <mat-select formControlName="typeCode" required>
                  <mat-option value="CHECKUP">{{ 'appointmentForm.type.checkup' | hhTranslate:'Khám tổng quát' }}</mat-option>
                  <mat-option value="CONSULT">{{ 'appointmentForm.type.consult' | hhTranslate:'Tư vấn' }}</mat-option>
                  <mat-option value="FOLLOWUP">{{ 'appointmentForm.type.followup' | hhTranslate:'Tái khám' }}</mat-option>
                  <mat-option value="EMERG">{{ 'appointmentForm.type.emergency' | hhTranslate:'Cấp cứu' }}</mat-option>
                  <mat-option value="PROCED">{{ 'appointmentForm.type.procedure' | hhTranslate:'Thủ thuật' }}</mat-option>
                  <mat-option value="VACCINE">{{ 'appointmentForm.type.vaccine' | hhTranslate:'Tiêm chủng' }}</mat-option>
                  <mat-option value="LAB">{{ 'appointmentForm.type.lab' | hhTranslate:'Xét nghiệm' }}</mat-option>
                  <mat-option value="TELE">{{ 'appointmentForm.type.tele' | hhTranslate:'Khám từ xa' }}</mat-option>
                </mat-select>
              </mat-form-field>

              <mat-form-field appearance="outline" class="full-width">
                <mat-label>{{ 'appointmentForm.reason' | hhTranslate:'Lý do' }}</mat-label>
                <textarea matInput formControlName="reason" rows="2"></textarea>
              </mat-form-field>

              <mat-form-field appearance="outline">
                <mat-label>{{ 'appointmentForm.location' | hhTranslate:'Địa điểm' }}</mat-label>
                <input matInput formControlName="location">
              </mat-form-field>
            </div>
          </hh-form-section>
        </hh-form-layout>

        <div class="form-actions">
          <button mat-button type="button" routerLink="/appointments">{{ 'common.cancel' | hhTranslate:'Hủy' }}</button>
          <button mat-raised-button color="primary" type="submit"
                  [disabled]="appointmentForm.invalid || submitting">
            @if (submitting) {
            <mat-spinner diameter="18" class="btn-spinner" [attr.aria-label]="'common.saving' | hhTranslate:'Đang đặt lịch'"></mat-spinner>
            }
            {{ submitting ? ('common.saving' | hhTranslate:'Đang đặt...') : ('appointmentForm.save' | hhTranslate:'Đặt lịch') }}
          </button>
        </div>
      </form>
    </hh-page-layout>
  `,
    styles: [`
    .form-grid { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: var(--form-field-gap, 16px); }
    .form-grid .full-width { grid-column: 1 / -1; }
    @media (max-width: 768px) { .form-grid { grid-template-columns: 1fr; } }
    .form-actions { display: flex; gap: 12px; justify-content: flex-end; margin-top: 16px; }
    .option-detail { color: var(--text-muted, #999); }
  `],
})
export class AppointmentFormComponent implements OnInit, OnDestroy {
  private destroy$ = new Subject<void>();
  private readonly i18n = inject(HisHopeI18nService);

  submitting = false;
  filteredPatients: Patient[] = [];
  patientSearchTerm = '';
  providers: ProviderOption[] = [
    { id: 'provider-001', fullName: 'Nguyễn Văn A', specialty: 'Nội khoa' },
    { id: 'provider-002', fullName: 'Trần Thị B', specialty: 'Nhi khoa' },
    { id: 'provider-003', fullName: 'Lê Văn C', specialty: 'Sản khoa' },
    { id: 'provider-004', fullName: 'Phạm Thị D', specialty: 'Ngoại khoa' },
    { id: 'provider-005', fullName: 'Hoàng Văn E', specialty: 'Tim mạch' },
    { id: 'provider-006', fullName: 'Đỗ Thị F', specialty: 'Thần kinh' },
    { id: 'provider-007', fullName: 'Vũ Văn G', specialty: 'Da liễu' },
    { id: 'provider-008', fullName: 'Bùi Thị H', specialty: 'Mắt' },
    { id: 'provider-009', fullName: 'Đặng Văn I', specialty: 'Tai mũi họng' },
    { id: 'provider-010', fullName: 'Ngô Thị K', specialty: 'Răng hàm mặt' },
  ];

  appointmentForm = this.fb.group({
    patientSearch: ['', Validators.required],
    patientId: ['', Validators.required],
    providerId: ['', Validators.required],
    scheduledDate: ['', Validators.required],
    startTime: ['', Validators.required],
    durationMinutes: [30, Validators.required],
    typeCode: ['CHECKUP', Validators.required],
    reason: [''],
    location: [''],
  });

  constructor(
    private fb: FormBuilder,
    private appointmentService: AppointmentService,
    private patientService: PatientService,
    private router: Router,
    private route: ActivatedRoute,
    private snackBar: MatSnackBar,
    private cdr: ChangeDetectorRef,
  ) {}

  ngOnInit(): void {
    // Listen for patientId query param (from patient detail page)
    const patientIdParam = this.route.snapshot.queryParamMap.get('patientId');
    if (patientIdParam) {
      this.patientService.getById(patientIdParam)
        .pipe(takeUntil(this.destroy$))
        .subscribe(p => {
          this.selectPatient(p);
          this.cdr.markForCheck();
        });
    }

    // Patient search autocomplete
    this.appointmentForm.get('patientSearch')!.valueChanges
      .pipe(
        debounceTime(300),
        distinctUntilChanged(),
        switchMap(value => {
          if (typeof value === 'string' && value.trim().length >= 2) {
            this.patientSearchTerm = value;
            return this.patientService.search(value, 1, 10);
          }
          this.patientSearchTerm = '';
          return of({ items: [], totalCount: 0, page: 1, pageSize: 10, hasNextPage: false, hasPreviousPage: false });
        }),
        takeUntil(this.destroy$),
      )
      .subscribe(result => {
        this.filteredPatients = result.items;
        this.cdr.markForCheck();
      });
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  displayPatientName(patient: Patient | string): string {
    if (typeof patient === 'string') return patient;
    return patient?.fullName || '';
  }

  selectPatient(patient: Patient): void {
    this.appointmentForm.patchValue({
      patientSearch: patient.fullName,
      patientId: patient.id,
    });
  }

  onSubmit(): void {
    if (this.appointmentForm.invalid) return;
    this.submitting = true;

    const formValue = this.appointmentForm.value;
    const request = {
      patientId: formValue.patientId!,
      providerId: formValue.providerId!,
      scheduledDate: new Date(formValue.scheduledDate!).toISOString(),
      startTime: formValue.startTime!,
      durationMinutes: formValue.durationMinutes!,
      typeCode: formValue.typeCode!,
      reason: formValue.reason || undefined,
      location: formValue.location || undefined,
    };

    this.appointmentService.schedule(request)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: () => {
          this.snackBar.open(this.i18n.t('appointmentForm.saveSuccess', 'Đã đặt lịch hẹn thành công'), this.i18n.t('common.close', 'Đóng'), { duration: 3000 });
          this.router.navigate(['/appointments']);
          this.cdr.markForCheck();
        },
        error: () => {
          this.submitting = false;
          this.snackBar.open(this.i18n.t('appointmentForm.saveFailed', 'Không thể đặt lịch hẹn'), this.i18n.t('common.close', 'Đóng'), { duration: 5000 });
          this.cdr.markForCheck();
        },
      });
  }
}
