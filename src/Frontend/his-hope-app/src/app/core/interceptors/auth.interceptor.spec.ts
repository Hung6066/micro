import { TestBed } from '@angular/core/testing';
import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { AuthService } from '@core/services/auth.service';
import { authInterceptor } from './auth.interceptor';
import { of } from 'rxjs';

describe('authInterceptor', () => {
  let http: HttpClient;
  let httpMock: HttpTestingController;
  let authService: jasmine.SpyObj<AuthService>;

  beforeEach(() => {
    authService = jasmine.createSpyObj<AuthService>('AuthService', [
      'getAccessToken', 'refreshAccessToken', 'handleSessionExpired',
    ]);

    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([authInterceptor])),
        provideHttpClientTesting(),
        { provide: AuthService, useValue: authService },
      ],
    });

    http = TestBed.inject(HttpClient);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('adds bearer authorization to API requests when an access token exists', () => {
    authService.getAccessToken.and.returnValue(of('access-token'));

    http.get('/api/v1/patients/search').subscribe();

    const req = httpMock.expectOne('/api/v1/patients/search');
    expect(req.request.headers.get('Authorization')).toBe('Bearer access-token');
    req.flush({});
  });

  it('does not add authorization when no access token exists', () => {
    authService.getAccessToken.and.returnValue(of(''));

    http.get('/api/v1/patients/search').subscribe();

    const req = httpMock.expectOne('/api/v1/patients/search');
    expect(req.request.headers.has('Authorization')).toBeFalse();
    req.flush({});
  });

  it('does not add authorization to non-API requests', () => {
    authService.getAccessToken.and.returnValue(of('access-token'));

    http.get('/assets/logo.svg').subscribe();

    const req = httpMock.expectOne('/assets/logo.svg');
    expect(req.request.headers.has('Authorization')).toBeFalse();
    req.flush('');
  });
});
