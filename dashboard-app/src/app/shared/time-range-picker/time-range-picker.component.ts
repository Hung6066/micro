import { Component, Output, EventEmitter, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatIconModule } from '@angular/material/icon';
import { provideNativeDateAdapter } from '@angular/material/core';

export interface TimeRange {
  from: Date;
  to: Date;
  label: string;
}

interface PresetOption {
  label: string;
  value: string;
  calc: () => TimeRange;
}

const PRESETS: PresetOption[] = [
  {
    label: '5m',
    value: '5m',
    calc: () => {
      const to = new Date();
      const from = new Date(to.getTime() - 5 * 60 * 1000);
      return { from, to, label: 'Last 5m' };
    },
  },
  {
    label: '15m',
    value: '15m',
    calc: () => {
      const to = new Date();
      const from = new Date(to.getTime() - 15 * 60 * 1000);
      return { from, to, label: 'Last 15m' };
    },
  },
  {
    label: '1h',
    value: '1h',
    calc: () => {
      const to = new Date();
      const from = new Date(to.getTime() - 60 * 60 * 1000);
      return { from, to, label: 'Last 1h' };
    },
  },
  {
    label: '6h',
    value: '6h',
    calc: () => {
      const to = new Date();
      const from = new Date(to.getTime() - 6 * 60 * 60 * 1000);
      return { from, to, label: 'Last 6h' };
    },
  },
  {
    label: '24h',
    value: '24h',
    calc: () => {
      const to = new Date();
      const from = new Date(to.getTime() - 24 * 60 * 60 * 1000);
      return { from, to, label: 'Last 24h' };
    },
  },
  {
    label: 'Custom',
    value: 'custom',
    calc: () => {
      const to = new Date();
      const from = new Date(to.getTime() - 60 * 60 * 1000);
      return { from, to, label: 'Custom' };
    },
  },
];

@Component({
  selector: 'app-time-range-picker',
  standalone: true,
  providers: [provideNativeDateAdapter()],
  imports: [
    CommonModule,
    FormsModule,
    MatButtonModule,
    MatDatepickerModule,
    MatFormFieldModule,
    MatInputModule,
    MatIconModule,
  ],
  template: `
    <div class="time-range-picker">
      <div class="preset-group">
        <button
          *ngFor="let preset of presets"
          mat-stroked-button
          size="small"
          class="preset-btn"
          [class.active]="activePreset === preset.value"
          (click)="selectPreset(preset)">
          {{ preset.label }}
        </button>
      </div>

      <div class="custom-range" *ngIf="showCustom">
        <mat-form-field appearance="outline" subscriptSizing="dynamic">
          <mat-label>From</mat-label>
          <input
            matInput
            [matDatepicker]="fromPicker"
            [(ngModel)]="customFrom"
            (ngModelChange)="onCustomChange()"
          />
          <mat-datepicker-toggle matSuffix [for]="fromPicker"></mat-datepicker-toggle>
          <mat-datepicker #fromPicker></mat-datepicker>
        </mat-form-field>

        <mat-form-field appearance="outline" subscriptSizing="dynamic">
          <mat-label>To</mat-label>
          <input
            matInput
            [matDatepicker]="toPicker"
            [(ngModel)]="customTo"
            (ngModelChange)="onCustomChange()"
          />
          <mat-datepicker-toggle matSuffix [for]="toPicker"></mat-datepicker-toggle>
          <mat-datepicker #toPicker></mat-datepicker>
        </mat-form-field>

        <button
          mat-raised-button
          color="primary"
          size="small"
          (click)="applyCustom()"
          [disabled]="!customFrom || !customTo">
          <mat-icon>check</mat-icon>
          Apply
        </button>
      </div>
    </div>
  `,
  styles: [`
    .time-range-picker {
      display: flex;
      align-items: center;
      gap: 8px;
      flex-wrap: wrap;
    }
    .preset-group {
      display: flex;
      gap: 4px;
      flex-wrap: wrap;
    }
    .preset-btn {
      min-width: 44px;
      font-size: 12px;
      padding: 0 10px;
      line-height: 30px;
      transition: all 150ms ease;
    }
    .preset-btn.active {
      background: var(--color-primary, #2F6B4A);
      color: #fff;
      border-color: var(--color-primary, #2F6B4A);
    }
    .custom-range {
      display: flex;
      align-items: center;
      gap: 8px;
      flex-wrap: wrap;
    }
    .custom-range mat-form-field {
      width: 140px;
    }
  `],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class TimeRangePickerComponent {
  @Output() rangeChange = new EventEmitter<TimeRange>();

  readonly presets = PRESETS;
  activePreset = '1h';
  showCustom = false;

  customFrom: Date | null = null;
  customTo: Date | null = null;

  private currentRange: TimeRange = PRESETS[2].calc(); // default 1h

  selectPreset(preset: PresetOption): void {
    this.activePreset = preset.value;
    this.showCustom = preset.value === 'custom';

    if (!this.showCustom) {
      this.currentRange = preset.calc();
      this.rangeChange.emit(this.currentRange);
    } else {
      // Default custom range to last 24h
      const now = new Date();
      this.customTo = now;
      this.customFrom = new Date(now.getTime() - 24 * 60 * 60 * 1000);
    }
  }

  onCustomChange(): void {
    // Auto-apply when both dates selected
    if (this.customFrom && this.customTo) {
      this.applyCustom();
    }
  }

  applyCustom(): void {
    if (!this.customFrom || !this.customTo) return;
    this.currentRange = {
      from: this.customFrom,
      to: this.customTo,
      label: 'Custom',
    };
    this.rangeChange.emit(this.currentRange);
  }
}
