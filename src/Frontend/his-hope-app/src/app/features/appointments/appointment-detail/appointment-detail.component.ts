import {
  Component,
  OnInit,
  OnDestroy,
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  inject,
} from "@angular/core";
import { CommonModule } from "@angular/common";
import { ActivatedRoute, Router, RouterModule } from "@angular/router";
import { Subject, takeUntil } from "rxjs";
import { MatIconModule } from "@angular/material/icon";
import { MatListModule } from "@angular/material/list";
import { AppointmentService } from "@core/services/appointment.service";
import { Appointment } from "@core/models/appointment.model";
import {
  HisHopeI18nService,
  HisHopeTranslatePipe,
} from "@his-hope/frontend-foundation/i18n";
import {
  HisHopeMetaItemComponent,
  HisHopePageHeaderComponent,
  HisHopePageLayoutComponent,
  HisHopePageSectionComponent,
  HisHopeStateComponent,
  HisHopeStatusBadgeComponent,
} from "@his-hope/frontend-foundation/ui";

@Component({
  selector: "app-appointment-detail",
  standalone: true,
  imports: [
    CommonModule,
    RouterModule,
    MatIconModule,
    MatListModule,
    HisHopePageLayoutComponent,
    HisHopePageHeaderComponent,
    HisHopePageSectionComponent,
    HisHopeStateComponent,
    HisHopeStatusBadgeComponent,
    HisHopeMetaItemComponent,
    HisHopeTranslatePipe,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <hh-page-layout>
      <hh-page-header
        hhPageHeader
        [title]="'appointments.detail' | hhTranslate: 'Chi tiết cuộc hẹn'"
        [subtitle]="
          appointment
            ? ('appointments.detailSubtitle'
              | hhTranslate
                : 'Mã hẹn: {{id}}'
                : { id: (appointment.id | slice: 0 : 8) })
            : ''
        "
      >
        <button mat-stroked-button (click)="goBack()">
          <mat-icon>arrow_back</mat-icon>
          {{ "common.back" | hhTranslate: "Quay lại" }}
        </button>
        @if (appointment) {
          <hh-status-badge
            [status]="appointment.status"
            [label]="appointment.statusName || appointment.status"
          />
        }
      </hh-page-header>

      @if (loading) {
        <hh-state kind="loading" [message]="'common.loading' | hhTranslate" />
      } @else if (error) {
        <hh-state kind="error" [message]="error">
          <button mat-stroked-button (click)="loadAppointment()">
            {{ "common.retry" | hhTranslate }}
          </button>
        </hh-state>
      } @else if (appointment) {
        <hh-page-section
          [title]="'appointments.section.schedule' | hhTranslate: 'Lịch hẹn'"
        >
          <div class="meta-grid">
            <hh-meta-item
              [label]="'appointments.field.date' | hhTranslate: 'Ngày'"
              [value]="(appointment.scheduledDate | date: 'mediumDate') || '-'"
              icon="event"
            />
            <hh-meta-item
              [label]="'appointments.field.time' | hhTranslate: 'Giờ'"
              [value]="appointment.startTime + ' - ' + appointment.endTime"
              icon="schedule"
            />
            <hh-meta-item
              [label]="'appointments.field.type' | hhTranslate: 'Loại'"
              [value]="appointment.typeName || appointment.type"
              icon="category"
            />
            <hh-meta-item
              [label]="'appointments.field.location' | hhTranslate: 'Địa điểm'"
              [value]="appointment.location || 'N/A'"
              icon="place"
            />
          </div>
        </hh-page-section>

        <hh-page-section
          [title]="
            'appointments.section.participants'
              | hhTranslate: 'Thành phần tham gia'
          "
        >
          <div class="meta-grid">
            <hh-meta-item
              [label]="
                'appointments.field.patient' | hhTranslate: 'Mã bệnh nhân'
              "
              [value]="appointment.patientId"
              icon="person"
            />
            <hh-meta-item
              [label]="'appointments.field.provider' | hhTranslate: 'Mã bác sĩ'"
              [value]="appointment.providerId"
              icon="medical_services"
            />
          </div>
        </hh-page-section>

        @if (appointment.reason) {
          <hh-page-section
            [title]="'appointments.section.reason' | hhTranslate: 'Lý do'"
          >
            <p>{{ appointment.reason }}</p>
          </hh-page-section>
        }

        @if (appointment.notes) {
          <hh-page-section
            [title]="'appointments.section.notes' | hhTranslate: 'Ghi chú'"
          >
            <p>{{ appointment.notes }}</p>
          </hh-page-section>
        }

        @if (appointment.cancellationReason) {
          <hh-page-section
            [title]="
              'appointments.section.cancellationReason'
                | hhTranslate: 'Lý do hủy'
            "
          >
            <p>{{ appointment.cancellationReason }}</p>
          </hh-page-section>
        }

        @if (appointment.createdAt) {
          <hh-page-section
            [title]="
              'appointments.section.timeline' | hhTranslate: 'Dòng thời gian'
            "
          >
            <mat-list>
              @if (appointment.createdAt) {
                <mat-list-item>
                  <mat-icon matListItemIcon>add_circle</mat-icon>
                  <div matListItemTitle>
                    {{
                      "appointments.timeline.created" | hhTranslate: "Khởi tạo"
                    }}
                  </div>
                  <div matListItemLine>
                    {{ appointment.createdAt | date: "medium" }}
                  </div>
                </mat-list-item>
              }
              @if (appointment.checkedInAt) {
                <mat-list-item>
                  <mat-icon matListItemIcon>login</mat-icon>
                  <div matListItemTitle>
                    {{
                      "appointments.timeline.checkedIn" | hhTranslate: "Đã đến"
                    }}
                  </div>
                  <div matListItemLine>
                    {{ appointment.checkedInAt | date: "medium" }}
                  </div>
                </mat-list-item>
              }
              @if (appointment.checkedOutAt) {
                <mat-list-item>
                  <mat-icon matListItemIcon>logout</mat-icon>
                  <div matListItemTitle>
                    {{
                      "appointments.timeline.checkedOut"
                        | hhTranslate: "Đã ra về"
                    }}
                  </div>
                  <div matListItemLine>
                    {{ appointment.checkedOutAt | date: "medium" }}
                  </div>
                </mat-list-item>
              }
              @if (appointment.cancelledAt) {
                <mat-list-item>
                  <mat-icon matListItemIcon>cancel</mat-icon>
                  <div matListItemTitle>
                    {{
                      "appointments.timeline.cancelled" | hhTranslate: "Đã hủy"
                    }}
                  </div>
                  <div matListItemLine>
                    {{ appointment.cancelledAt | date: "medium" }}
                  </div>
                </mat-list-item>
              }
            </mat-list>
          </hh-page-section>
        }
      }
    </hh-page-layout>
  `,
  styles: [
    `
      .meta-grid {
        display: grid;
        grid-template-columns: repeat(auto-fill, minmax(280px, 1fr));
        gap: 16px;
      }

      p {
        margin: 4px 0;
        line-height: 1.5;
      }
    `,
  ],
})
export class AppointmentDetailComponent implements OnInit, OnDestroy {
  private destroy$ = new Subject<void>();
  private readonly i18n = inject(HisHopeI18nService);
  appointment?: Appointment;

  loading = true;
  error = "";

  constructor(
    private route: ActivatedRoute,
    private appointmentService: AppointmentService,
    private cdr: ChangeDetectorRef,
    private router: Router,
  ) {}

  ngOnInit(): void {
    this.loadAppointment();
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  loadAppointment(): void {
    const id = this.route.snapshot.paramMap.get("id")!;
    this.loading = true;
    this.error = "";
    this.appointmentService
      .getById(id)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (a) => {
          this.appointment = a;
          this.loading = false;
          this.cdr.markForCheck();
        },
        error: () => {
          this.error = this.i18n.t(
            "appointments.loadFailed",
            "Không thể tải thông tin cuộc hẹn.",
          );
          this.loading = false;
          this.cdr.markForCheck();
        },
      });
  }

  goBack(): void {
    this.router.navigate(["/appointments"]);
  }
}
