import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideHttpClient, withInterceptorsFromDi } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { OidcSecurityService } from 'angular-auth-oidc-client';
import { of } from 'rxjs';
import { AuthService } from './auth.service';
import { User } from '@core/models/auth.model';
import { environment } from '@env/environment';

describe('AuthService (BFF/session contract)', () => {
  let service: AuthService;
  let httpMock: HttpTestingController;
  let oidc: ReturnType<typeof createOidcMock>;

  const user: User = {
    id: 'usr-001',
    username: 'admin',
    email: 'admin@hishope.vn',
    firstName: 'Admin',
    lastName: 'User',
    fullName: 'Admin User',
    roles: ['admin'],
    permissions: ['patients.view', 'patients.write'],
  };

  function createOidcMock() {
    return {
      authorize: jasmine.createSpy('authorize'),
      logoff: jasmine.createSpy('logoff').and.returnValue(of(undefined)),
      logoffLocal: jasmine.createSpy('logoffLocal'),
      checkAuth: jasmine.createSpy('checkAuth').and.returnValue(of({ isAuthenticated: false, userData: null })),
      getAccessToken: jasmine.createSpy('getAccessToken').and.returnValue(of('')),
      isAuthenticated$: of({ isAuthenticated: false }),
      userData$: of(null),
      isAuthenticated: jasmine.createSpy('isAuthenticated').and.returnValue(of(false)),
      forceRefreshSession: jasmine.createSpy('forceRefreshSession').and.returnValue(of({ isAuthenticated: false })),
    };
  }

  beforeEach(() => {
    sessionStorage.clear();
    oidc = createOidcMock();
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptorsFromDi()),
        provideHttpClientTesting(),
        { provide: OidcSecurityService, useValue: oidc },
      ],
    });
    service = TestBed.inject(AuthService);
    httpMock = TestBed.inject(HttpTestingController);
    httpMock.expectOne(`${environment.apiUrl}/auth/session-status`).flush({ authenticated: false });
  });

  afterEach(() => {
    httpMock.verify();
    sessionStorage.clear();
  });

  it('boots the BFF session coordinator without an OIDC authorize call', () => {
    service.checkAuth().subscribe();
    expect(oidc.authorize).not.toHaveBeenCalled();
  });

  it('maps external providers without exposing credentials', (done) => {
    service.externalProviders$.subscribe((providers) => {
      expect(providers).toEqual([{ provider: 'AcmeSaml', displayName: 'Acme SAML', icon: 'openid' }]);
      done();
    });
    const request = httpMock.expectOne(`${environment.apiUrl}/auth/external-providers`);
    expect(request.request.method).toBe('GET');
    request.flush({ providers: [{ provider: 'AcmeSaml', displayName: 'Acme SAML', icon: 'openid', clientSecret: 'redacted' }] });
  });

  it('loads the current user and hydrates the shared permission snapshot', (done) => {
    service.getCurrentUser().subscribe((result) => {
      expect(result).toEqual(user);
      done();
    });
    httpMock.expectOne(`${environment.apiUrl}/auth/me`).flush(user);
  });

  it('enforces permission checks with AND semantics', () => {
    (service as any).currentUserSubject.next(user);
    expect(service.hasPermission('patients.view')).toBeTrue();
    expect(service.hasPermission(['patients.view', 'patients.write'])).toBeTrue();
    expect(service.hasPermission(['patients.view', 'billing.view'])).toBeFalse();
  });

  it('checks a permission on the server and caches the result', () => {
    service.hasPermissionOnServer('patients.view').subscribe((granted) => expect(granted).toBeTrue());
    const request = httpMock.expectOne(`${environment.apiUrl}/auth/check-permission`);
    expect(request.request.body).toEqual({ permission: 'patients.view' });
    request.flush({ granted: true });

    service.hasPermissionOnServer('patients.view').subscribe((granted) => expect(granted).toBeTrue());
    httpMock.expectNone(`${environment.apiUrl}/auth/check-permission`);
  });

  it('clears user and permission state on logout failure', () => {
    (service as any).currentUserSubject.next(user);
    service.logout().subscribe();
    const request = httpMock.expectOne(`${environment.apiUrl}/auth/logout`);
    request.flush({}, { status: 500, statusText: 'failure' });
    const retry = httpMock.expectOne(`${environment.apiUrl}/auth/logout`);
    retry.flush({}, { status: 500, statusText: 'failure' });
    expect((service as any).currentUserSubject.value).toBeNull();
  });

  it('uses the session-backed access token contract', (done) => {
    service.getAccessToken().subscribe((token) => {
      expect(token).toBe('');
      done();
    });
  });
});
