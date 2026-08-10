import { TestBed } from '@angular/core/testing';
import { AppComponent } from './app.component';
import { AuthService } from './core/services/auth.service';
import { AlertToastService } from './shared/alert-toast/alert-toast.service';
import { BehaviorSubject, of } from 'rxjs';
import { provideRouter } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';
import { NoopAnimationsModule } from '@angular/platform-browser/animations';
import { HisHopeThemeService } from '@his-hope/frontend-foundation';

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

  it('should expose the shared theme service', () => {
    const fixture = TestBed.createComponent(AppComponent);
    const app = fixture.componentInstance;
    expect(TestBed.inject(HisHopeThemeService).resolvedTheme()).toBeDefined();
  });

  it('should toggle theme on toggleTheme()', () => {
    const fixture = TestBed.createComponent(AppComponent);
    const app = fixture.componentInstance;
    const themeService = TestBed.inject(HisHopeThemeService);
    const initial = themeService.resolvedTheme();
    app.toggleTheme();
    expect(themeService.resolvedTheme()).not.toBe(initial);
    app.toggleTheme();
    expect(themeService.resolvedTheme()).toBe(initial);
  });

  it('should set data-theme attribute on the document root', () => {
    const fixture = TestBed.createComponent(AppComponent);
    const app = fixture.componentInstance;
    const root = document.documentElement;
    app.toggleTheme();
    expect(root.getAttribute('data-theme')).toBe('dark');
    app.toggleTheme();
    expect(root.getAttribute('data-theme')).toBe('light');
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
