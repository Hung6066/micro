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
import { MatSnackBar, MatSnackBarModule } from "@angular/material/snack-bar";
import { MatIconModule } from "@angular/material/icon";
import { MatButtonModule } from "@angular/material/button";
import { Subject, takeUntil } from "rxjs";
import { PharmacyService } from "@core/services/pharmacy.service";
import { Medication } from "@core/models/medication.model";
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
} from "@his-hope/frontend-foundation/ui";

@Component({
  selector: "app-medication-detail",
  standalone: true,
  imports: [
    CommonModule,
    RouterModule,
    MatIconModule,
    MatButtonModule,
    MatSnackBarModule,
    HisHopePageLayoutComponent,
    HisHopePageHeaderComponent,
    HisHopePageSectionComponent,
    HisHopeStateComponent,
    HisHopeMetaItemComponent,
    HisHopeTranslatePipe,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <hh-page-layout>
      <hh-page-header
        hhPageHeader
        [title]="
          medication?.name ||
          ('medications.detail' | hhTranslate: 'Thông tin thuốc')
        "
        [subtitle]="
          medication
            ? ('medications.detailSubtitle'
              | hhTranslate
                : 'Mã thuốc: {{id}} | {{generic}}'
                : {
                    id: (medication.id | slice: 0 : 8),
                    generic: medication.genericName,
                  })
            : ''
        "
      >
        <button mat-stroked-button (click)="goBack()">
          <mat-icon>arrow_back</mat-icon>
          {{ "common.back" | hhTranslate: "Quay lại" }}
        </button>
        @if (medication) {
          @if (medication.isActive) {
            <button
              mat-raised-button
              color="accent"
              [routerLink]="['/pharmacy/medications', medication.id, 'edit']"
              [attr.aria-label]="
                'medications.editLabel' | hhTranslate: 'Chỉnh sửa thuốc'
              "
            >
              <mat-icon>edit</mat-icon>
              {{ "common.edit" | hhTranslate: "Chỉnh sửa" }}
            </button>
          }
          <button
            mat-stroked-button
            [color]="medication.isActive ? 'warn' : 'primary'"
            (click)="toggleActive()"
            [attr.aria-label]="
              medication.isActive
                ? ('medications.deactivateLabel' | hhTranslate: 'Ngừng thuốc')
                : ('medications.reactivateLabel'
                  | hhTranslate: 'Kích hoạt thuốc')
            "
          >
            <mat-icon>{{
              medication.isActive ? "block" : "check_circle"
            }}</mat-icon>
            {{
              medication.isActive
                ? ("medications.deactivate" | hhTranslate: "Ngừng sử dụng")
                : ("medications.reactivate" | hhTranslate: "Kích hoạt")
            }}
          </button>
        }
      </hh-page-header>

      @if (loading) {
        <hh-state kind="loading" [message]="'common.loading' | hhTranslate" />
      } @else if (loadError) {
        <hh-state kind="error" [message]="errorMessage">
          <button mat-stroked-button (click)="loadMedication()">
            {{ "common.retry" | hhTranslate }}
          </button>
        </hh-state>
      } @else if (medication) {
        <hh-page-section
          [title]="'medications.section.info' | hhTranslate: 'Thông tin thuốc'"
        >
          <div class="meta-grid">
            <hh-meta-item
              [label]="'medications.field.name' | hhTranslate: 'Tên thuốc'"
              [value]="medication.name"
              icon="medication"
            />
            <hh-meta-item
              [label]="'medications.field.generic' | hhTranslate: 'Hoạt chất'"
              [value]="medication.genericName"
              icon="science"
            />
            <hh-meta-item
              [label]="
                'medications.field.brand' | hhTranslate: 'Tên thương mại'
              "
              [value]="medication.brandName || '-'"
              icon="local_pharmacy"
            />
            <hh-meta-item
              [label]="'medications.field.strength' | hhTranslate: 'Hàm lượng'"
              [value]="medication.strength"
              icon="scale"
            />
            <hh-meta-item
              [label]="
                'medications.field.dosageForm' | hhTranslate: 'Dạng bào chế'
              "
              [value]="medication.dosageForm"
              icon="category"
            />
            <hh-meta-item
              [label]="'medications.field.route' | hhTranslate: 'Đường dùng'"
              [value]="medication.route"
              icon="route"
            />
            <hh-meta-item
              [label]="'medications.field.prescription' | hhTranslate: 'Kê đơn'"
              [value]="
                medication.requiresPrescription
                  ? ('medications.rxRequired' | hhTranslate: 'Cần kê đơn')
                  : ('medications.rxNotRequired'
                    | hhTranslate: 'Không cần kê đơn')
              "
              icon="description"
            />
          </div>
        </hh-page-section>

        <hh-page-section
          [title]="'medications.section.timeline' | hhTranslate: 'Thời gian'"
        >
          <div class="meta-grid">
            <hh-meta-item
              [label]="'medications.field.createdAt' | hhTranslate: 'Ngày tạo'"
              [value]="(medication.createdAt | date: 'medium') || '-'"
              icon="event"
            />
            @if (medication.updatedAt) {
              <hh-meta-item
                [label]="
                  'medications.field.updatedAt'
                    | hhTranslate: 'Cập nhật lần cuối'
                "
                [value]="(medication.updatedAt | date: 'medium') || '-'"
                icon="update"
              />
            }
          </div>
        </hh-page-section>
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
    `,
  ],
})
export class MedicationDetailComponent implements OnInit, OnDestroy {
  private destroy$ = new Subject<void>();
  private readonly i18n = inject(HisHopeI18nService);

  medication?: Medication;
  loadError = false;
  loading = true;
  errorMessage = "";
  private medicationId = "";

  constructor(
    private route: ActivatedRoute,
    private pharmacyService: PharmacyService,
    private snackBar: MatSnackBar,
    private router: Router,
    private cdr: ChangeDetectorRef,
  ) {}

  ngOnInit(): void {
    this.medicationId = this.route.snapshot.params["id"];
    this.loadMedication();
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  goBack(): void {
    this.router.navigate(["/pharmacy/medications"]);
  }

  loadMedication(): void {
    this.loadError = false;
    this.loading = true;
    this.pharmacyService
      .getMedication(this.medicationId)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (m) => {
          this.medication = m;
          this.loading = false;
          this.cdr.markForCheck();
        },
        error: () => {
          this.loadError = true;
          this.loading = false;
          this.errorMessage = this.i18n.t(
            "medications.loadFailed",
            "Không thể tải thông tin thuốc. Vui lòng thử lại sau.",
          );
          this.cdr.markForCheck();
        },
      });
  }

  toggleActive(): void {
    if (!this.medication) return;
    const activate = !this.medication.isActive;

    this.pharmacyService
      .deactivateMedication(this.medication.id)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: () => {
          this.medication!.isActive = !this.medication!.isActive;
          this.snackBar.open(
            this.i18n.t(
              activate
                ? "medications.activatedSuccess"
                : "medications.deactivatedSuccess",
              activate
                ? "Đã kích hoạt thuốc thành công"
                : "Đã ngừng thuốc thành công",
            ),
            this.i18n.t("common.close", "Đóng"),
            { duration: 3000 },
          );
          this.cdr.markForCheck();
        },
        error: () => {
          this.snackBar.open(
            this.i18n.t(
              activate
                ? "medications.activateFailed"
                : "medications.deactivateFailed",
              activate ? "Không thể kích hoạt thuốc" : "Không thể ngừng thuốc",
            ),
            this.i18n.t("common.close", "Đóng"),
            { duration: 5000 },
          );
          this.cdr.markForCheck();
        },
      });
  }
}
