import { Component, Inject, OnDestroy, OnInit, ChangeDetectionStrategy, ChangeDetectorRef } from '@angular/core';
import { FormBuilder, FormGroup, Validators, ReactiveFormsModule } from '@angular/forms';
import { MatDialogRef, MAT_DIALOG_DATA, MatDialogModule } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatAutocompleteModule } from '@angular/material/autocomplete';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { CommonModule } from '@angular/common';
import { Subject, of, takeUntil, debounceTime, distinctUntilChanged, switchMap } from 'rxjs';
import { PharmacyService } from '@core/services/pharmacy.service';
import { AuthService } from '@core/services/auth.service';
import { Medication } from '@core/models/medication.model';

export interface PrescribeData {
  patientId: string;
  patientName: string;
}

@Component({
    selector: 'app-prescribe-dialog',
    imports: [
        CommonModule,
        ReactiveFormsModule,
        MatDialogModule,
        MatButtonModule,
        MatFormFieldModule,
        MatInputModule,
        MatSelectModule,
        MatAutocompleteModule,
        MatIconModule,
        MatProgressSpinnerModule,
        MatSnackBarModule,
    ],
    changeDetection: ChangeDetectionStrategy.OnPush,
    template: `
    <h2 mat-dialog-title>Kê đơn thuốc</h2>
    <mat-dialog-content>
      @if (data.patientName) {
      <div class="patient-info">
        <mat-icon>person</mat-icon>
        <span>{{ data.patientName }}</span>
      </div>
      }
      <form [formGroup]="form" class="dialog-form">
        <mat-form-field appearance="outline">
          <mat-label>Tìm thuốc</mat-label>
          <input matInput formControlName="medicationSearch" [matAutocomplete]="auto" placeholder="Gõ tên thuốc...">
          <mat-autocomplete #auto="matAutocomplete" [displayWith]="displayMedication" (optionSelected)="onMedicationSelected($event)">
            @for (med of filteredMedications; track med) {
            <mat-option [value]="med">
              {{ med.name }} <small>({{ med.strength }})</small>
            </mat-option>
            }
          </mat-autocomplete>
          @if (form.get('medicationSearch')?.value && !selectedMedication) {
          <mat-icon matSuffix>search</mat-icon>
          }
          @if (selectedMedication) {
          <mat-icon matSuffix color="primary">check_circle</mat-icon>
          }
        </mat-form-field>

        @if (selectedMedication) {
        <div class="selected-med">
          <p><strong>{{ selectedMedication.name }}</strong></p>
          <p>Dạng: {{ selectedMedication.dosageForm }} | Hàm lượng: {{ selectedMedication.strength }}</p>
        </div>
        }

        <mat-form-field appearance="outline">
          <mat-label>Liều dùng</mat-label>
          <input matInput formControlName="dosageInstructions" placeholder="VD: Uống 1 viên x 2 lần/ngày sau ăn" required>
          @if (form.get('dosageInstructions')?.hasError('required')) {
          <mat-error>Vui lòng nhập liều dùng</mat-error>
          }
        </mat-form-field>

        <mat-form-field appearance="outline">
          <mat-label>Đường dùng</mat-label>
          <mat-select formControlName="route" required>
            <mat-option value="oral">Uống</mat-option>
            <mat-option value="intravenous">Tiêm tĩnh mạch</mat-option>
            <mat-option value="intramuscular">Tiêm bắp</mat-option>
            <mat-option value="subcutaneous">Tiêm dưới da</mat-option>
            <mat-option value="inhalation">Hít</mat-option>
            <mat-option value="topical">Bôi ngoài da</mat-option>
            <mat-option value="rectal">Đặt hậu môn</mat-option>
          </mat-select>
        </mat-form-field>

        <mat-form-field appearance="outline">
          <mat-label>Số lượng</mat-label>
          <input matInput type="number" formControlName="quantity" min="1" required>
          @if (form.get('quantity')?.hasError('required')) {
          <mat-error>Vui lòng nhập số lượng</mat-error>
          }
        </mat-form-field>

        <mat-form-field appearance="outline">
          <mat-label>Số lần tái kê</mat-label>
          <input matInput type="number" formControlName="refills" min="0" value="0">
        </mat-form-field>
      </form>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button mat-dialog-close [disabled]="saving">Hủy</button>
      <button mat-raised-button color="primary" (click)="save()" [disabled]="form.invalid || saving || !selectedMedication">
        <mat-icon>medication</mat-icon>
        @if (!saving) {
        <span>Kê đơn</span>
        }
        @if (saving) {
        <mat-spinner diameter="20"></mat-spinner>
        }
      </button>
    </mat-dialog-actions>
  `,
    styles: [`
    .patient-info { display: flex; align-items: center; gap: 8px; margin-bottom: 16px; padding: 8px 12px; background: var(--pastel-green, #EDF3EC); border-radius: 8px; color: var(--pastel-green-text, #346538); font-weight: 500; }
    .dialog-form { display: flex; flex-direction: column; gap: 16px; min-width: 380px; }
    .selected-med { background: var(--surface-white, #FFFFFF); padding: 12px; border-radius: 8px; border-left: 4px solid var(--color-primary, #2F6B4A); }
    .selected-med p { margin: 4px 0; }
  `]
})
export class PrescribeDialogComponent implements OnInit, OnDestroy {
  private destroy$ = new Subject<void>();

  form: FormGroup;
  saving = false;
  filteredMedications: Medication[] = [];
  selectedMedication: Medication | null = null;

  constructor(
    private fb: FormBuilder,
    private dialogRef: MatDialogRef<PrescribeDialogComponent>,
    private pharmacyService: PharmacyService,
    private authService: AuthService,
    private snackBar: MatSnackBar,
    private cdr: ChangeDetectorRef,
    @Inject(MAT_DIALOG_DATA) public data: PrescribeData,
  ) {
    this.form = this.fb.group({
      medicationSearch: [''],
      dosageInstructions: ['', Validators.required],
      route: ['oral', Validators.required],
      quantity: [30, [Validators.required, Validators.min(1)]],
      refills: [0, [Validators.min(0)]],
    });
  }

  ngOnInit(): void {
    this.form.get('medicationSearch')?.valueChanges
      .pipe(
        debounceTime(300),
        distinctUntilChanged(),
        takeUntil(this.destroy$),
      )
      .subscribe((val) => {
        if (typeof val !== 'string') return;
        this.selectedMedication = null;
        if (val.length < 1) {
          this.filteredMedications = [];
          this.cdr.markForCheck();
          return;
        }
        this.pharmacyService.searchMedications({ searchTerm: val, pageSize: 20 })
          .pipe(takeUntil(this.destroy$))
          .subscribe((res) => {
            this.filteredMedications = res.items;
            this.cdr.markForCheck();
          });
      });
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  displayMedication(med: Medication): string {
    return med ? `${med.name} (${med.strength})` : '';
  }

  onMedicationSelected(event: any): void {
    this.selectedMedication = event.option.value;
    this.form.patchValue({ medicationSearch: this.selectedMedication?.name });
  }

  save(): void {
    if (this.form.invalid || this.saving || !this.selectedMedication) return;
    this.saving = true;
    this.cdr.markForCheck();

    this.authService.currentUser$
      .pipe(takeUntil(this.destroy$))
      .subscribe((user) => {
        const providerId = user?.id ?? 'usr-002';

        this.pharmacyService.createPrescription({
          patientId: this.data.patientId,
          providerId,
          medicationId: this.selectedMedication!.id,
          dosageInstructions: this.form.value.dosageInstructions,
          route: this.form.value.route,
          quantity: this.form.value.quantity,
          refills: this.form.value.refills,
        }).pipe(takeUntil(this.destroy$))
          .subscribe({
            next: () => {
              this.snackBar.open('Đã kê đơn thuốc thành công', 'Đóng', { duration: 3000 });
              this.dialogRef.close(true);
            },
            error: () => {
              this.saving = false;
              this.snackBar.open('Không thể kê đơn thuốc', 'Đóng', { duration: 5000 });
              this.cdr.markForCheck();
            },
          });
      });
  }
}
