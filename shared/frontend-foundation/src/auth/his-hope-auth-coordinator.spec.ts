import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Router } from '@angular/router';
import { OidcSecurityService } from 'angular-auth-oidc-client';
import { of, throwError } from 'rxjs';
import { HisHopeAuthCoordinator } from './his-hope-auth-coordinator';

describe('HisHopeAuthCoordinator', () => {
  it('does not publish authenticated when BFF session exchange returns 401', () => {
    const oidc = {} as unknown as OidcSecurityService;
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
});
