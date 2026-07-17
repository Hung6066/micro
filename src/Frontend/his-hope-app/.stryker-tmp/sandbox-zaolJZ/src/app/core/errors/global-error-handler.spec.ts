// @ts-nocheck
import { TestBed, fakeAsync, tick, flush } from '@angular/core/testing';
import { ErrorHandler, Injector, NgZone } from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import { MatSnackBar } from '@angular/material/snack-bar';
import { MockStore, provideMockStore } from '@ngrx/store/testing';
import { NoopAnimationsModule } from '@angular/platform-browser/animations';
import { of } from 'rxjs';
import { GlobalErrorHandler } from './global-error-handler';
import { ErrorService } from '@core/services/error.service';
import { HttpClientTestingModule } from '@angular/common/http/testing';

const initialAppState = {
  auth: { user: null, loading: false, error: null },
  patients: { patients: [], selectedPatient: null, totalCount: 0, page: 1, pageSize: 20, query: '', loading: false, error: null },
  error: { message: null, code: null, correlationId: null, timestamp: null },
};

describe('GlobalErrorHandler', () => {
  let errorHandler: GlobalErrorHandler;
  let errorService: jasmine.SpyObj<ErrorService>;
  let snackBar: jasmine.SpyObj<MatSnackBar>;
  let store: MockStore;

  beforeEach(() => {
    const errorServiceSpy = jasmine.createSpyObj('ErrorService', [
      'buildErrorContext',
      'reportError',
    ]);
    const snackBarSpy = jasmine.createSpyObj('MatSnackBar', ['open']);

    TestBed.configureTestingModule({
      imports: [NoopAnimationsModule, HttpClientTestingModule],
      providers: [
        GlobalErrorHandler,
        { provide: ErrorService, useValue: errorServiceSpy },
        { provide: MatSnackBar, useValue: snackBarSpy },
        provideMockStore({ initialState: initialAppState }),
      ],
    });

    errorHandler = TestBed.inject(GlobalErrorHandler);
    errorService = TestBed.inject(ErrorService) as jasmine.SpyObj<ErrorService>;
    snackBar = TestBed.inject(MatSnackBar) as jasmine.SpyObj<MatSnackBar>;
    store = TestBed.inject(MockStore);
  });

  it('should handle HttpErrorResponse and show snackbar', fakeAsync(() => {
    const httpError = new HttpErrorResponse({
      status: 500,
      statusText: 'Internal Server Error',
      url: '/api/test',
    });

    errorService.buildErrorContext.and.returnValue({
      correlationId: 'hh-001',
      message: 'Server error',
      type: 'HTTP_500',
      url: '/api/test',
      timestamp: '2024-01-01T00:00:00.000Z',
    });
    errorService.reportError.and.returnValue(of(void 0));

    errorHandler.handleError(httpError);
    flush();

    expect(errorService.buildErrorContext).toHaveBeenCalledWith(httpError);
    expect(errorService.reportError).toHaveBeenCalled();
  }));

  it('should handle TypeError and show snackbar', fakeAsync(() => {
    const typeError = new TypeError('Cannot read property of undefined');

    errorService.buildErrorContext.and.returnValue({
      correlationId: 'hh-002',
      message: typeError.message,
      type: 'TypeError',
      stack: typeError.stack,
      url: window.location.href,
      timestamp: '2024-01-01T00:00:00.000Z',
    });
    errorService.reportError.and.returnValue(of(void 0));

    errorHandler.handleError(typeError);
    flush();

    expect(errorService.buildErrorContext).toHaveBeenCalledWith(typeError);
    expect(snackBar.open).toHaveBeenCalled();
  }));
});
