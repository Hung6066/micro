import { TestBed } from '@angular/core/testing';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { AuthService } from './auth.service';
import { LoginRequest, RegisterRequest, User } from '@core/models/auth.model';
import { environment } from '@env/environment';
import { HttpErrorResponse, provideHttpClient, withInterceptorsFromDi } from '@angular/common/http';

describe('AuthService', () => {
  let service: AuthService;
  let httpMock: HttpTestingController;

  function createJwtToken(payload: Record<string, unknown>): string {
    const encoded = Buffer.from(JSON.stringify(payload)).toString('base64url');
    return `header.${encoded}.signature`;
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
    TestBed.configureTestingModule({
    imports: [],
    providers: [provideHttpClient(withInterceptorsFromDi()), provideHttpClientTesting()]
});
    service = TestBed.inject(AuthService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
    sessionStorage.clear();
  });

  it('should login, store token in memory, and return user', () => {
    const request: LoginRequest = { username: 'admin', password: 'secret' };
    const response = { accessToken: 'jwt-token', user: mockUser };

    service.login(request).subscribe((user) => {
      expect(user).toEqual(mockUser);
    });

    const req = httpMock.expectOne(`${environment.apiUrl}/auth/login`);
    expect(req.request.method).toBe('POST');
    expect(req.request.withCredentials).toBeTrue();
    req.flush(response);

    // Token stored in memory and mirrored to sessionStorage for reload hydration
    expect(service.getStoredAccessToken()).toBe('jwt-token');
    expect(sessionStorage.getItem('hishope_access_token')).toBe('jwt-token');
  });

  it('should register and return user', () => {
    const request: RegisterRequest = {
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

    expect(service.getStoredAccessToken()).toBe('fresh-token');
    expect(sessionStorage.getItem('hishope_access_token')).toBe('fresh-token');
  });

  it('should logout and clear token from memory', () => {
    service.storeAccessToken('existing-token');
    service.logout().subscribe();

    const req = httpMock.expectOne(`${environment.apiUrl}/auth/logout`);
    expect(req.request.method).toBe('POST');
    req.flush(null);

    expect(service.getStoredAccessToken()).toBeNull();
  });

  it('should return logged in status when token exists', () => {
    service.storeAccessToken('persisted-token');

    service.isLoggedIn().subscribe((loggedIn) => {
      expect(loggedIn).toBeTrue();
    });

    const req = httpMock.expectOne(`${environment.apiUrl}/auth/verify`);
    req.flush({ authenticated: true });
  });

  it('should hydrate the current user from stored token claims', (done) => {
    const token = createJwtToken({
      sub: mockUser.id,
      email: mockUser.email,
      unique_name: mockUser.username,
      fullName: mockUser.fullName,
      roles: mockUser.roles,
      permissions: mockUser.permissions?.join(','),
    });
    service.storeAccessToken(token);

    service.ensureCurrentUser().subscribe((user) => {
      expect(user).toEqual(mockUser);
      expect(service['currentUserSubject'].value).toEqual(mockUser);
      done();
    });
  });

  it('should hydrate unicode claims from stored token claims', (done) => {
    const token = createJwtToken({
      sub: mockUser.id,
      email: mockUser.email,
      unique_name: mockUser.username,
      fullName: 'Viên Quản Trị',
      roles: mockUser.roles,
      permissions: mockUser.permissions?.join(','),
    });
    service.storeAccessToken(token);

    service.ensureCurrentUser().subscribe((user) => {
      expect(user?.fullName).toBe('Viên Quản Trị');
      expect(user?.firstName).toBe('Viên Quản');
      expect(user?.lastName).toBe('Trị');
      done();
    });
  });

  it('should return false for isLoggedIn when verify fails', () => {
    service.storeAccessToken('persisted-token');

    service.isLoggedIn().subscribe((loggedIn) => {
      expect(loggedIn).toBeFalse();
    });

    const req = httpMock.expectOne(`${environment.apiUrl}/auth/verify`);
    req.error(new ProgressEvent('Network error'));
    // isLoggedIn() pipes retry(1), so handle the retry
    const retryReq = httpMock.expectOne(`${environment.apiUrl}/auth/verify`);
    retryReq.error(new ProgressEvent('Network error'));
  });

  it('should return user roles from subject', () => {
    service['currentUserSubject'].next(mockUser);
    expect(service.getUserRoles()).toEqual(['admin']);
  });

  it('should return empty roles when no user', () => {
    service['currentUserSubject'].next(null);
    expect(service.getUserRoles()).toEqual([]);
  });

  it('should check permission', () => {
    service['currentUserSubject'].next(mockUser);
    expect(service.hasPermission('patients.view')).toBeTrue();
    expect(service.hasPermission('nonexistent')).toBeFalse();
  });

  it('should check multiple permissions with AND logic', () => {
    service['currentUserSubject'].next(mockUser);
    expect(service.hasPermission(['patients.view', 'patients.write'])).toBeTrue();
    expect(service.hasPermission(['patients.view', 'nonexistent'])).toBeFalse();
  });

  it('should handleHttpError and transform', () => {
    service.login({ username: '', password: '' }).subscribe({
      error: (error: HttpErrorResponse) => {
        expect(error).toBeTruthy();
      },
    });

    const req = httpMock.expectOne(`${environment.apiUrl}/auth/login`);
    req.flush('Unauthorized', { status: 401, statusText: 'Unauthorized' });
  });

  it('should return null from getStoredAccessToken when no token', () => {
    expect(service.getStoredAccessToken()).toBeNull();
  });

  it('should clear stored access token from memory', () => {
    service.storeAccessToken('test-token');
    service.clearStoredAccessToken();
    expect(service.getStoredAccessToken()).toBeNull();
  });

  it('should check hasRole with string array', () => {
    service['currentUserSubject'].next({ ...mockUser, roles: ['admin', 'doctor'] });
    expect(service.hasRole('admin')).toBeTrue();
    expect(service.hasRole('nurse')).toBeFalse();
    expect(service.hasRole(['admin', 'nurse'])).toBeTrue();
  });

  it('should store access token in memory and sessionStorage', () => {
    service.storeAccessToken('new-jwt-token');
    expect(service.getStoredAccessToken()).toBe('new-jwt-token');
    expect(sessionStorage.getItem('hishope_access_token')).toBe('new-jwt-token');
  });

  it('should getCurrentUser', () => {
    service.getCurrentUser().subscribe((user) => {
      expect(user).toBeTruthy();
    });
    const req = httpMock.expectOne(`${environment.apiUrl}/auth/me`);
    expect(req.request.method).toBe('GET');
    req.flush({ id: 'usr-001', username: 'admin' });
  });

  it('should getCurrentUserRoles observable', () => {
    service.getCurrentUserRoles().subscribe((roles) => {
      expect(roles).toEqual([]);
    });
  });

  it('should getUserPermissions from current user when available', () => {
    service['currentUserSubject'].next(mockUser);
    const perms = service.getUserPermissions();
    expect(perms).toEqual(['patients.view', 'patients.write']);
  });

  it('should return empty permissions when no user and no token', () => {
    service['currentUserSubject'].next(null);
    expect(service.getUserPermissions()).toEqual([]);
  });

  it('should handle register error', () => {
    service.register({} as any).subscribe({
      error: (error) => expect(error).toBeTruthy(),
    });
    const req = httpMock.expectOne(`${environment.apiUrl}/auth/register`);
    req.flush('Error', { status: 400, statusText: 'Bad Request' });
  });
});
