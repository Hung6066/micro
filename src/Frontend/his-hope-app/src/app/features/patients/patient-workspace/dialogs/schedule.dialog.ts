import {
  Component,
  Inject,
  OnDestroy,
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  inject,
} from "@angular/core";
import {
  FormBuilder,
  FormGroup,
  Validators,
  ReactiveFormsModule,
} from "@angular/forms";
import {
  MatDialogRef,
  MAT_DIALOG_DATA,
  MatDialogModule,
} from "@angular/material/dialog";
import { MatButtonModule } from "@angular/material/button";
import { MatFormFieldModule } from "@angular/material/form-field";
import { MatInputModule } from "@angular/material/input";
import { MatSelectModule } from "@angular/material/select";
import { MatDatepickerModule } from "@angular/material/datepicker";
import { MatNativeDateModule } from "@angular/material/core";
import { MatIconModule } from "@angular/material/icon";
import { MatProgressSpinnerModule } from "@angular/material/progress-spinner";
import { MatSnackBar, MatSnackBarModule } from "@angular/material/snack-bar";
import { CommonModule } from "@angular/common";
import { Subject, takeUntil } from "rxjs";
import { AppointmentService } from "@core/services/appointment.service";
import { AuthService } from "@core/services/auth.service";
import {
  HisHopeTranslatePipe,
  HisHopeI18nService,
} from "@his-hope/frontend-foundation/i18n";
import {
  HisHopeCreateDialogShellComponent,
  HisHopeFormLayoutComponent,
  HisHopeFormSectionComponent,
} from "@his-hope/frontend-foundation/ui";

export interface ScheduleData {
  patientId: string;
  patientName: string;
}

const TIME_SLOTS = [
  "07:00",
  "07:30",
  "08:00",
  "08:30",
  "09:00",
  "09:30",
  "10:00",
  "10:30",
  "11:00",
  "11:30",
  "13:00",
  "13:30",
  "14:00",
  "14:30",
  "15:00",
  "15:30",
  "16:00",
  "16:30",
];

@Component({
  selector: "app-schedule-dialog",
  imports: [
    CommonModule,
    ReactiveFormsModule,
    MatDialogModule,
    MatButtonModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatDatepickerModule,
    MatNativeDateModule,
    MatIconModule,
    MatProgressSpinnerModule,
    MatSnackBarModule,
    HisHopeCreateDialogShellComponent,
    HisHopeFormLayoutComponent,
    HisHopeFormSectionComponent,
    HisHopeTranslatePipe,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <form [formGroup]="form" (ngSubmit)="save()" novalidate>
      <hh-create-dialog-shell
        [title]="'schedule.title' | hhTranslate: 'Đặt lịch hẹn'"
        [subtitle]="
          'schedule.subtitle' | hhTranslate: 'Lên lịch hẹn khám cho bệnh nhân'
        "
      >
        <div hhCreateDialogContent>
          @if (data.patientName) {
            <div class="patient-info">
              <mat-icon>person</mat-icon>
              <span>{{ data.patientName }}</span>
            </div>
          }
          <hh-form-layout>
            <hh-form-section
              [title]="
                'schedule.section.appointment'
                  | hhTranslate: 'Thông tin lịch hẹn'
              "
              [span]="2"
            >
              <div class="form-grid">
                <mat-form-field appearance="outline">
                  <mat-label>{{
                    "schedule.date" | hhTranslate: "Ngày hẹn"
                  }}</mat-label>
                  <input
                    matInput
                    [matDatepicker]="picker"
                    formControlName="scheduledDate"
                    required
                  />
                  <mat-datepicker-toggle
                    matSuffix
                    [for]="picker"
                  ></mat-datepicker-toggle>
                  <mat-datepicker #picker></mat-datepicker>
                  @if (form.get("scheduledDate")?.hasError("required")) {
                    <mat-error>{{
                      "schedule.dateRequired"
                        | hhTranslate: "Vui lòng chọn ngày"
                    }}</mat-error>
                  }
                </mat-form-field>

                <mat-form-field appearance="outline">
                  <mat-label>{{
                    "schedule.time" | hhTranslate: "Giờ hẹn"
                  }}</mat-label>
                  <mat-select formControlName="startTime" required>
                    @for (slot of timeSlots; track slot) {
                      <mat-option [value]="slot">{{ slot }}</mat-option>
                    }
                  </mat-select>
                  @if (form.get("startTime")?.hasError("required")) {
                    <mat-error>{{
                      "schedule.timeRequired" | hhTranslate: "Vui lòng chọn giờ"
                    }}</mat-error>
                  }
                </mat-form-field>

                <mat-form-field appearance="outline">
                  <mat-label>{{
                    "schedule.type" | hhTranslate: "Loại hẹn"
                  }}</mat-label>
                  <mat-select formControlName="typeCode" required>
                    <mat-option value="consultation">{{
                      "schedule.type.consultation" | hhTranslate: "Khám bệnh"
                    }}</mat-option>
                    <mat-option value="follow_up">{{
                      "schedule.type.followup" | hhTranslate: "Tái khám"
                    }}</mat-option>
                    <mat-option value="emergency">{{
                      "schedule.type.emergency" | hhTranslate: "Cấp cứu"
                    }}</mat-option>
                    <mat-option value="procedure">{{
                      "schedule.type.procedure" | hhTranslate: "Thủ thuật"
                    }}</mat-option>
                  </mat-select>
                </mat-form-field>

                <mat-form-field appearance="outline">
                  <mat-label>{{
                    "schedule.location" | hhTranslate: "Phòng / Địa điểm"
                  }}</mat-label>
                  <input
                    matInput
                    formControlName="location"
                    [placeholder]="
                      'schedule.locationPlaceholder'
                        | hhTranslate: 'VD: Phòng khám số 2 - Tầng 1'
                    "
                  />
                </mat-form-field>

                <mat-form-field appearance="outline" class="full-width">
                  <mat-label>{{
                    "schedule.reason" | hhTranslate: "Lý do hẹn"
                  }}</mat-label>
                  <textarea
                    matInput
                    formControlName="reason"
                    rows="3"
                    [placeholder]="
                      'schedule.reasonPlaceholder'
                        | hhTranslate: 'Lý do đến khám...'
                    "
                  ></textarea>
                </mat-form-field>
              </div>
            </hh-form-section>
          </hh-form-layout>
        </div>
        <div hhCreateDialogFooter>
          <button mat-button type="button" mat-dialog-close [disabled]="saving">
            {{ "common.cancel" | hhTranslate: "Hủy" }}
          </button>
          <button
            mat-raised-button
            color="primary"
            type="submit"
            [disabled]="form.invalid || saving"
          >
            <mat-icon>calendar_today</mat-icon>
            @if (!saving) {
              <span>{{ "schedule.save" | hhTranslate: "Đặt lịch" }}</span>
            }
            @if (saving) {
              <mat-spinner diameter="20"></mat-spinner>
            }
          </button>
        </div>
      </hh-create-dialog-shell>
    </form>
  `,
  styles: [
    `
      .patient-info {
        display: flex;
        align-items: center;
        gap: 8px;
        margin-bottom: 16px;
        padding: 8px 12px;
        background: var(--pastel-orange, #fdf0e2);
        border-radius: 8px;
        color: var(--pastel-orange-text, #b6581c);
        font-weight: 500;
      }
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
    `,
  ],
})
export class ScheduleDialogComponent implements OnDestroy {
  private destroy$ = new Subject<void>();
  private readonly i18n = inject(HisHopeI18nService);

  form: FormGroup;
  saving = false;
  timeSlots = TIME_SLOTS;

  constructor(
    private fb: FormBuilder,
    private dialogRef: MatDialogRef<ScheduleDialogComponent>,
    private appointmentService: AppointmentService,
    private authService: AuthService,
    private snackBar: MatSnackBar,
    private cdr: ChangeDetectorRef,
    @Inject(MAT_DIALOG_DATA) public data: ScheduleData,
  ) {
    const tomorrow = new Date();
    tomorrow.setDate(tomorrow.getDate() + 1);

    this.form = this.fb.group({
      scheduledDate: [tomorrow, Validators.required],
      startTime: ["", Validators.required],
      typeCode: ["consultation", Validators.required],
      reason: [""],
      location: [""],
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
        const providerId = user?.id ?? "usr-002";
        const date = this.form.value.scheduledDate;
        const dateStr =
          date instanceof Date ? date.toISOString().slice(0, 10) : date;

        this.appointmentService
          .schedule({
            patientId: this.data.patientId,
            providerId,
            scheduledDate: dateStr,
            startTime: this.form.value.startTime,
            durationMinutes: 30,
            typeCode: this.form.value.typeCode,
            reason: this.form.value.reason,
            location: this.form.value.location,
          })
          .pipe(takeUntil(this.destroy$))
          .subscribe({
            next: () => {
              this.snackBar.open(
                this.i18n.t(
                  "schedule.saveSuccess",
                  "Đã đặt lịch hẹn thành công",
                ),
                this.i18n.t("common.close", "Đóng"),
                { duration: 3000 },
              );
              this.dialogRef.close(true);
            },
            error: () => {
              this.saving = false;
              this.snackBar.open(
                this.i18n.t("schedule.saveFailed", "Không thể đặt lịch hẹn"),
                this.i18n.t("common.close", "Đóng"),
                { duration: 5000 },
              );
              this.cdr.markForCheck();
            },
          });
      });
  }
}
