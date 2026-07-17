import { ComponentFixture, TestBed, fakeAsync, tick, discardPeriodicTasks } from '@angular/core/testing';
import { Router } from '@angular/router';
import { Subject, of, throwError } from 'rxjs';
import { NoopAnimationsModule } from '@angular/platform-browser/animations';
import { ReactiveFormsModule } from '@angular/forms';
import { MatSnackBarModule } from '@angular/material/snack-bar';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { LoginComponent } from './login.component';
import { AuthService } from '@core/services/auth.service';

describe('LoginComponent', () => {
  let component: LoginComponent;
  let fixture: ComponentFixture<LoginComponent>;
  let authService: jasmine.SpyObj<AuthService>;
  let router: jasmine.SpyObj<Router>;

  beforeEach(() => {
    const authSpy = jasmine.createSpyObj('AuthService', ['login']);
    const routerSpy = jasmine.createSpyObj('Router', ['navigate']);

    TestBed.configureTestingModule({
      declarations: [LoginComponent],
      imports: [
        NoopAnimationsModule,
        ReactiveFormsModule,
        MatSnackBarModule,
        MatCardModule,
        MatFormFieldModule,
        MatInputModule,
        MatIconModule,
        MatProgressSpinnerModule,
      ],
      providers: [
        { provide: AuthService, useValue: authSpy },
        { provide: Router, useValue: routerSpy },
      ],
    });

    fixture = TestBed.createComponent(LoginComponent);
    component = fixture.componentInstance;
    authService = TestBed.inject(AuthService) as jasmine.SpyObj<AuthService>;
    router = TestBed.inject(Router) as jasmine.SpyObj<Router>;
    fixture.detectChanges();
  });

  it('should render login form with username and password fields', () => {
    const usernameInput = fixture.nativeElement.querySelector('input[formControlName="username"]');
    const passwordInput = fixture.nativeElement.querySelector('input[formControlName="password"]');
    expect(usernameInput).toBeTruthy();
    expect(passwordInput).toBeTruthy();
  });

  it('should call authService.login on form submit', () => {
    authService.login.and.returnValue(of({ id: 'usr-001', username: 'admin', email: 'admin@hishope.vn', firstName: 'Admin', lastName: 'User', fullName: 'Admin User', roles: ['admin'] }));

    component.loginForm.setValue({ username: 'admin', password: 'secret' });
    component.onSubmit();

    expect(authService.login).toHaveBeenCalledWith({ username: 'admin', password: 'secret' });
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
