import { TestBed, fakeAsync, tick } from '@angular/core/testing';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { HTTP_INTERCEPTORS, HttpClient, provideHttpClient, withInterceptorsFromDi } from '@angular/common/http';
import { Router } from '@angular/router';
import { MatSnackBar } from '@angular/material/snack-bar';
import { NoopAnimationsModule } from '@angular/platform-browser/animations';
import { ErrorInterceptor } from './error.interceptor';
import { ErrorService } from '@core/services/error.service';
import { AuthService } from '@core/services/auth.service';

describe('ErrorInterceptor', () => {
  let httpClient: HttpClient;
  let httpMock: HttpTestingController;
  let authService: jasmine.SpyObj<AuthService>;
  let router: jasmine.SpyObj<Router>;
  let snackBar: jasmine.SpyObj<MatSnackBar>;
  let errorService: jasmine.SpyObj<ErrorService>;

  beforeEach(() => {
    const authSpy = jasmine.createSpyObj('AuthService', ['clearStoredAccessToken']);
    const routerSpy = jasmine.createSpyObj('Router', ['navigate']);
    const snackBarSpy = jasmine.createSpyObj('MatSnackBar', ['open']);
    const errorServiceSpy = jasmine.createSpyObj('ErrorService', ['getCorrelationId']);

    TestBed.configureTestingModule({
    imports: [NoopAnimationsModule],
    providers: [
        { provide: AuthService, useValue: authSpy },
        { provide: Router, useValue: routerSpy },
        { provide: MatSnackBar, useValue: snackBarSpy },
        { provide: ErrorService, useValue: errorServiceSpy },
        { provide: HTTP_INTERCEPTORS, useClass: ErrorInterceptor, multi: true },
        provideHttpClient(withInterceptorsFromDi()),
        provideHttpClientTesting(),
    ]
});

    httpClient = TestBed.inject(HttpClient);
    httpMock = TestBed.inject(HttpTestingController);
    authService = TestBed.inject(AuthService) as jasmine.SpyObj<AuthService>;
    router = TestBed.inject(Router) as jasmine.SpyObj<Router>;
    snackBar = TestBed.inject(MatSnackBar) as jasmine.SpyObj<MatSnackBar>;
    errorService = TestBed.inject(ErrorService) as jasmine.SpyObj<ErrorService>;
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('should retry on transient error (503)', fakeAsync(() => {
    errorService.getCorrelationId.and.returnValue('hh-correlation-id');

    httpClient.get('/api/v1/test').subscribe({
      error: () => {
        expect(snackBar.open).toHaveBeenCalled();
      },
    });

    const req1 = httpMock.expectOne('/api/v1/test');
    req1.flush('Service Unavailable', { status: 503, statusText: 'Service Unavailable' });

    const req2 = httpMock.expectOne('/api/v1/test');
    req2.flush('Service Unavailable', { status: 503, statusText: 'Service Unavailable' });
    tick();
  }));

  it('should redirect to login on 401 for non-auth URLs', fakeAsync(() => {
    errorService.getCorrelationId.and.returnValue('hh-id');

    httpClient.get('/api/v1/patients').subscribe({
      error: () => {
        expect(authService.clearStoredAccessToken).toHaveBeenCalled();
        expect(router.navigate).toHaveBeenCalledWith(['/auth/login']);
      },
    });

    const req = httpMock.expectOne('/api/v1/patients');
    req.flush('Unauthorized', { status: 401, statusText: 'Unauthorized' });
    tick();
  }));

  it('should show notification for 403 access denied', fakeAsync(() => {
    errorService.getCorrelationId.and.returnValue('hh-id');

    httpClient.get('/api/v1/patients').subscribe({
      error: () => {
        expect(snackBar.open).toHaveBeenCalledWith(
          jasmine.any(String),
          'Close',
          jasmine.any(Object),
        );
      },
    });

    const req = httpMock.expectOne('/api/v1/patients');
    req.flush('Forbidden', { status: 403, statusText: 'Forbidden' });
    tick();
  }));
});
