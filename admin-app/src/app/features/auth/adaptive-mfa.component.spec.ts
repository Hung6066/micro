import { ActivatedRoute, Router, convertToParamMap } from '@angular/router';
import { fakeAsync, TestBed, tick } from '@angular/core/testing';
import { BehaviorSubject, of } from 'rxjs';
import {
  createHisHopeAdaptiveMfaState,
  getHisHopeAdaptiveMfaAlternateMethods,
  setHisHopeAdaptiveMfaAlternateMethodsOpen,
} from '@his-hope/frontend-foundation';
import { AuthService } from '../../core/services/auth.service';
import { LoginComponent } from './login.component';

describe('adaptive MFA public API', () => {
  it('defaults to passkey when available', () => {
    const state = createHisHopeAdaptiveMfaState({
      availableMethods: ['passkey', 'mobileApproval', 'totp'],
    });

    expect(state.preferredMethod).toBe('passkey');
    expect(state.availableMethods).toEqual(['passkey', 'mobileApproval', 'totp']);
    expect(getHisHopeAdaptiveMfaAlternateMethods(state)).toEqual(['mobileApproval', 'totp']);
  });

  it('prefers mobile approval for unfamiliar devices', () => {
    const state = createHisHopeAdaptiveMfaState({
      availableMethods: ['passkey', 'mobileApproval', 'totp'],
      unfamiliarDevice: true,
    });

    expect(state.preferredMethod).toBe('mobileApproval');
  });

  it('tracks alternate method disclosure explicitly', () => {
    const state = createHisHopeAdaptiveMfaState({
      availableMethods: ['passkey', 'mobileApproval', 'totp'],
    });

    const opened = setHisHopeAdaptiveMfaAlternateMethodsOpen(state, true);
    const closed = setHisHopeAdaptiveMfaAlternateMethodsOpen(opened, false);

    expect(opened.alternateMethodsOpen).toBeTrue();
    expect(closed.alternateMethodsOpen).toBeFalse();
  });

  it('keeps totp-only availability valid', () => {
    const state = createHisHopeAdaptiveMfaState({
      availableMethods: ['totp'],
      unfamiliarDevice: true,
    });

    expect(state.preferredMethod).toBe('totp');
    expect(state.availableMethods).toEqual(['totp']);
    expect(getHisHopeAdaptiveMfaAlternateMethods(state)).toEqual([]);
  });
});

describe('LoginComponent', () => {
  function configureTestBed(returnUrl?: string): void {
    const isAuthenticated$ = new BehaviorSubject(false);
    const authService = jasmine.createSpyObj<AuthService>(
      'AuthService',
      ['login', 'trySsoLogin'],
      { isAuthenticated$: isAuthenticated$.asObservable() },
    );
    authService.trySsoLogin.and.returnValue(of(false));

    TestBed.configureTestingModule({
      imports: [LoginComponent],
      providers: [
        { provide: AuthService, useValue: authService },
        {
          provide: Router,
          useValue: {
            navigate: jasmine.createSpy('navigate').and.resolveTo(true),
            url: '/auth/login',
          },
        },
        {
          provide: ActivatedRoute,
          useValue: {
            snapshot: {
              queryParamMap: convertToParamMap(
                returnUrl ? { returnUrl } : {},
              ),
            },
          },
        },
      ],
    });
  }

  it('uses the shared safe login path for a requested return url', () => {
    configureTestBed('/users?tab=security');
    const fixture = TestBed.createComponent(LoginComponent);
    const authService = TestBed.inject(AuthService) as jasmine.SpyObj<AuthService>;

    fixture.componentInstance.startLogin();

    expect(authService.login).toHaveBeenCalledWith('/users?tab=security');
  });

  it('preserves the requested return url during automatic SSO polling', fakeAsync(() => {
    configureTestBed('/users?tab=security');
    const fixture = TestBed.createComponent(LoginComponent);
    const authService = TestBed.inject(AuthService) as jasmine.SpyObj<AuthService>;

    fixture.detectChanges();
    tick();

    expect(authService.trySsoLogin).toHaveBeenCalledWith('/users?tab=security');

    fixture.destroy();
  }));
});
