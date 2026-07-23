import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, Router } from '@angular/router';
import { of } from 'rxjs';
import { NoopAnimationsModule } from '@angular/platform-browser/animations';
import { RouterModule } from '@angular/router';
import { LoginComponent } from './login.component';
import { AuthService } from '@core/services/auth.service';

describe('LoginComponent', () => {
  let component: LoginComponent;
  let fixture: ComponentFixture<LoginComponent>;
  let authService: jasmine.SpyObj<AuthService>;
  let router: jasmine.SpyObj<Router>;

  beforeEach(() => {
    const authSpy = jasmine.createSpyObj('AuthService', ['oidcLogin', 'isLoggedIn']);
    authSpy.isLoggedIn.and.returnValue(of(false));
    const routerSpy = jasmine.createSpyObj('Router', ['navigateByUrl']);
    const activatedRouteStub = {
      snapshot: { queryParamMap: convertToParamMap({}) },
    };

    TestBed.configureTestingModule({
      imports: [
        LoginComponent,
        NoopAnimationsModule,
        RouterModule,
      ],
      providers: [
        { provide: AuthService, useValue: authSpy },
        { provide: Router, useValue: routerSpy },
        { provide: ActivatedRoute, useValue: activatedRouteStub },
      ],
    });

    fixture = TestBed.createComponent(LoginComponent);
    component = fixture.componentInstance;
    authService = TestBed.inject(AuthService) as jasmine.SpyObj<AuthService>;
    router = TestBed.inject(Router) as jasmine.SpyObj<Router>;
    fixture.detectChanges();
  });

  it('should render the Sign in with His.Hope button', () => {
    const signInBtn: HTMLButtonElement | undefined = Array.from(
      fixture.nativeElement.querySelectorAll('.oidc-btn'),
    ).find((btn: any) => btn.textContent?.trim().includes('Sign in with His.Hope'));
    expect(signInBtn).toBeTruthy();
  });

  it('should render the Continue with Google button', () => {
    const googleBtn: HTMLButtonElement | undefined = Array.from(
      fixture.nativeElement.querySelectorAll('.oidc-btn'),
    ).find((btn: any) => btn.textContent?.trim().includes('Continue with Google'));
    expect(googleBtn).toBeTruthy();
  });

  it('should call authService.oidcLogin when sign in button is clicked', () => {
    const signInBtn: HTMLButtonElement | undefined = Array.from(
      fixture.nativeElement.querySelectorAll('.oidc-btn'),
    ).find((btn: any) => btn.textContent?.trim().includes('Sign in with His.Hope'));
    signInBtn!.click();
    expect(authService.oidcLogin).toHaveBeenCalled();
  });

  it('should call authService.oidcLogin when Google button is clicked', () => {
    const googleBtn: HTMLButtonElement | undefined = Array.from(
      fixture.nativeElement.querySelectorAll('.oidc-btn'),
    ).find((btn: any) => btn.textContent?.trim().includes('Continue with Google'));
    googleBtn!.click();
    expect(authService.oidcLogin).toHaveBeenCalled();
  });

  it('should call authService.oidcLogin when Microsoft button is clicked', () => {
    const msBtn: HTMLButtonElement | undefined = Array.from(
      fixture.nativeElement.querySelectorAll('.oidc-btn'),
    ).find((btn: any) => btn.textContent?.trim().includes('Continue with Microsoft'));
    msBtn!.click();
    expect(authService.oidcLogin).toHaveBeenCalled();
  });

  it('should auto-redirect to dashboard when already authenticated on init', () => {
    authService.isLoggedIn.and.returnValue(of(true));
    component.ngOnInit();
    expect(router.navigateByUrl).toHaveBeenCalledWith('/dashboard');
  });

  it('should auto-redirect to returnUrl when already authenticated', () => {
    const route = TestBed.inject(ActivatedRoute);
    (route.snapshot.queryParamMap as any) = convertToParamMap({ returnUrl: '/settings' });
    authService.isLoggedIn.and.returnValue(of(true));
    component.ngOnInit();
    expect(router.navigateByUrl).toHaveBeenCalledWith('/settings');
  });

  it('should not redirect when not authenticated', () => {
    component.ngOnInit();
    expect(router.navigateByUrl).not.toHaveBeenCalled();
  });

  it('should pass returnUrl to oidcLogin when returnUrl param exists', () => {
    const route = TestBed.inject(ActivatedRoute);
    (route.snapshot.queryParamMap as any) = convertToParamMap({ returnUrl: '/admin' });
    component.signIn();
    expect(authService.oidcLogin).toHaveBeenCalledWith('/admin');
  });

  it('should have component initialized', () => {
    expect(component).toBeDefined();
  });
});
