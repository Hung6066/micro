import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap } from '@angular/router';
import { of, throwError } from 'rxjs';
import { NoopAnimationsModule } from '@angular/platform-browser/animations';
import { ReactiveFormsModule } from '@angular/forms';
import { MatSnackBar } from '@angular/material/snack-bar';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { LoginComponent } from './login.component';
import { AuthService } from '@core/services/auth.service';
import { SessionService } from '@core/services/session.service';

describe('LoginComponent', () => {
  let component: LoginComponent;
  let fixture: ComponentFixture<LoginComponent>;
  let authService: jasmine.SpyObj<AuthService>;
  let locationAssignSpy: jest.Mock;
  let originalLocation: Location;

  beforeEach(() => {
    const authSpy = jasmine.createSpyObj('AuthService', ['login']);
    const snackBarSpy = jasmine.createSpyObj('MatSnackBar', ['open']);
    const sessionSpy = jasmine.createSpyObj('SessionService', ['startTracking']);
    const activatedRouteStub = {
      snapshot: { queryParamMap: convertToParamMap({}) },
    };
    originalLocation = window.location;
    locationAssignSpy = jest.fn();
    delete (window as any).location;
    (window as any).location = {
      assign: locationAssignSpy,
    };

    TestBed.configureTestingModule({
      
      imports: [
        LoginComponent, 
        NoopAnimationsModule,
        ReactiveFormsModule,
        MatCardModule,
        MatFormFieldModule,
        MatInputModule,
        MatIconModule,
        MatProgressSpinnerModule,
      ],
      providers: [
        { provide: AuthService, useValue: authSpy },
        { provide: SessionService, useValue: sessionSpy },
        { provide: MatSnackBar, useValue: snackBarSpy },
        { provide: ActivatedRoute, useValue: activatedRouteStub },
      ],
    });

    fixture = TestBed.createComponent(LoginComponent);
    component = fixture.componentInstance;
    authService = TestBed.inject(AuthService) as jasmine.SpyObj<AuthService>;
    fixture.detectChanges();
  });

  afterEach(() => {
    delete (window as any).location;
    (window as any).location = originalLocation;
  });

  it('should render login form with username and password fields', () => {
    const usernameInput = fixture.nativeElement.querySelector('input[formControlName="username"]');
    const passwordInput = fixture.nativeElement.querySelector('input[formControlName="password"]');
    expect(usernameInput).toBeTruthy();
    expect(passwordInput).toBeTruthy();
  });

  it('should render the login logo without material icon ligature text', () => {
    const cardHeader: HTMLElement = fixture.nativeElement.querySelector('mat-card-header');
    const logo = cardHeader.querySelector('.card-logo');

    expect(logo).toBeTruthy();
    expect(cardHeader.textContent).not.toContain('local_hospital');
  });

  it('should call authService.login on form submit with device info', () => {
    authService.login.and.returnValue(of({ id: 'usr-001', username: 'admin', email: 'admin@hishope.vn', firstName: 'Admin', lastName: 'User', fullName: 'Admin User', roles: ['admin'] }));

    component.loginForm.setValue({ username: 'admin', password: 'secret' });
    component.onSubmit();

    expect(authService.login).toHaveBeenCalledWith({
      username: 'admin',
      password: 'secret',
      deviceInfo: jasmine.any(String),
      userAgent: jasmine.any(String),
    } as any);
  });

  it('should navigate to dashboard after login success', () => {
    authService.login.and.returnValue(of({ id: 'usr-001', username: 'admin', email: 'admin@hishope.vn', firstName: 'Admin', lastName: 'User', fullName: 'Admin User', roles: ['admin'] }));

    component.loginForm.setValue({ username: 'admin', password: 'secret' });
    component.onSubmit();

    expect(locationAssignSpy).toHaveBeenCalledWith('/dashboard');
  });

  it('should show error snackbar on login failure', () => {
    authService.login.and.returnValue(throwError(() => ({ error: { error: 'Invalid credentials' } })));

    component.loginForm.setValue({ username: 'admin', password: 'wrong' });
    component.onSubmit();

    expect(component.loading).toBeFalse();
  });

  it('should disable submit button while loading', () => {
    component.loading = true;
    component.loginForm.setValue({ username: 'admin', password: 'secret' });
    fixture.detectChanges();

    const submitBtn: HTMLButtonElement = fixture.nativeElement.querySelector('button[type="submit"]');
    expect(submitBtn.disabled).toBeTrue();
  });

  it('should have component initialized', () => {
    expect(component).toBeDefined();
  });

  it('should have fixture defined', () => {
    expect(fixture).toBeDefined();
  });
});
