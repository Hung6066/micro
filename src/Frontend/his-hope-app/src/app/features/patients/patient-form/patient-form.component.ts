import {
  Component,
  OnInit,
  OnDestroy,
  ChangeDetectionStrategy,
  ChangeDetectorRef,
} from "@angular/core";
import { CommonModule } from "@angular/common";
import { FormBuilder, Validators, ReactiveFormsModule } from "@angular/forms";
import { ActivatedRoute, Router, RouterModule } from "@angular/router";
import { Subject, takeUntil } from "rxjs";
import { MatFormFieldModule } from "@angular/material/form-field";
import { MatInputModule } from "@angular/material/input";
import { MatSelectModule } from "@angular/material/select";
import { MatButtonModule } from "@angular/material/button";
import { MatDatepickerModule } from "@angular/material/datepicker";
import { MatNativeDateModule } from "@angular/material/core";
import { MatProgressSpinnerModule } from "@angular/material/progress-spinner";
import { PatientService } from "@core/services/patient.service";
import { MatSnackBar, MatSnackBarModule } from "@angular/material/snack-bar";
import { HisHopeTranslatePipe } from "@his-hope/frontend-foundation/i18n";
import {
  HisHopePageLayoutComponent,
  HisHopePageHeaderComponent,
  HisHopeFormLayoutComponent,
  HisHopeFormSectionComponent,
} from "@his-hope/frontend-foundation/ui";

@Component({
  selector: "app-patient-form",
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    RouterModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatButtonModule,
    MatDatepickerModule,
    MatNativeDateModule,
    MatProgressSpinnerModule,
    MatSnackBarModule,
    HisHopePageLayoutComponent,
    HisHopePageHeaderComponent,
    HisHopeFormLayoutComponent,
    HisHopeFormSectionComponent,
    HisHopeTranslatePipe,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <hh-page-layout>
      <hh-page-header
        hhPageHeader
        [title]="
          (isEdit ? 'patientForm.title.edit' : 'patientForm.title.create')
            | hhTranslate
              : (isEdit ? 'Chỉnh sửa bệnh nhân' : 'Thêm bệnh nhân mới')
        "
        [subtitle]="
          'patientForm.subtitle'
            | hhTranslate: 'Quản lý thông tin hồ sơ bệnh nhân'
        "
      />

      <form [formGroup]="patientForm" (ngSubmit)="onSubmit()" novalidate>
        <hh-form-layout>
          <hh-form-section
            [title]="
              'patientForm.section.identity' | hhTranslate: 'Thông tin cá nhân'
            "
            [span]="2"
          >
            <div class="form-grid">
              <mat-form-field appearance="outline">
                <mat-label>{{
                  "patientForm.lastName" | hhTranslate: "Họ"
                }}</mat-label>
                <input matInput formControlName="lastName" required />
              </mat-form-field>

              <mat-form-field appearance="outline">
                <mat-label>{{
                  "patientForm.middleName" | hhTranslate: "Tên đệm"
                }}</mat-label>
                <input matInput formControlName="middleName" />
              </mat-form-field>

              <mat-form-field appearance="outline">
                <mat-label>{{
                  "patientForm.firstName" | hhTranslate: "Tên"
                }}</mat-label>
                <input matInput formControlName="firstName" required />
              </mat-form-field>

              <mat-form-field appearance="outline">
                <mat-label>{{
                  "patientForm.dateOfBirth" | hhTranslate: "Ngày sinh"
                }}</mat-label>
                <input
                  matInput
                  [matDatepicker]="picker"
                  formControlName="dateOfBirth"
                  required
                />
                <mat-datepicker-toggle
                  matSuffix
                  [for]="picker"
                ></mat-datepicker-toggle>
                <mat-datepicker #picker></mat-datepicker>
              </mat-form-field>

              <mat-form-field appearance="outline">
                <mat-label>{{
                  "patientForm.gender" | hhTranslate: "Giới tính"
                }}</mat-label>
                <mat-select formControlName="genderCode" required>
                  <mat-option value="M">{{
                    "patientForm.gender.male" | hhTranslate: "Nam"
                  }}</mat-option>
                  <mat-option value="F">{{
                    "patientForm.gender.female" | hhTranslate: "Nữ"
                  }}</mat-option>
                  <mat-option value="O">{{
                    "patientForm.gender.other" | hhTranslate: "Khác"
                  }}</mat-option>
                </mat-select>
              </mat-form-field>

              <mat-form-field appearance="outline">
                <mat-label>{{
                  "patientForm.phone" | hhTranslate: "Số điện thoại"
                }}</mat-label>
                <input
                  matInput
                  formControlName="phone"
                  required
                  placeholder="+84..."
                />
              </mat-form-field>

              <mat-form-field appearance="outline">
                <mat-label>{{
                  "patientForm.email" | hhTranslate: "Email"
                }}</mat-label>
                <input matInput formControlName="email" type="email" />
              </mat-form-field>

              <mat-form-field appearance="outline">
                <mat-label>{{
                  "patientForm.nationalId" | hhTranslate: "CMND/CCCD"
                }}</mat-label>
                <input matInput formControlName="nationalId" />
              </mat-form-field>
            </div>
          </hh-form-section>

          <hh-form-section
            [title]="'patientForm.section.address' | hhTranslate: 'Địa chỉ'"
            [span]="2"
          >
            <div class="form-grid">
              <mat-form-field appearance="outline" class="full-width">
                <mat-label>{{
                  "patientForm.street" | hhTranslate: "Địa chỉ"
                }}</mat-label>
                <input matInput formControlName="street" required />
              </mat-form-field>

              <mat-form-field appearance="outline">
                <mat-label>{{
                  "patientForm.district" | hhTranslate: "Quận/Huyện"
                }}</mat-label>
                <input matInput formControlName="district" />
              </mat-form-field>

              <mat-form-field appearance="outline">
                <mat-label>{{
                  "patientForm.city" | hhTranslate: "Thành phố"
                }}</mat-label>
                <input matInput formControlName="city" required />
              </mat-form-field>

              <mat-form-field appearance="outline">
                <mat-label>{{
                  "patientForm.province" | hhTranslate: "Tỉnh"
                }}</mat-label>
                <input matInput formControlName="province" required />
              </mat-form-field>

              <mat-form-field appearance="outline">
                <mat-label>{{
                  "patientForm.country" | hhTranslate: "Quốc gia"
                }}</mat-label>
                <input matInput formControlName="country" required />
              </mat-form-field>

              <mat-form-field appearance="outline">
                <mat-label>{{
                  "patientForm.insuranceId" | hhTranslate: "Mã BHYT"
                }}</mat-label>
                <input matInput formControlName="insuranceId" />
              </mat-form-field>
            </div>
          </hh-form-section>
        </hh-form-layout>

        <div class="form-actions">
          <button mat-button type="button" routerLink="/patients">
            {{ "common.cancel" | hhTranslate: "Hủy" }}
          </button>
          <button
            mat-raised-button
            color="primary"
            type="submit"
            [disabled]="patientForm.invalid || submitting"
          >
            @if (submitting) {
              <mat-spinner
                diameter="18"
                class="btn-spinner"
                [attr.aria-label]="'common.saving' | hhTranslate: 'Đang lưu'"
              ></mat-spinner>
            }
            {{
              submitting
                ? ("common.saving" | hhTranslate: "Đang lưu...")
                : ("patientForm.save" | hhTranslate: "Lưu bệnh nhân")
            }}
          </button>
        </div>
      </form>
    </hh-page-layout>
  `,
  styles: [
    `
      .form-grid {
        display: grid;
        grid-template-columns: repeat(2, minmax(0, 1fr));
        gap: var(--form-field-gap, 16px);
      }
      .form-grid .full-width {
        grid-column: 1 / -1;
      }
      @media (max-width: 768px) {
        .form-grid {
          grid-template-columns: 1fr;
        }
      }
      .form-actions {
        margin-top: 24px;
        display: flex;
        gap: 12px;
        justify-content: flex-end;
      }
    `,
  ],
})
export class PatientFormComponent implements OnInit, OnDestroy {
  private destroy$ = new Subject<void>();

  isEdit = false;
  patientId?: string;
  submitting = false;

  patientForm = this.fb.group({
    firstName: ["", Validators.required],
    lastName: ["", Validators.required],
    middleName: [""],
    dateOfBirth: ["", Validators.required],
    genderCode: ["", Validators.required],
    phone: ["", Validators.required],
    email: [""],
    nationalId: [""],
    street: ["", Validators.required],
    district: [""],
    city: ["", Validators.required],
    province: ["", Validators.required],
    postalCode: [""],
    country: ["Vietnam", Validators.required],
    insuranceId: [""],
  });

  constructor(
    private fb: FormBuilder,
    private patientService: PatientService,
    private route: ActivatedRoute,
    private router: Router,
    private snackBar: MatSnackBar,
    private cdr: ChangeDetectorRef,
  ) {}

  ngOnInit(): void {
    this.patientId = this.route.snapshot.params["id"];
    if (this.patientId) {
      this.isEdit = true;
      this.patientService
        .getById(this.patientId)
        .pipe(takeUntil(this.destroy$))
        .subscribe((patient) => {
          this.patientForm.patchValue(patient as any);
          this.cdr.markForCheck();
        });
    }
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  onSubmit(): void {
    if (this.patientForm.invalid) return;

    this.submitting = true;
    const request = this.patientForm.value as any;

    const action = this.isEdit
      ? this.patientService.update(this.patientId!, request)
      : this.patientService.create(request);

    action.pipe(takeUntil(this.destroy$)).subscribe({
      next: (patient) => {
        this.snackBar.open(
          `Patient ${this.isEdit ? "updated" : "created"} successfully`,
          "Close",
          { duration: 3000 },
        );
        this.router.navigate(["/patients", patient.id]);
      },
      error: () => {
        this.submitting = false;
        this.snackBar.open("Failed to save patient", "Close", {
          duration: 5000,
        });
        this.cdr.markForCheck();
      },
    });
  }
}
