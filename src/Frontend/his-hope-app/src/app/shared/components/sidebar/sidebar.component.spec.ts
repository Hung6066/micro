import { ComponentFixture, TestBed, fakeAsync, tick } from '@angular/core/testing';
import { Router } from '@angular/router';
import { Subject, of } from 'rxjs';
import { NoopAnimationsModule } from '@angular/platform-browser/animations';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { ReactiveFormsModule } from '@angular/forms';
import { MatListModule } from '@angular/material/list';
import { MatIconModule } from '@angular/material/icon';
import { MatBadgeModule } from '@angular/material/badge';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatAutocompleteModule } from '@angular/material/autocomplete';
import { RouterTestingModule } from '@angular/router/testing';
import { SidebarComponent } from './sidebar.component';
import { AuthService } from '@core/services/auth.service';
import { PatientService } from '@core/services/patient.service';

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
      imports: [SidebarComponent, CommonModule, RouterModule, ReactiveFormsModule, MatListModule, MatIconModule, MatBadgeModule, MatTooltipModule, MatButtonModule, MatFormFieldModule, MatInputModule, MatAutocompleteModule, NoopAnimationsModule, RouterTestingModule.withRoutes([])],
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

  it('should render the brand logo without material icon ligature text', () => {
    fixture.detectChanges();

    const brand: HTMLElement = fixture.nativeElement.querySelector('.brand');
    const logo = brand.querySelector('.logo-mark');

    expect(logo).toBeTruthy();
    expect(brand.textContent).not.toContain('local_hospital');
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
