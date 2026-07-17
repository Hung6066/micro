import { ComponentFixture, TestBed } from '@angular/core/testing';
import { RouterTestingModule } from '@angular/router/testing';
import { NoopAnimationsModule } from '@angular/platform-browser/animations';
import { HttpClientTestingModule } from '@angular/common/http/testing';
import { ReactiveFormsModule } from '@angular/forms';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatNativeDateModule } from '@angular/material/core';
import { MatAutocompleteModule } from '@angular/material/autocomplete';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatSnackBarModule } from '@angular/material/snack-bar';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { of } from 'rxjs';
import { AppointmentFormComponent } from './appointment-form.component';
import { AppointmentService } from '@core/services/appointment.service';
import { PatientService } from '@core/services/patient.service';

describe('AppointmentFormComponent', () => {
  let component: AppointmentFormComponent;
  let fixture: ComponentFixture<AppointmentFormComponent>;

  beforeEach(async () => {
    const appointmentSpy = jasmine.createSpyObj('AppointmentService', ['schedule']);
    const patientSpy = jasmine.createSpyObj('PatientService', ['search', 'getById']);
    patientSpy.search.and.returnValue(of({ items: [], totalCount: 0, page: 1, pageSize: 10, hasNextPage: false, hasPreviousPage: false }));

    await TestBed.configureTestingModule({
      declarations: [AppointmentFormComponent],
      imports: [
        RouterTestingModule, NoopAnimationsModule, HttpClientTestingModule,
        ReactiveFormsModule, MatFormFieldModule, MatInputModule, MatSelectModule,
        MatDatepickerModule, MatNativeDateModule, MatAutocompleteModule,
        MatButtonModule, MatIconModule, MatSnackBarModule, MatProgressSpinnerModule,
      ],
      providers: [
        { provide: AppointmentService, useValue: appointmentSpy },
        { provide: PatientService, useValue: patientSpy },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(AppointmentFormComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('should render title', () => {
    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('h1')?.textContent).toContain('Đặt lịch hẹn');
  });

  it('should have providers list', () => {
    expect(component.providers.length).toBeGreaterThan(0);
  });

  it('should have form controls', () => {
    expect(component.appointmentForm.contains('patientId')).toBeTrue();
    expect(component.appointmentForm.contains('providerId')).toBeTrue();
    expect(component.appointmentForm.contains('scheduledDate')).toBeTrue();
    expect(component.appointmentForm.contains('startTime')).toBeTrue();
  });

  it('should be invalid when empty', () => {
    expect(component.appointmentForm.valid).toBeFalse();
  });

  it('should show cancel button', () => {
    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('button[routerLink="/appointments"]')).toBeTruthy();
  });

  it('should have component initialized', () => {
    expect(component).toBeDefined();
  });

  it('should have fixture defined', () => {
    expect(fixture).toBeDefined();
  });
});
