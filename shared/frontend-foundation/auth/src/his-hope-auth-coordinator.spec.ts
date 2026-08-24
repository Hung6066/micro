import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Router } from '@angular/router';
import { OidcSecurityService } from 'angular-auth-oidc-client';
import { of, Subject, throwError } from 'rxjs';
import { HisHopeAuthCoordinator } from './his-hope-auth-coordinator';

describe('HisHopeAuthCoordinator', () => {
  beforeEach(() => {
    sessionStorage.clear();
  });

  afterEach(() => {
    sessionStorage.clear();
  });

  it('does not publish authenticated when BFF session exchange returns 401', () => {
    const oidc = { logoffLocal: jasmine.createSpy('logoffLocal') } as unknown as OidcSecurityService;
    const http = {
      get: jasmine.createSpy('get').and.returnValue(of({ authenticated: true })),
      post: jasmine.createSpy('post').and.returnValue(
        throwError(() => new HttpErrorResponse({ status: 401, statusText: 'Unauthorized' })),
      ),
    } as unknown as HttpClient;
    const router = { url: '/auth/login', navigate: jasmine.createSpy('navigate') } as unknown as Router;
    const states: boolean[] = [];

    new HisHopeAuthCoordinator(oidc, http, router, {
      defaultReturnUrl: '/dashboard',
      sessionStatusUrl: '/api/v1/auth/session-status',
      sessionExchangeUrl: '/api/v1/auth/session/exchange',
      logoutUrl: '/api/v1/auth/logout',
      bffOnly: true,
    }).isAuthenticated$.subscribe((state) => states.push(state));

    expect(states).toEqual([false]);
  });

  it('clears the server session when the portal client is forbidden', () => {
    const oidc = { logoffLocal: jasmine.createSpy('logoffLocal') } as unknown as OidcSecurityService;
    const http = {
      get: jasmine.createSpy('get').and.returnValue(of({ authenticated: true })),
      post: jasmine.createSpy('post').and.returnValue(
        throwError(() => new HttpErrorResponse({ status: 403, statusText: 'Forbidden' })),
      ),
    } as unknown as HttpClient;
    const router = {
      url: '/auth/login',
      navigate: jasmine.createSpy('navigate'),
    } as unknown as Router;

    new HisHopeAuthCoordinator(oidc, http, router, {
      defaultReturnUrl: '/dashboard',
      sessionStatusUrl: '/api/v1/auth/session-status',
      sessionExchangeUrl: '/api/v1/auth/session/exchange',
      logoutUrl: '/api/v1/auth/logout',
      bffOnly: true,
    }).isAuthenticated$.subscribe();

    expect(http.post).toHaveBeenCalledWith(
      '/api/v1/auth/session/exchange',
      {},
      { withCredentials: true },
    );
    expect(http.post).toHaveBeenCalledWith(
      '/api/v1/auth/logout',
      {},
      { withCredentials: true },
    );
  });

  it('exchanges an authenticated BFF cookie after the SSO marker survives the redirect', (done) => {
    const oidc = { logoffLocal: jasmine.createSpy('logoffLocal') } as unknown as OidcSecurityService;
    const http = {
      get: jasmine.createSpy('get').and.returnValue(of({ authenticated: true, userName: 'admin' })),
      post: jasmine.createSpy('post').and.returnValue(of(void 0)),
    } as unknown as HttpClient;
    const router = { url: '/auth/login', navigateByUrl: jasmine.createSpy('navigateByUrl') } as unknown as Router;
    sessionStorage.setItem('hishop_sso_login_in_progress_until', String(Date.now() + 60_000));

    const coordinator = new HisHopeAuthCoordinator(oidc, http, router, {
      defaultReturnUrl: '/dashboard',
      sessionStatusUrl: '/api/v1/auth/session-status',
      sessionExchangeUrl: '/api/v1/auth/session/exchange',
      logoutUrl: '/api/v1/auth/logout',
      bffClientId: 'customer-factory-x-app',
      bffOnly: true,
    });

    coordinator.trySsoLogin('/dashboard').subscribe((exchanged) => {
      expect(exchanged).toBeTrue();
      expect(http.post).toHaveBeenCalledWith(
        '/api/v1/auth/session/exchange',
        { clientId: 'customer-factory-x-app' },
        { withCredentials: true },
      );
      expect(router.navigateByUrl).toHaveBeenCalledWith('/dashboard');
      sessionStorage.removeItem('hishop_sso_login_in_progress_until');
      done();
    });
  });

  it('shares concurrent BFF exchanges so session rotation cannot leave a stale cookie', (done) => {
    const oidc = { logoffLocal: jasmine.createSpy('logoffLocal') } as unknown as OidcSecurityService;
    const exchange$ = new Subject<void>();
    const http = {
      get: jasmine.createSpy('get').and.returnValue(of({ authenticated: false })),
      post: jasmine.createSpy('post').and.returnValue(exchange$),
    } as unknown as HttpClient;
    const router = { url: '/dashboard', navigate: jasmine.createSpy('navigate') } as unknown as Router;
    const coordinator = new HisHopeAuthCoordinator(oidc, http, router, {
      defaultReturnUrl: '/dashboard',
      sessionStatusUrl: '/api/v1/auth/session-status',
      sessionExchangeUrl: '/api/v1/auth/session/exchange',
      logoutUrl: '/api/v1/auth/logout',
      bffOnly: true,
    });

    const first = coordinator.refreshAccessToken().subscribe(result => expect(result).toBeTrue());
    const second = coordinator.refreshAccessToken().subscribe(result => expect(result).toBeTrue());
    expect(http.post).toHaveBeenCalledTimes(1);
    exchange$.next();
    exchange$.complete();
    setTimeout(() => {
      first.unsubscribe();
      second.unsubscribe();
      done();
    }, 0);
  });
});
