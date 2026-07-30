import { Component, OnInit, OnDestroy, ChangeDetectionStrategy, ChangeDetectorRef } from '@angular/core';
import { FormBuilder, FormGroup, Validators, ReactiveFormsModule } from '@angular/forms';
import { Subject, takeUntil } from 'rxjs';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { MatExpansionModule } from '@angular/material/expansion';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { CommonModule } from '@angular/common';
import { AdminService } from '@core/services/admin.service';
import { Setting } from '@core/models/admin.model';
import { LoadingSpinnerComponent } from '@shared/components/loading-spinner/loading-spinner.component';

interface CategoryConfig {
  key: string;
  label: string;
  icon: string;
}

const CATEGORIES: CategoryConfig[] = [
  { key: 'hospital', label: 'Thông tin bệnh viện', icon: 'local_hospital' },
  { key: 'system', label: 'Hệ thống', icon: 'settings_applications' },
  { key: 'clinical', label: 'Lâm sàng', icon: 'medical_services' },
  { key: 'billing', label: 'Thanh toán', icon: 'receipt' },
  { key: 'appointment', label: 'Lịch hẹn', icon: 'calendar_today' },
  { key: 'lab', label: 'Xét nghiệm', icon: 'biotech' },
  { key: 'pharmacy', label: 'Dược', icon: 'medication' },
];

@Component({
  selector: 'app-admin-settings',
  standalone: true,
  imports: [
    CommonModule, ReactiveFormsModule,
    MatSnackBarModule, MatButtonModule, MatIconModule, MatFormFieldModule,
    MatInputModule, MatSelectModule, MatSlideToggleModule, MatExpansionModule,
    MatProgressSpinnerModule, MatProgressBarModule,
    LoadingSpinnerComponent,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './settings.component.html',
  styleUrls: ['./settings.component.scss'],
})
export class SettingsComponent implements OnInit, OnDestroy {
  private destroy$ = new Subject<void>();

  settings: Setting[] = [];
  settingValues: Record<string, any> = {};
  originalValues: Record<string, any> = {};
  loading = true;
  saving = false;
  hasChanges = false;

  categories = CATEGORIES;

  constructor(
    private adminService: AdminService,
    private snackBar: MatSnackBar,
    private cdr: ChangeDetectorRef,
  ) {}

  ngOnInit(): void {
    this.loadSettings();
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  loadSettings(): void {
    this.loading = true;
    this.cdr.markForCheck();

    this.adminService.getSettings()
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (settings) => {
          this.settings = settings;
          this.settingValues = {};
          this.originalValues = {};
          for (const s of settings) {
            this.settingValues[s.key] = s.value;
            this.originalValues[s.key] = s.value;
          }
          this.loading = false;
          this.hasChanges = false;
          this.cdr.markForCheck();
        },
        error: () => {
          this.loading = false;
          this.snackBar.open('Không thể tải cài đặt hệ thống', 'Đóng', { duration: 5000 });
          this.cdr.markForCheck();
        },
      });
  }

  getSettingsByCategory(categoryKey: string): Setting[] {
    return this.settings.filter((s) => s.category === categoryKey);
  }

  onSettingChange(key: string, event: Event, type: string): void {
    const input = event.target as HTMLInputElement;
    this.settingValues[key] = type === 'number' ? Number(input.value) : input.value;
    this.detectChanges();
  }

  onSettingSelect(key: string, event: any): void {
    this.settingValues[key] = event.value;
    this.detectChanges();
  }

  onSettingToggle(key: string, event: any): void {
    this.settingValues[key] = event.checked;
    this.detectChanges();
  }

  private detectChanges(): void {
    this.hasChanges = this.settings.some((s) => this.settingValues[s.key] !== this.originalValues[s.key]);
    this.cdr.markForCheck();
  }

  saveAll(): void {
    if (this.saving || !this.hasChanges) return;
    this.saving = true;
    this.cdr.markForCheck();

    const changedSettings = this.settings
      .filter((s) => this.settingValues[s.key] !== this.originalValues[s.key])
      .map((s) => ({ key: s.key, value: this.settingValues[s.key] }));

    this.adminService.bulkUpdateSettings(changedSettings)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: () => {
          this.originalValues = { ...this.settingValues };
          this.hasChanges = false;
          this.saving = false;
          this.snackBar.open('Đã lưu cài đặt thành công', 'Đóng', { duration: 3000 });
          this.cdr.markForCheck();
        },
        error: () => {
          this.saving = false;
          this.snackBar.open('Không thể lưu cài đặt', 'Đóng', { duration: 5000 });
          this.cdr.markForCheck();
        },
      });
  }
}
