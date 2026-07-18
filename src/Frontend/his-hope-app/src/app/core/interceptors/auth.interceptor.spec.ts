import { TestBed } from '@angular/core/testing';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { HTTP_INTERCEPTORS, HttpClient, provideHttpClient, withInterceptorsFromDi } from '@angular/common/http';
import { Router } from '@angular/router';
import { Observable } from 'rxjs';
import { AuthInterceptor } from './auth.interceptor';
import { AuthService } from '@core/services/auth.service';
import { User } from '@core/models/auth.model';

describe('AuthInterceptor', () => {
  let httpClient: HttpClient;
  let httpMock: HttpTestingController;
  let authService: jasmine.SpyObj<AuthService>;
  let router: jasmine.SpyObj<Router>;

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
    const authSpy = jasmine.createSpyObj('AuthService', [
      'getStoredAccessToken',
      'refreshToken',
      'clearStoredAccessToken',
    ]);
    const routerSpy = jasmine.createSpyObj('Router', ['navigate']);

    TestBed.configureTestingModule({
    imports: [],
    providers: [
        { provide: AuthService, useValue: authSpy },
        { provide: Router, useValue: routerSpy },
        {
            provide: HTTP_INTERCEPTORS,
            useClass: AuthInterceptor,
            multi: true,
        },
        provideHttpClient(withInterceptorsFromDi()),
        provideHttpClientTesting(),
    ]
});

    httpClient = TestBed.inject(HttpClient);
    httpMock = TestBed.inject(HttpTestingController);
    authService = TestBed.inject(AuthService) as jasmine.SpyObj<AuthService>;
    router = TestBed.inject(Router) as jasmine.SpyObj<Router>;
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('should attach Authorization Bearer header when token exists', () => {
    authService.getStoredAccessToken.and.returnValue('test-jwt-token');

    httpClient.get('/api/v1/patients').subscribe();

    const req = httpMock.expectOne('/api/v1/patients');
    expect(req.request.headers.has('Authorization')).toBeTrue();
    expect(req.request.headers.get('Authorization')).toBe('Bearer test-jwt-token');
    expect(req.request.headers.has('X-Correlation-ID')).toBeTrue();

    req.flush([]);
  });

  it('should not attach Authorization header for auth login endpoint', () => {
    authService.getStoredAccessToken.and.returnValue('test-jwt-token');

    httpClient.post('/api/v1/auth/login', {}).subscribe();

    const req = httpMock.expectOne('/api/v1/auth/login');
    expect(req.request.headers.has('Authorization')).toBeFalse();

    req.flush({});
  });

  it('should not attach Authorization header when no token is stored', () => {
    authService.getStoredAccessToken.and.returnValue(null);

    httpClient.get('/api/v1/patients').subscribe();

    const req = httpMock.expectOne('/api/v1/patients');
    expect(req.request.headers.has('Authorization')).toBeFalse();

    req.flush([]);
  });

  it('should attempt token refresh on 401 and retry the original request', () => {
    authService.getStoredAccessToken.and.returnValues('expired-token', 'new-token');
    authService.refreshToken.and.returnValue({
      pipe: () => ({
        pipe: () => {},
        subscribe: () => {},
      }),
    } as any);
    // Use a simplified spy approach: make refreshToken return an observable
    authService.refreshToken.and.callFake(() => {
      return new Observable<User>((observer) => {
        observer.next(mockUser);
        observer.complete();
      });
    });

    httpClient.get('/api/v1/patients').subscribe();

    // First request returns 401
    const req = httpMock.expectOne('/api/v1/patients');
    req.flush('Unauthorized', { status: 401, statusText: 'Unauthorized' });

    // After refresh, the retry request should carry the new token
    const retryReq = httpMock.expectOne('/api/v1/patients');
    expect(retryReq.request.headers.get('Authorization')).toBe('Bearer new-token');
    retryReq.flush([]);
  });

  it('should redirect to login when token refresh fails', () => {
    authService.getStoredAccessToken.and.returnValue('expired-token');
    authService.refreshToken.and.callFake(() => {
      return new Observable<never>((observer) => {
        observer.error(new Error('Refresh failed'));
      });
    });

    httpClient.get('/api/v1/patients').subscribe({
      error: () => {
        expect(router.navigate).toHaveBeenCalledWith(['/auth/login']);
      },
    });

    const req = httpMock.expectOne('/api/v1/patients');
    req.flush('Unauthorized', { status: 401, statusText: 'Unauthorized' });
  });

  it('should add X-Correlation-ID header to every request', () => {
    authService.getStoredAccessToken.and.returnValue(null);

    httpClient.get('/api/v1/dashboard').subscribe();

    const req = httpMock.expectOne('/api/v1/dashboard');
    expect(req.request.headers.has('X-Correlation-ID')).toBeTrue();
    const correlationId = req.request.headers.get('X-Correlation-ID');
    expect(correlationId).toMatch(/^hh-/);

    req.flush({});
  });

  it('should not attempt refresh on non-401 errors', () => {
    authService.getStoredAccessToken.and.returnValue('test-token');

    httpClient.get('/api/v1/patients').subscribe({
      error: () => {
        expect(authService.refreshToken).not.toHaveBeenCalled();
      },
    });

    const req = httpMock.expectOne('/api/v1/patients');
    req.flush('Forbidden', { status: 403, statusText: 'Forbidden' });
  });
});
