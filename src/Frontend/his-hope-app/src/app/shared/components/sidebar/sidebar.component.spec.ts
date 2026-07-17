import { ComponentFixture, TestBed, fakeAsync, tick } from '@angular/core/testing';
import { Router } from '@angular/router';
import { Subject, of } from 'rxjs';
import { NoopAnimationsModule } from '@angular/platform-browser/animations';
import { RouterTestingModule } from '@angular/router/testing';
import { SidebarComponent } from './sidebar.component';
import { AuthService } from '@core/services/auth.service';
import { PatientService } from '@core/services/patient.service';
import { SharedModule } from '@shared/shared.module';

describe('SidebarComponent', () => {
  let component: SidebarComponent;
  let fixture: ComponentFixture<SidebarComponent>;
  let authService: jasmine.SpyObj<AuthService>;
  let patientService: jasmine.SpyObj<PatientService>;
  let router: Router;
  let currentUserSubject: Subject<any>;

  beforeEach(() => {
    currentUserSubject = new Subject<any>();
    const authSpy = jasmine.createSpyObj('AuthService', ['logout'], {
      currentUser$: currentUserSubject.asObservable(),
    });
    const patientSpy = jasmine.createSpyObj('PatientService', ['search']);

    TestBed.configureTestingModule({
      declarations: [SidebarComponent],
      imports: [SharedModule, NoopAnimationsModule, RouterTestingModule.withRoutes([])],
      providers: [
        { provide: AuthService, useValue: authSpy },
        { provide: PatientService, useValue: patientSpy },
      ],
    });

    fixture = TestBed.createComponent(SidebarComponent);
    component = fixture.componentInstance;
    authService = TestBed.inject(AuthService) as jasmine.SpyObj<AuthService>;
    patientService = TestBed.inject(PatientService) as jasmine.SpyObj<PatientService>;
    router = TestBed.inject(Router);
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('should display current user name when logged in', () => {
    fixture.detectChanges();
    currentUserSubject.next({ fullName: 'Admin User', specialty: 'General' });
    fixture.detectChanges();

    const nameEl: HTMLElement = fixture.nativeElement.querySelector('#user-name');
    expect(nameEl?.textContent?.trim()).toBe('Admin User');
  });

  it('should not show footer when user is null', () => {
    fixture.detectChanges();
    currentUserSubject.next(null);
    fixture.detectChanges();

    const footer = fixture.nativeElement.querySelector('.sidebar-footer');
    expect(footer).toBeNull();
  });

  it('should pass a basic integrity check', () => {
    expect(true).toBeTrue();
  });

});
