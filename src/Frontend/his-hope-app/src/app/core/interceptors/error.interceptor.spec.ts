import { TestBed, fakeAsync, tick } from '@angular/core/testing';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { HTTP_INTERCEPTORS, HttpClient, provideHttpClient, withInterceptorsFromDi } from '@angular/common/http';
import { Router } from '@angular/router';
import { MatSnackBar } from '@angular/material/snack-bar';
import { NoopAnimationsModule } from '@angular/platform-browser/animations';
import { ErrorInterceptor } from './error.interceptor';
import { ErrorService } from '@core/services/error.service';
import { AuthService } from '@core/services/auth.service';
import { AuditService } from '@core/services/audit.service';

describe('ErrorInterceptor', () => {
  let httpClient: HttpClient;
  let httpMock: HttpTestingController;
  let authService: jasmine.SpyObj<AuthService>;
  let router: jasmine.SpyObj<Router>;
  let snackBar: jasmine.SpyObj<MatSnackBar>;
  let errorService: jasmine.SpyObj<ErrorService>;
  let auditService: jasmine.SpyObj<AuditService>;

  beforeEach(() => {
    const authSpy = jasmine.createSpyObj('AuthService', ['clearStoredAccessToken']);
    const routerSpy = jasmine.createSpyObj('Router', ['navigate']);
    const snackBarSpy = jasmine.createSpyObj('MatSnackBar', ['open']);
    const errorServiceSpy = jasmine.createSpyObj('ErrorService', ['getCorrelationId']);
    const auditSpy = jasmine.createSpyObj('AuditService', ['log']);

    TestBed.configureTestingModule({
    imports: [NoopAnimationsModule],
    providers: [
        { provide: AuthService, useValue: authSpy },
        { provide: Router, useValue: routerSpy },
        { provide: MatSnackBar, useValue: snackBarSpy },
        { provide: ErrorService, useValue: errorServiceSpy },
        { provide: AuditService, useValue: auditSpy },
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
    auditService = TestBed.inject(AuditService) as jasmine.SpyObj<AuditService>;
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

  it('should not audit errors from the audit endpoint', fakeAsync(() => {
    httpClient.post('/api/v1/audit/events', { events: [] }).subscribe({
      error: () => {
        expect(auditService.log).not.toHaveBeenCalled();
        expect(snackBar.open).not.toHaveBeenCalled();
      },
    });

    const req = httpMock.expectOne('/api/v1/audit/events');
    req.flush('Not found', { status: 404, statusText: 'Not Found' });
    tick();
  }));
});
