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
];

@Component({
  selector: 'app-admin-settings',
  standalone: false,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="settings-page">
      <div class="page-header">
        <div>
          <h1>Cài đặt hệ thống</h1>
          <p class="subtitle">Cấu hình các tham số và giá trị mặc định toàn hệ thống</p>
        </div>
        <button mat-raised-button color="primary" (click)="saveAll()" [disabled]="saving">
          <mat-icon>save</mat-icon>
          <span *ngIf="!saving">Lưu tất cả</span>
          <mat-spinner *ngIf="saving" diameter="20"></mat-spinner>
        </button>
      </div>

      <app-loading-spinner [loading]="loading" message="Đang tải cài đặt..."></app-loading-spinner>

      <div class="settings-content" *ngIf="!loading">
        <mat-accordion class="settings-accordion" [multi]="true">
          <mat-expansion-panel *ngFor="let cat of categories" expanded>
            <mat-expansion-panel-header>
              <mat-panel-title>
                <mat-icon>{{ cat.icon }}</mat-icon>
                {{ cat.label }}
              </mat-panel-title>
            </mat-expansion-panel-header>

            <div class="settings-grid">
              <ng-container *ngFor="let setting of getSettingsByCategory(cat.key)">
                <div class="setting-item">
                  <label class="setting-label">{{ setting.label }}</label>

                  <!-- Text input -->
                  <mat-form-field appearance="outline" *ngIf="setting.type === 'text'" subscriptSizing="dynamic">
                    <input matInput [value]="settingValues[setting.key] || ''"
                           (input)="onSettingChange(setting.key, $event, setting.type)">
                  </mat-form-field>

                  <!-- Number input -->
                  <mat-form-field appearance="outline" *ngIf="setting.type === 'number'" subscriptSizing="dynamic">
                    <input matInput type="number" [value]="settingValues[setting.key] ?? 0"
                           (input)="onSettingChange(setting.key, $event, setting.type)">
                  </mat-form-field>

                  <!-- Select -->
                  <mat-form-field appearance="outline" *ngIf="setting.type === 'select'" subscriptSizing="dynamic">
                    <mat-select [value]="settingValues[setting.key]"
                                (selectionChange)="onSettingSelect(setting.key, $event)">
                      <mat-option *ngFor="let opt of setting.options" [value]="opt.value">
                        {{ opt.label }}
                      </mat-option>
                    </mat-select>
                  </mat-form-field>

                  <!-- Boolean toggle -->
                  <div class="toggle-wrapper" *ngIf="setting.type === 'boolean'">
                    <mat-slide-toggle [checked]="settingValues[setting.key] === true"
                                      (change)="onSettingToggle(setting.key, $event)">
                      {{ settingValues[setting.key] ? 'Đã bật' : 'Đã tắt' }}
                    </mat-slide-toggle>
                  </div>
                </div>
              </ng-container>
            </div>
          </mat-expansion-panel>
        </mat-accordion>

        <div class="save-bar" *ngIf="hasChanges">
          <span class="changes-hint">Có thay đổi chưa được lưu</span>
          <button mat-raised-button color="primary" (click)="saveAll()" [disabled]="saving">
            <mat-icon>save</mat-icon>
            Lưu tất cả
          </button>
        </div>
      </div>
    </div>
  `,
  styles: [`
    .settings-page { padding: 24px; max-width: 900px; margin: 0 auto; }
    .page-header { display: flex; justify-content: space-between; align-items: flex-start; margin-bottom: 24px; }
    .page-header h1 { margin: 0; font-size: 24px; font-weight: 600; color: #1A1A1A; }
    .subtitle { margin: 4px 0 0; color: #787774; font-size: 14px; }

    .settings-content { display: flex; flex-direction: column; gap: 0; }
    .settings-accordion { background: transparent; }

    .settings-grid { display: grid; grid-template-columns: repeat(auto-fill, minmax(320px, 1fr)); gap: 20px; padding: 8px 0; }
    .setting-item { display: flex; flex-direction: column; gap: 6px; }
    .setting-label { font-size: 13px; font-weight: 500; color: #1A1A1A; }
    .toggle-wrapper { display: flex; align-items: center; padding: 8px 0; }

    ::ng-deep .mat-expansion-panel-header-title { display: flex; align-items: center; gap: 8px; }
    ::ng-deep .mat-expansion-panel-header-title mat-icon { color: #2F6B4A; }

    .save-bar { display: flex; align-items: center; justify-content: flex-end; gap: 16px; margin-top: 24px; padding: 16px 24px; background: #FFFFFF; border: 1px solid #EAEAEA; border-radius: 8px; }
    .changes-hint { color: #e65100; font-size: 13px; font-weight: 500; }
  `],
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
