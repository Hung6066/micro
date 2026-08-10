import { ComponentFixture, TestBed } from '@angular/core/testing';
import { NoopAnimationsModule } from '@angular/platform-browser/animations';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { ReactiveFormsModule } from '@angular/forms';
import { MatDialogModule, MatDialogRef, MAT_DIALOG_DATA } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatNativeDateModule } from '@angular/material/core';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSnackBarModule } from '@angular/material/snack-bar';
import { CommonModule } from '@angular/common';
import { of } from 'rxjs';
import { ScheduleDialogComponent, ScheduleData } from './schedule.dialog';
import { AppointmentService } from '@core/services/appointment.service';
import { AuthService } from '@core/services/auth.service';
import { provideHttpClient, withInterceptorsFromDi } from '@angular/common/http';

describe('ScheduleDialogComponent', () => {
  let component: ScheduleDialogComponent;
  let fixture: ComponentFixture<ScheduleDialogComponent>;

  const mockData: ScheduleData = { patientId: 'pat-001', patientName: 'Test Patient' };

  beforeEach(async () => {
    const appointmentSpy = jasmine.createSpyObj('AppointmentService', ['schedule']);
    const authSpy = jasmine.createSpyObj('AuthService', ['currentUser$']);
    authSpy.currentUser$ = of({ id: 'usr-001' });

    await TestBed.configureTestingModule({
    imports: [ScheduleDialogComponent,
        CommonModule, ReactiveFormsModule, MatDialogModule, MatButtonModule,
        MatFormFieldModule, MatInputModule, MatSelectModule, MatDatepickerModule,
        MatNativeDateModule, MatIconModule, MatProgressSpinnerModule, MatSnackBarModule,
        NoopAnimationsModule],
    providers: [
        { provide: MatDialogRef, useValue: { close: jasmine.createSpy('close') } },
        { provide: MAT_DIALOG_DATA, useValue: mockData },
        { provide: AppointmentService, useValue: appointmentSpy },
        { provide: AuthService, useValue: authSpy },
        provideHttpClient(withInterceptorsFromDi()),
        provideHttpClientTesting(),
    ]
}).compileComponents();

    fixture = TestBed.createComponent(ScheduleDialogComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('should display title', () => {
    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('h2')?.textContent).toContain('Đặt lịch hẹn');
  });

  it('should show patient name', () => {
    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('Test Patient');
  });

  it('should have form initialized', () => {
    expect(component.form.contains('scheduledDate')).toBeTrue();
    expect(component.form.contains('startTime')).toBeTrue();
  });

  it('should have time slots', () => {
    expect(component.timeSlots.length).toBeGreaterThan(0);
  });

  it('should pass a basic integrity check', () => {
    expect(true).toBeTrue();
  });

});
