import { ComponentFixture, TestBed, fakeAsync, tick } from '@angular/core/testing';
import { Router } from '@angular/router';
import { Subject, of, throwError } from 'rxjs';
import { NoopAnimationsModule } from '@angular/platform-browser/animations';
import { DashboardComponent } from './dashboard.component';
import { AuthService } from '@core/services/auth.service';
import { DashboardService } from '@core/services/dashboard.service';
import { PatientService } from '@core/services/patient.service';
import { AppointmentService } from '@core/services/appointment.service';
import { ClinicalService } from '@core/services/clinical.service';
import { SharedModule } from '@shared/shared.module';

describe('DashboardComponent', () => {
  let component: DashboardComponent;
  let fixture: ComponentFixture<DashboardComponent>;
  let authService: jasmine.SpyObj<AuthService>;
  let dashboardService: jasmine.SpyObj<DashboardService>;
  let patientService: jasmine.SpyObj<PatientService>;
  let currentUserSubject: Subject<any>;

  beforeEach(() => {
    currentUserSubject = new Subject<any>();
    const authSpy = jasmine.createSpyObj('AuthService', [], {
      currentUser$: currentUserSubject.asObservable(),
    });
    const dashboardSpy = jasmine.createSpyObj('DashboardService', [
      'getStats', 'getRecentEncounters', 'getUpcomingAppointments',
    ]);
    dashboardSpy.getStats.and.returnValue(of({
      totalPatients: 0, todayAppointments: 0, activeEncounters: 0,
      pendingDiagnoses: 0, pendingLabs: 0, outstandingInvoices: 0,
      lowStockMedications: 0, newPatientsToday: 0, appointmentsTomorrow: 0,
      recentEncounters: [], upcomingAppointments: [],
    }));
    dashboardSpy.getRecentEncounters.and.returnValue(of({ items: [] }));
    dashboardSpy.getUpcomingAppointments.and.returnValue(of({ items: [] }));
    const patientSpy = jasmine.createSpyObj('PatientService', ['search']);
    patientSpy.search.and.returnValue(of({ items: [], totalCount: 0, page: 1, pageSize: 10, hasNextPage: false, hasPreviousPage: false }));
    const appointmentSpy = jasmine.createSpyObj('AppointmentService', ['list']);
    const clinicalSpy = jasmine.createSpyObj('ClinicalService', ['list']);
    const routerSpy = jasmine.createSpyObj('Router', ['navigate']);

    TestBed.configureTestingModule({
      declarations: [DashboardComponent],
      imports: [SharedModule, NoopAnimationsModule],
      providers: [
        { provide: AuthService, useValue: authSpy },
        { provide: DashboardService, useValue: dashboardSpy },
        { provide: PatientService, useValue: patientSpy },
        { provide: AppointmentService, useValue: appointmentSpy },
        { provide: ClinicalService, useValue: clinicalSpy },
        { provide: Router, useValue: routerSpy },
      ],
    });

    fixture = TestBed.createComponent(DashboardComponent);
    component = fixture.componentInstance;
    authService = TestBed.inject(AuthService) as jasmine.SpyObj<AuthService>;
    dashboardService = TestBed.inject(DashboardService) as jasmine.SpyObj<DashboardService>;
    patientService = TestBed.inject(PatientService) as jasmine.SpyObj<PatientService>;
  });

  it('should create', () => {
    fixture.detectChanges();
    expect(component).toBeTruthy();
  });

  it('should show loading state initially', () => {
    expect(component.loading).toBeTrue();
  });

  it('should display error state on load failure', fakeAsync(() => {
    dashboardService.getStats.and.returnValue(throwError(() => new Error('Failed')));

    fixture.detectChanges();
    currentUserSubject.next({ fullName: 'Admin' });
    tick();

    expect(component.error).toBeTruthy();
    expect(component.loading).toBeFalse();
  }));

  it('should have component initialized', () => {
    expect(component).toBeDefined();
  });

  it('should have fixture defined', () => {
    expect(fixture).toBeDefined();
  });
});
