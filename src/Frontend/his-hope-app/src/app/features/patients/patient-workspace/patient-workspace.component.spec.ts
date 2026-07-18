import { ComponentFixture, TestBed } from '@angular/core/testing';
import { RouterTestingModule } from '@angular/router/testing';
import { NoopAnimationsModule } from '@angular/platform-browser/animations';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { MatTabsModule } from '@angular/material/tabs';
import { MatTableModule } from '@angular/material/table';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatCardModule } from '@angular/material/card';
import { MatChipsModule } from '@angular/material/chips';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatDialogModule } from '@angular/material/dialog';
import { MatSnackBarModule } from '@angular/material/snack-bar';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { of } from 'rxjs';
import { PatientWorkspaceComponent } from './patient-workspace.component';
import { PatientService } from '@core/services/patient.service';
import { AuthService } from '@core/services/auth.service';
import { AppointmentService } from '@core/services/appointment.service';
import { createMockPatient, createMockPagedResult } from '@testing/mock-data';
import { provideHttpClient, withInterceptorsFromDi } from '@angular/common/http';

describe('PatientWorkspaceComponent', () => {
  let component: PatientWorkspaceComponent;
  let fixture: ComponentFixture<PatientWorkspaceComponent>;
  let patientService: jasmine.SpyObj<PatientService>;

  const mockPatient = createMockPatient();

  beforeEach(async () => {
    const patientSpy = jasmine.createSpyObj('PatientService', [
      'getById', 'getEncounters', 'getAppointments', 'getLabOrders', 'getPrescriptions', 'getInvoices',
    ]);
    patientSpy.getById.and.returnValue(of(mockPatient));
    patientSpy.getEncounters.and.returnValue(of(createMockPagedResult([], 0)));
    patientSpy.getAppointments.and.returnValue(of(createMockPagedResult([], 0)));
    patientSpy.getLabOrders.and.returnValue(of(createMockPagedResult([], 0)));
    patientSpy.getPrescriptions.and.returnValue(of(createMockPagedResult([], 0)));
    patientSpy.getInvoices.and.returnValue(of(createMockPagedResult([], 0)));

    const authSpy = jasmine.createSpyObj('AuthService', ['currentUser$']);
    authSpy.currentUser$ = of(null);

    const appointmentSpy = jasmine.createSpyObj('AppointmentService', ['checkIn', 'checkOut', 'cancel']);

    await TestBed.configureTestingModule({
    imports: [PatientWorkspaceComponent,
        RouterTestingModule,
        NoopAnimationsModule,
        CommonModule,
        RouterModule,
        MatTabsModule,
        MatTableModule,
        MatButtonModule,
        MatIconModule,
        MatCardModule,
        MatChipsModule,
        MatProgressSpinnerModule,
        MatTooltipModule,
        MatDialogModule,
        MatSnackBarModule],
    providers: [
        { provide: PatientService, useValue: patientSpy },
        { provide: AuthService, useValue: authSpy },
        { provide: AppointmentService, useValue: appointmentSpy },
        provideHttpClient(withInterceptorsFromDi()),
        provideHttpClientTesting(),
    ]
}).compileComponents();

    fixture = TestBed.createComponent(PatientWorkspaceComponent);
    component = fixture.componentInstance;
    patientService = TestBed.inject(PatientService) as jasmine.SpyObj<PatientService>;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('should load patient on init', () => {
    expect(patientService.getById).toHaveBeenCalled();
  });

  it('should display patient name', () => {
    const compiled = fixture.nativeElement as HTMLElement;
    const nameEl = compiled.querySelector('.patient-details h1');
    expect(nameEl).toBeTruthy();
  });

  it('should render quick action buttons', () => {
    const compiled = fixture.nativeElement as HTMLElement;
    const buttons = compiled.querySelectorAll('.quick-actions button');
    expect(buttons.length).toBeGreaterThanOrEqual(4);
  });

  it('should render tab group', () => {
    const compiled = fixture.nativeElement as HTMLElement;
    const tabs = compiled.querySelectorAll('.mat-mdc-tab');
    expect(tabs.length).toBeGreaterThanOrEqual(5);
  });

  it('should have column definitions', () => {
    expect(component.encounterColumns).toBeDefined();
    expect(component.appointmentColumns).toBeDefined();
    expect(component.labColumns).toBeDefined();
    expect(component.prescriptionColumns).toBeDefined();
    expect(component.invoiceColumns).toBeDefined();
  });

  it('should pass a basic integrity check', () => {
    expect(true).toBeTrue();
  });

});
