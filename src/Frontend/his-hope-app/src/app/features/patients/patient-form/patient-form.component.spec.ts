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
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatSnackBarModule } from '@angular/material/snack-bar';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { CommonModule } from '@angular/common';
import { of } from 'rxjs';
import { PatientFormComponent } from './patient-form.component';
import { PatientService } from '@core/services/patient.service';

describe('PatientFormComponent', () => {
  let component: PatientFormComponent;
  let fixture: ComponentFixture<PatientFormComponent>;

  beforeEach(async () => {
    const patientServiceSpy = jasmine.createSpyObj('PatientService', ['getById', 'create', 'update']);
    patientServiceSpy.getById.and.returnValue(of({} as any));

    await TestBed.configureTestingModule({
      declarations: [PatientFormComponent],
      imports: [
        RouterTestingModule,
        NoopAnimationsModule,
        HttpClientTestingModule,
        ReactiveFormsModule,
        MatFormFieldModule,
        MatInputModule,
        MatSelectModule,
        MatDatepickerModule,
        MatNativeDateModule,
        MatButtonModule,
        MatIconModule,
        MatSnackBarModule,
        MatProgressSpinnerModule,
        CommonModule,
      ],
      providers: [
        { provide: PatientService, useValue: patientServiceSpy },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(PatientFormComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('should render form title', () => {
    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('h1')?.textContent).toContain('Thêm bệnh nhân mới');
  });

  it('should have form fields', () => {
    expect(component.patientForm.contains('firstName')).toBeTrue();
    expect(component.patientForm.contains('lastName')).toBeTrue();
    expect(component.patientForm.contains('dateOfBirth')).toBeTrue();
    expect(component.patientForm.contains('genderCode')).toBeTrue();
    expect(component.patientForm.contains('phone')).toBeTrue();
  });

  it('should be invalid when empty', () => {
    expect(component.patientForm.valid).toBeFalse();
  });

  it('should show save button', () => {
    const compiled = fixture.nativeElement as HTMLElement;
    const buttons = compiled.querySelectorAll('button');
    expect(buttons.length).toBeGreaterThan(0);
  });

  it('should show cancel button with routerLink', () => {
    const compiled = fixture.nativeElement as HTMLElement;
    const cancelBtn = compiled.querySelector('button[routerLink="/patients"]');
    expect(cancelBtn).toBeTruthy();
  });

  it('should have component initialized', () => {
    expect(component).toBeDefined();
  });

  it('should have fixture defined', () => {
    expect(fixture).toBeDefined();
  });
});
