import { TestBed } from '@angular/core/testing';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { AuthService } from './auth.service';
import { User } from '@core/models/auth.model';
import { environment } from '@env/environment';
import { provideHttpClient, withInterceptorsFromDi } from '@angular/common/http';
import { OidcSecurityService } from 'angular-auth-oidc-client';
import { of } from 'rxjs';

describe('AuthService', () => {
  let service: AuthService;
  let httpMock: HttpTestingController;
  let mockOidcSecurityService: ReturnType<typeof createMockOidc>;

  function createMockOidc() {
    return {
      authorize: jasmine.createSpy('authorize'),
      logoff: jasmine.createSpy('logoff').and.returnValue(of(undefined)),
      checkAuth: jasmine.createSpy('checkAuth').and.returnValue(of({ isAuthenticated: false, userData: null })),
      getAccessToken: jasmine.createSpy('getAccessToken').and.returnValue(of('mock-token')),
      isAuthenticated$: of({ isAuthenticated: true }),
      userData$: of(null),
      isAuthenticated: jasmine.createSpy('isAuthenticated').and.returnValue(of(true)),
      forceRefreshSession: jasmine.createSpy('forceRefreshSession').and.returnValue(of(undefined)),
    };
  }

  const mockUser: User = {
    id: 'usr-001',
    username: 'admin',
    email: 'admin@hishope.vn',
    firstName: 'Admin',
    lastName: 'User',
    fullName: 'Admin User',
    roles: ['admin'],
    permissions: ['patients.view', 'patients.write'],
  };

  beforeEach(() => {
    sessionStorage.clear();
    mockOidcSecurityService = createMockOidc();
    TestBed.configureTestingModule({
      imports: [],
      providers: [
        provideHttpClient(withInterceptorsFromDi()),
        provideHttpClientTesting(),
        { provide: OidcSecurityService, useValue: mockOidcSecurityService },
      ],
    });
    service = TestBed.inject(AuthService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
    sessionStorage.clear();
  });

  // ─── OIDC Methods ─────────────────────────────────────────────────

  describe('OIDC authentication', () => {
    it('should call authorize on oidcLogin', () => {
      service.oidcLogin();
      expect(mockOidcSecurityService.authorize).toHaveBeenCalled();
    });

    it('should store returnUrl in sessionStorage on oidcLogin', () => {
      service.oidcLogin('/dashboard');
      expect(sessionStorage.getItem('oidc_returnUrl')).toBe('/dashboard');
    });

    it('should not store returnUrl when not provided', () => {
      service.oidcLogin();
      expect(sessionStorage.getItem('oidc_returnUrl')).toBeNull();
    });

    it('should call logoff on oidcLogout and clear user state', () => {
      (service as any).currentUserSubject.next(mockUser);
      service.oidcLogout();
      expect(mockOidcSecurityService.logoff).toHaveBeenCalled();
      expect((service as any).currentUserSubject.value).toBeNull();
    });

    it('should handle callback via checkAuth and load user on success', (done) => {
      mockOidcSecurityService.checkAuth.and.returnValue(of({ isAuthenticated: true, userData: { sub: 'usr-001' } }));

      service.handleCallback().subscribe((isAuth) => {
        expect(isAuth).toBeTrue();
        expect(mockOidcSecurityService.checkAuth).toHaveBeenCalled();
        done();
      });
    });

    it('should return false from handleCallback when not authenticated', (done) => {
      mockOidcSecurityService.checkAuth.and.returnValue(of({ isAuthenticated: false, userData: null }));

      service.handleCallback().subscribe((isAuth) => {
        expect(isAuth).toBeFalse();
        done();
      });
    });
  });

  // ─── Role & Permission Methods ────────────────────────────────────

  it('should return user roles from subject', () => {
    (service as any).currentUserSubject.next(mockUser);
    expect(service.getUserRoles()).toEqual(['admin']);
  });

  it('should return empty roles when no user', () => {
    (service as any).currentUserSubject.next(null);
    expect(service.getUserRoles()).toEqual([]);
  });

  it('should check permission', () => {
    (service as any).currentUserSubject.next(mockUser);
    expect(service.hasPermission('patients.view')).toBeTrue();
    expect(service.hasPermission('nonexistent')).toBeFalse();
  });

  it('should check multiple permissions with AND logic', () => {
    (service as any).currentUserSubject.next(mockUser);
    expect(service.hasPermission(['patients.view', 'patients.write'])).toBeTrue();
    expect(service.hasPermission(['patients.view', 'nonexistent'])).toBeFalse();
  });

  it('should check hasRole with string array', () => {
    (service as any).currentUserSubject.next({ ...mockUser, roles: ['admin', 'doctor'] });
    expect(service.hasRole('admin')).toBeTrue();
    expect(service.hasRole('nurse')).toBeFalse();
    expect(service.hasRole(['admin', 'nurse'])).toBeTrue();
  });

  it('should getCurrentUserRoles observable', (done) => {
    service.getCurrentUserRoles().subscribe((roles) => {
      expect(roles).toEqual([]);
      done();
    });
  });

  it('should getUserPermissions from current user when available', () => {
    (service as any).currentUserSubject.next(mockUser);
    expect(service.getUserPermissions()).toEqual(['patients.view', 'patients.write']);
  });

  it('should return empty permissions when no user', () => {
    (service as any).currentUserSubject.next(null);
    expect(service.getUserPermissions()).toEqual([]);
  });

  it('should check hasPermissionOnServer', () => {
    service.hasPermissionOnServer('patients.view').subscribe((granted) => {
      expect(granted).toBeTrue();
    });
    const req = httpMock.expectOne(`${environment.apiUrl}/auth/check-permission`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ permission: 'patients.view' });
    req.flush({ granted: true });
  });

  it('should cache hasPermissionOnServer result', () => {
    service.hasPermissionOnServer('patients.view').subscribe();
    httpMock.expectOne(`${environment.apiUrl}/auth/check-permission`).flush({ granted: true });

    service.hasPermissionOnServer('patients.view').subscribe((granted) => {
      expect(granted).toBeTrue();
    });
    httpMock.expectNone(`${environment.apiUrl}/auth/check-permission`);
  });

  it('should emit current user via currentUser$', (done) => {
    (service as any).currentUserSubject.next(mockUser);
    service.currentUser$.subscribe((user) => {
      expect(user).toEqual(mockUser);
      done();
    });
  });

  it('should return isLoggedIn based on current user', (done) => {
    (service as any).currentUserSubject.next(mockUser);
    service.isLoggedIn().subscribe((loggedIn) => {
      expect(loggedIn).toBeTrue();
      done();
    });
  });

  it('should return isLoggedIn from OIDC when no current user', (done) => {
    service.isLoggedIn().subscribe((loggedIn) => {
      expect(loggedIn).toBeTrue();
      done();
    });
  });

  // ─── Deprecated HTTP Methods (kept for backward compatibility) ──

  xdescribe('Deprecated HTTP methods', () => {
    beforeEach(() => {
      mockOidcSecurityService.checkAuth.and.returnValue(of({ isAuthenticated: false, userData: null }));
    });

    it('should login, store token in memory, and return user', () => {
      const request = { username: 'admin', password: 'secret' };
      const response = { accessToken: 'jwt-token', user: mockUser };

      service.login(request).subscribe((user) => {
        expect(user).toEqual(mockUser);
      });

      const req = httpMock.expectOne(`${environment.apiUrl}/auth/login`);
      expect(req.request.method).toBe('POST');
      expect(req.request.withCredentials).toBeTrue();
      req.flush(response);
    });

    it('should register and return user', () => {
      const request = {
        username: 'newuser',
        email: 'new@hishope.vn',
        password: 'secret',
        firstName: 'New',
        lastName: 'User',
      };

      service.register(request).subscribe((user) => {
        expect(user).toEqual(mockUser);
      });

      const req = httpMock.expectOne(`${environment.apiUrl}/auth/register`);
      expect(req.request.method).toBe('POST');
      req.flush(mockUser);
    });

    it('should refresh token', () => {
      service.refreshToken().subscribe((user) => {
        expect(user).toEqual(mockUser);
      });

      const req = httpMock.expectOne(`${environment.apiUrl}/auth/refresh`);
      expect(req.request.method).toBe('POST');
      req.flush({
        accessToken: 'fresh-token',
        refreshToken: 'fresh-refresh-token',
        expiresAt: '2026-07-20T12:00:00Z',
        user: mockUser,
      });
    });

    it('should logout and clear token from memory', () => {
      service.logout().subscribe();

      const req = httpMock.expectOne(`${environment.apiUrl}/auth/logout`);
      expect(req.request.method).toBe('POST');
      req.flush(null);
    });
  });
});
