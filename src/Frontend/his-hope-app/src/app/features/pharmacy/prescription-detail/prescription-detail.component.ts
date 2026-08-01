import { Component, OnInit, OnDestroy, ChangeDetectionStrategy, ChangeDetectorRef, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { Subject, takeUntil } from 'rxjs';
import { PharmacyService } from '@core/services/pharmacy.service';
import { Prescription } from '@core/models/prescription.model';
import {
  HisHopeI18nService,
  HisHopeMetaItemComponent,
  HisHopePageHeaderComponent,
  HisHopePageLayoutComponent,
  HisHopePageSectionComponent,
  HisHopeStateComponent,
  HisHopeStatusBadgeComponent,
  HisHopeTranslatePipe,
} from '@his-hope/frontend-foundation';

@Component({
    selector: 'app-prescription-detail',
    standalone: true,
    imports: [
        CommonModule, RouterModule,
        MatIconModule, MatButtonModule, MatSnackBarModule,
        HisHopePageLayoutComponent, HisHopePageHeaderComponent, HisHopePageSectionComponent,
        HisHopeStateComponent, HisHopeStatusBadgeComponent, HisHopeMetaItemComponent,
        HisHopeTranslatePipe,
    ],
    changeDetection: ChangeDetectionStrategy.OnPush,
    template: `
    <hh-page-layout>
      <hh-page-header hhPageHeader
                      [title]="'prescriptions.detail' | hhTranslate:'Chi tiết đơn thuốc'"
                      [subtitle]="prescription ? ('prescriptions.detailSubtitle' | hhTranslate:'Đơn thuốc #{{id}}':{id: (prescription.id | slice:0:8)}) : ''">
        <button mat-stroked-button (click)="goBack()">
          <mat-icon>arrow_back</mat-icon> {{ 'common.back' | hhTranslate:'Quay lại' }}
        </button>
        @if (prescription) {
        <hh-status-badge [status]="prescription.statusCode" [label]="prescription.statusName" />
        @if (prescription.statusCode === 'active') {
        <button mat-raised-button color="primary"
                (click)="fillPrescription()"
                [attr.aria-label]="'prescriptions.fillLabel' | hhTranslate:'Cấp phát thuốc'">
          <mat-icon>medication</mat-icon> {{ 'prescriptions.fill' | hhTranslate:'Cấp phát' }}
        </button>
        }
        @if (prescription.statusCode === 'active' || prescription.statusCode === 'partially_filled') {
        <button mat-stroked-button color="warn"
                (click)="cancelPrescription()"
                [attr.aria-label]="'prescriptions.cancelLabel' | hhTranslate:'Hủy đơn thuốc'">
          <mat-icon>cancel</mat-icon> {{ 'prescriptions.cancel' | hhTranslate:'Hủy đơn' }}
        </button>
        }
        }
      </hh-page-header>

      @if (loading) {
      <hh-state kind="loading" [message]="'common.loading' | hhTranslate" />
      } @else if (loadError) {
      <hh-state kind="error" [message]="errorMessage">
        <button mat-stroked-button (click)="loadPrescription()">{{ 'common.retry' | hhTranslate }}</button>
      </hh-state>
      } @else if (prescription) {
      <hh-page-section [title]="'prescriptions.section.info' | hhTranslate:'Thông tin đơn thuốc'">
        <div class="meta-grid">
          <hh-meta-item [label]="'prescriptions.field.patient' | hhTranslate:'Bệnh nhân'" [value]="prescription.patientName || prescription.patientId" icon="person" />
          <hh-meta-item [label]="'prescriptions.field.provider' | hhTranslate:'Bác sĩ'" [value]="prescription.providerName || prescription.providerId" icon="medical_services" />
          <hh-meta-item [label]="'prescriptions.field.medication' | hhTranslate:'Thuốc'" [value]="prescription.medicationName" icon="medication" />
          <hh-meta-item [label]="'prescriptions.field.strength' | hhTranslate:'Hàm lượng'" [value]="prescription.strength" icon="scale" />
          <hh-meta-item [label]="'prescriptions.field.dosageForm' | hhTranslate:'Dạng bào chế'" [value]="prescription.dosageForm" icon="category" />
          <hh-meta-item [label]="'prescriptions.field.instructions' | hhTranslate:'Hướng dẫn sử dụng'" [value]="prescription.dosageInstructions" icon="description" />
          <hh-meta-item [label]="'prescriptions.field.route' | hhTranslate:'Đường dùng'" [value]="prescription.route" icon="route" />
          <hh-meta-item [label]="'prescriptions.field.quantity' | hhTranslate:'Số lượng'" [value]="prescription.quantity + ''" icon="inventory_2" />
          <hh-meta-item [label]="'prescriptions.field.refills' | hhTranslate:'Số lần tái kê'" [value]="prescription.refills + ''" icon="replay" />
        </div>
      </hh-page-section>

      <hh-page-section [title]="'prescriptions.section.timeline' | hhTranslate:'Thời gian'">
        <div class="meta-grid">
          <hh-meta-item [label]="'prescriptions.field.prescribedAt' | hhTranslate:'Ngày kê đơn'" [value]="(prescription.prescribedAt | date:'medium') || '-'" icon="event" />
          @if (prescription.filledAt) {
          <hh-meta-item [label]="'prescriptions.field.filledAt' | hhTranslate:'Ngày cấp phát'" [value]="(prescription.filledAt | date:'medium') || '-'" icon="check_circle" />
          }
          <hh-meta-item [label]="'prescriptions.field.createdAt' | hhTranslate:'Ngày tạo'" [value]="(prescription.createdAt | date:'medium') || '-'" icon="event_note" />
          @if (prescription.updatedAt) {
          <hh-meta-item [label]="'prescriptions.field.updatedAt' | hhTranslate:'Cập nhật'" [value]="(prescription.updatedAt | date:'medium') || '-'" icon="update" />
          }
        </div>
      </hh-page-section>
      }
    </hh-page-layout>
  `,
    styles: [`
    .meta-grid {
      display: grid;
      grid-template-columns: repeat(auto-fill, minmax(280px, 1fr));
      gap: 16px;
    }
  `],
})
export class PrescriptionDetailComponent implements OnInit, OnDestroy {
  private destroy$ = new Subject<void>();
  private readonly i18n = inject(HisHopeI18nService);

  prescription?: Prescription;
  loadError = false;
  loading = true;
  errorMessage = '';
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

  goBack(): void {
    this.router.navigate(['/pharmacy/prescriptions']);
  }

  loadPrescription(): void {
    this.loadError = false;
    this.loading = true;
    this.pharmacyService.getPrescription(this.prescriptionId)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (p) => {
          this.prescription = p;
          this.loading = false;
          this.cdr.markForCheck();
        },
        error: () => {
          this.loadError = true;
          this.loading = false;
          this.errorMessage = this.i18n.t('prescriptions.loadFailed', 'Không thể tải thông tin đơn thuốc. Vui lòng thử lại sau.');
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
          this.snackBar.open(this.i18n.t('prescriptions.fillSuccess', 'Đã cấp phát thuốc thành công'), this.i18n.t('common.close', 'Đóng'), { duration: 3000 });
          this.loadPrescription();
        },
        error: () => {
          this.snackBar.open(this.i18n.t('prescriptions.fillFailed', 'Không thể cấp phát thuốc'), this.i18n.t('common.close', 'Đóng'), { duration: 5000 });
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
          this.snackBar.open(this.i18n.t('prescriptions.cancelSuccess', 'Đã hủy đơn thuốc'), this.i18n.t('common.close', 'Đóng'), { duration: 3000 });
          this.loadPrescription();
        },
        error: () => {
          this.snackBar.open(this.i18n.t('prescriptions.cancelFailed', 'Không thể hủy đơn thuốc'), this.i18n.t('common.close', 'Đóng'), { duration: 5000 });
          this.cdr.markForCheck();
        },
      });
  }
}
