import { Component, OnDestroy, OnInit, ChangeDetectionStrategy, ChangeDetectorRef } from '@angular/core';
import { FormBuilder, Validators } from '@angular/forms';
import { Router, ActivatedRoute } from '@angular/router';
import { Subject, Observable, of, debounceTime, distinctUntilChanged, switchMap, takeUntil } from 'rxjs';
import * as moment from 'moment';
import { AppointmentService } from '@core/services/appointment.service';
import { PatientService } from '@core/services/patient.service';
import { Patient } from '@core/models/patient.model';
import { MatSnackBar } from '@angular/material/snack-bar';

interface ProviderOption {
  id: string;
  fullName: string;
  specialty?: string;
}

@Component({
    selector: 'app-appointment-form',
    changeDetection: ChangeDetectionStrategy.OnPush,
    template: `
    <div class="appointment-form">
      <h1>Đặt lịch hẹn</h1>
      <form [formGroup]="appointmentForm" (ngSubmit)="onSubmit()" class="form">

        <!-- Patient Autocomplete -->
        <mat-form-field appearance="outline">
          <mat-label>Tìm bệnh nhân</mat-label>
          <input matInput
                 [matAutocomplete]="patientAuto"
                 formControlName="patientSearch"
                 required
                 placeholder="Nhập tên bệnh nhân...">
          <mat-icon matSuffix>search</mat-icon>
          <mat-hint>Gõ tên bệnh nhân để tìm kiếm</mat-hint>
          <mat-autocomplete #patientAuto="matAutocomplete" [displayWith]="displayPatientName">
            <mat-option *ngFor="let p of filteredPatients" [value]="p">
              <span>{{ p.fullName }}</span>
              <small class="option-detail"> - {{ p.genderName }}, {{ p.age }}t - {{ p.phone }}</small>
            </mat-option>
            <mat-option *ngIf="filteredPatients.length === 0 && patientSearchTerm" disabled>
              Không tìm thấy bệnh nhân
            </mat-option>
          </mat-autocomplete>
        </mat-form-field>

        <!-- Hidden patient ID field -->
        <input type="hidden" formControlName="patientId">

        <!-- Provider Select -->
        <mat-form-field appearance="outline">
          <mat-label>Bác sĩ</mat-label>
          <mat-select formControlName="providerId" required>
            <mat-option [value]="" disabled>Chọn bác sĩ</mat-option>
            <mat-option *ngFor="let prov of providers" [value]="prov.id">
              {{ prov.fullName }} <small *ngIf="prov.specialty">- {{ prov.specialty }}</small>
            </mat-option>
          </mat-select>
        </mat-form-field>

        <mat-form-field appearance="outline">
          <mat-label>Ngày</mat-label>
          <input matInput [matDatepicker]="picker" formControlName="scheduledDate" required>
          <mat-datepicker-toggle matSuffix [for]="picker"></mat-datepicker-toggle>
          <mat-datepicker #picker></mat-datepicker>
        </mat-form-field>

        <mat-form-field appearance="outline">
          <mat-label>Giờ bắt đầu</mat-label>
          <input matInput type="time" formControlName="startTime" required>
        </mat-form-field>

        <mat-form-field appearance="outline">
          <mat-label>Thời lượng (phút)</mat-label>
          <input matInput type="number" formControlName="durationMinutes" value="30" required>
        </mat-form-field>

        <mat-form-field appearance="outline">
          <mat-label>Loại</mat-label>
          <mat-select formControlName="typeCode" required>
            <mat-option value="CHECKUP">Khám tổng quát</mat-option>
            <mat-option value="CONSULT">Tư vấn</mat-option>
            <mat-option value="FOLLOWUP">Tái khám</mat-option>
            <mat-option value="EMERG">Cấp cứu</mat-option>
            <mat-option value="PROCED">Thủ thuật</mat-option>
            <mat-option value="VACCINE">Tiêm chủng</mat-option>
            <mat-option value="LAB">Xét nghiệm</mat-option>
            <mat-option value="TELE">Khám từ xa</mat-option>
          </mat-select>
        </mat-form-field>

        <mat-form-field appearance="outline" class="full-width">
          <mat-label>Lý do</mat-label>
          <textarea matInput formControlName="reason" rows="2"></textarea>
        </mat-form-field>

        <mat-form-field appearance="outline">
          <mat-label>Địa điểm</mat-label>
          <input matInput formControlName="location">
        </mat-form-field>

        <div class="form-actions">
          <button mat-button type="button" routerLink="/appointments">Hủy</button>
          <button mat-raised-button color="primary" type="submit"
                  [disabled]="appointmentForm.invalid || submitting">
            <mat-spinner diameter="18" *ngIf="submitting" class="btn-spinner" aria-label="Đang đặt lịch"></mat-spinner>
            {{ submitting ? 'Đang đặt...' : 'Đặt lịch' }}
          </button>
        </div>
      </form>
    </div>
  `,
    styles: [`
    .appointment-form { padding: 24px; max-width: 600px; }
    .form { display: flex; flex-direction: column; gap: 16px; }
    .full-width { width: 100%; }
    .form-actions { display: flex; gap: 12px; justify-content: flex-end; margin-top: 16px; }
    .option-detail { color: #999; }
  `],
    standalone: false
})
export class AppointmentFormComponent implements OnInit, OnDestroy {
  private destroy$ = new Subject<void>();

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
      scheduledDate: moment(formValue.scheduledDate!).toISOString(),
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
          this.snackBar.open('Đã đặt lịch hẹn thành công', 'Đóng', { duration: 3000 });
          this.router.navigate(['/appointments']);
          this.cdr.markForCheck();
        },
        error: () => {
          this.submitting = false;
          this.snackBar.open('Không thể đặt lịch hẹn', 'Đóng', { duration: 5000 });
          this.cdr.markForCheck();
        },
      });
  }
}
