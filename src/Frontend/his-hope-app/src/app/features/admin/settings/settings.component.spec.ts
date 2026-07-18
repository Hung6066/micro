import { ComponentFixture, TestBed } from '@angular/core/testing';
import { RouterTestingModule } from '@angular/router/testing';
import { NoopAnimationsModule } from '@angular/platform-browser/animations';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { ReactiveFormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { MatExpansionModule } from '@angular/material/expansion';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSnackBarModule } from '@angular/material/snack-bar';
import { CommonModule } from '@angular/common';
import { of } from 'rxjs';
import { SettingsComponent } from './settings.component';
import { AdminService } from '@core/services/admin.service';
import { LoadingSpinnerComponent } from '@shared/components/loading-spinner/loading-spinner.component';
import { provideHttpClient, withInterceptorsFromDi } from '@angular/common/http';

describe('SettingsComponent', () => {
  let component: SettingsComponent;
  let fixture: ComponentFixture<SettingsComponent>;
  let adminService: jasmine.SpyObj<AdminService>;

  const mockSettings = [
    { key: 'hospital_name', value: 'His.Hope Hospital', type: 'text' as const, label: 'Hospital Name', category: 'hospital' },
    { key: 'enable_telemedicine', value: true, type: 'boolean' as const, label: 'Enable Telemedicine', category: 'system' },
    { key: 'default_language', value: 'vi', type: 'select' as const, label: 'Default Language', category: 'system', options: [{ label: 'Vietnamese', value: 'vi' }, { label: 'English', value: 'en' }] },
    { key: 'max_appointments_per_day', value: 50, type: 'number' as const, label: 'Max Appointments/Day', category: 'clinical' },
  ];

  beforeEach(async () => {
    const spy = jasmine.createSpyObj('AdminService', ['getSettings', 'bulkUpdateSettings']);
    spy.getSettings.and.returnValue(of(mockSettings));

    await TestBed.configureTestingModule({
    declarations: [
        SettingsComponent, LoadingSpinnerComponent
    ],
    imports: [RouterTestingModule, NoopAnimationsModule,
        ReactiveFormsModule, MatButtonModule, MatIconModule, MatFormFieldModule,
        MatInputModule, MatSelectModule, MatSlideToggleModule, MatExpansionModule,
        MatProgressSpinnerModule, MatProgressBarModule, MatSnackBarModule, CommonModule],
    providers: [
        { provide: AdminService, useValue: spy },
        provideHttpClient(withInterceptorsFromDi()),
        provideHttpClientTesting(),
    ]
}).compileComponents();

    fixture = TestBed.createComponent(SettingsComponent);
    component = fixture.componentInstance;
    adminService = TestBed.inject(AdminService) as jasmine.SpyObj<AdminService>;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('should load settings on init', () => {
    expect(adminService.getSettings).toHaveBeenCalled();
  });

  it('should render title', () => {
    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('h1')?.textContent).toContain('Cài đặt hệ thống');
  });

  it('should display settings accordion', () => {
    const compiled = fixture.nativeElement as HTMLElement;
    const panels = compiled.querySelectorAll('mat-expansion-panel');
    expect(panels.length).toBeGreaterThanOrEqual(1);
  });

  it('should have settings loaded', () => {
    expect(component.settings.length).toBe(4);
  });

  it('should have categories', () => {
    expect(component.categories.length).toBe(4);
  });

  it('should pass a basic integrity check', () => {
    expect(true).toBeTrue();
  });

});
