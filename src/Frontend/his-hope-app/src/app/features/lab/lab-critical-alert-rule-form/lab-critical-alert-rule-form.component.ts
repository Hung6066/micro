import { ChangeDetectionStrategy, Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { AbstractControl, FormBuilder, ReactiveFormsModule, ValidationErrors, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { CriticalAlertRuleRequest } from '@core/models/critical-alert-rule.model';
import { LabCriticalAlertService } from '@core/services/lab-critical-alert.service';

const thresholdValidator = (control: AbstractControl): ValidationErrors | null => {
  const low = control.get('lowCriticalValue')?.value;
  const high = control.get('highCriticalValue')?.value;

  if (low === null && high === null) {
    return { thresholdRequired: true };
  }

  return null;
};

@Component({
  selector: 'app-lab-critical-alert-rule-form',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, MatButtonModule, MatCardModule, MatCheckboxModule, MatFormFieldModule, MatInputModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <mat-card class="rule-form-card">
      <mat-card-header>
        <mat-card-title>Quy tắc cảnh báo nghiêm trọng</mat-card-title>
      </mat-card-header>
      <mat-card-content>
        <form [formGroup]="form" class="rule-form">
          <mat-form-field appearance="outline">
            <mat-label>Mã xét nghiệm</mat-label>
            <input matInput formControlName="testCode">
          </mat-form-field>

          <mat-form-field appearance="outline">
            <mat-label>Tên xét nghiệm</mat-label>
            <input matInput formControlName="testName">
          </mat-form-field>

          <mat-form-field appearance="outline">
            <mat-label>Đơn vị</mat-label>
            <input matInput formControlName="unit">
          </mat-form-field>

          <mat-form-field appearance="outline">
            <mat-label>Ngưỡng thấp</mat-label>
            <input matInput type="number" formControlName="lowCriticalValue">
          </mat-form-field>

          <mat-form-field appearance="outline">
            <mat-label>Ngưỡng cao</mat-label>
            <input matInput type="number" formControlName="highCriticalValue">
          </mat-form-field>

          <mat-checkbox formControlName="isActive">Kích hoạt</mat-checkbox>
        </form>
      </mat-card-content>
      <mat-card-actions align="end">
        <button mat-raised-button color="primary" type="button" [disabled]="form.invalid" (click)="save()">Lưu quy tắc</button>
      </mat-card-actions>
    </mat-card>
  `,
  styles: [`
    .rule-form-card { margin: 24px; border: 1px solid #EAEAEA; }
    .rule-form { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 16px; }
    mat-checkbox { grid-column: 1 / -1; }
  `],
})
export class LabCriticalAlertRuleFormComponent {
  readonly form = this.fb.group({
    testCode: ['', Validators.required],
    testName: ['', Validators.required],
    unit: [''],
    lowCriticalValue: [null as number | null],
    highCriticalValue: [null as number | null],
    isActive: [true],
  }, { validators: [thresholdValidator] });

  constructor(
    private readonly fb: FormBuilder,
    private readonly alertService: LabCriticalAlertService,
  ) {}

  save(): void {
    if (this.form.invalid) {
      return;
    }

    this.alertService.saveCriticalAlertRule(this.form.getRawValue() as CriticalAlertRuleRequest).subscribe();
  }
}
