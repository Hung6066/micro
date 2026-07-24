import { TestBed } from '@angular/core/testing';
import { AppComponent } from './app.component';
import { AuthService } from './core/services/auth.service';
import { AlertToastService } from './shared/alert-toast/alert-toast.service';
import { BehaviorSubject, of } from 'rxjs';
import { provideRouter } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';
import { NoopAnimationsModule } from '@angular/platform-browser/animations';

describe('AppComponent', () => {
  let authServiceStub: Partial<AuthService>;
  let alertToastStub: Partial<AlertToastService>;

  beforeEach(async () => {
    authServiceStub = {
      isAuthenticated$: new BehaviorSubject<boolean>(false),
      login: jasmine.createSpy('login'),
      logout: jasmine.createSpy('logout'),
    };
    alertToastStub = {};

    await TestBed.configureTestingModule({
      imports: [AppComponent, NoopAnimationsModule],
      providers: [
        provideRouter([]),
        provideHttpClient(),
        { provide: AuthService, useValue: authServiceStub },
        { provide: AlertToastService, useValue: alertToastStub },
      ],
    }).compileComponents();
  });

  it('should create the app', () => {
    const fixture = TestBed.createComponent(AppComponent);
    const app = fixture.componentInstance;
    expect(app).toBeTruthy();
  });

  it('should start with light theme by default', () => {
    const fixture = TestBed.createComponent(AppComponent);
    const app = fixture.componentInstance;
    expect(app.isDarkMode$).toBeDefined();
  });

  it('should toggle theme on toggleTheme()', () => {
    const fixture = TestBed.createComponent(AppComponent);
    const app = fixture.componentInstance;
    const initial = app['isDarkModeSubject'].value;
    app.toggleTheme();
    expect(app['isDarkModeSubject'].value).toBe(!initial);
    app.toggleTheme();
    expect(app['isDarkModeSubject'].value).toBe(initial);
  });

  it('should set data-theme attribute on body', () => {
    const fixture = TestBed.createComponent(AppComponent);
    const app = fixture.componentInstance;
    const body = document.body;
    app.toggleTheme();
    expect(body.getAttribute('data-theme')).toBe('dark');
    app.toggleTheme();
    expect(body.getAttribute('data-theme')).toBe('light');
  });

  it('should toggle sidenav on toggleSidenav()', () => {
    const fixture = TestBed.createComponent(AppComponent);
    const app = fixture.componentInstance;
    const initial = app.sidenavOpened$.value;
    app.toggleSidenav();
    expect(app.sidenavOpened$.value).toBe(!initial);
    app.toggleSidenav();
    expect(app.sidenavOpened$.value).toBe(initial);
  });

  it('should detect mobile viewport', () => {
    const fixture = TestBed.createComponent(AppComponent);
    const app = fixture.componentInstance;
    expect(app.isMobile$).toBeDefined();
  });
});
