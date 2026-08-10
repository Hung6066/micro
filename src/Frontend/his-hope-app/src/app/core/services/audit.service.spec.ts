import { TestBed } from '@angular/core/testing';
import { HttpClientTestingModule, HttpTestingController } from '@angular/common/http/testing';
import { AuditService, AuditAction } from './audit.service';
import { environment } from '@env/environment';

describe('AuditService', () => {
  let service: AuditService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [HttpClientTestingModule],
      providers: [AuditService],
    });
    service = TestBed.inject(AuditService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
    service['flushTimer'] = null; // reset timer
  });

  it('should queue audit events', () => {
    service.log('auth.login', { success: true });
    expect(service.queueLength()).toBe(1);
  });

  it('should flush events and clear queue', () => {
    service.log('auth.login', { success: true });
    service.flushNow();

    const req = httpMock.expectOne(`${environment.apiUrl}/audit/events`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body.events.length).toBe(1);
    expect(req.request.body.events[0].action).toBe('auth.login');
    req.flush({});

    expect(service.queueLength()).toBe(0);
  });

  it('should batch multiple events', () => {
    for (let i = 0; i < 5; i++) {
      service.log('navigation.change', { to: `/page-${i}` });
    }
    expect(service.queueLength()).toBe(5);

    service.flushNow();
    const req = httpMock.expectOne(`${environment.apiUrl}/audit/events`);
    expect(req.request.body.events.length).toBe(5);
    req.flush({});
  });

  it('should not throw when flush fails', () => {
    service.log('data.view', { patientId: '123' });
    service.flushNow();

    const req = httpMock.expectOne(`${environment.apiUrl}/audit/events`);
    req.flush('Server error', { status: 500, statusText: 'Server Error' });

    // After failed flush, event should be re-queued
    expect(service.queueLength()).toBe(1);
  });

  it('should drop non-retryable audit endpoint failures', () => {
    service.log('data.view', { patientId: '123' });
    service.flushNow();

    const req = httpMock.expectOne(`${environment.apiUrl}/audit/events`);
    req.flush('Not found', { status: 404, statusText: 'Not Found' });

    expect(service.queueLength()).toBe(0);
  });

  it('should use setUserId in events', () => {
    service.setUserId('usr-001');
    service.log('data.view', {});

    service.flushNow();
    const req = httpMock.expectOne(`${environment.apiUrl}/audit/events`);
    expect(req.request.body.events[0].userId).toBe('usr-001');
    req.flush({});
  });

  it('should not flush empty queue', () => {
    service.flushNow();
    httpMock.expectNone(`${environment.apiUrl}/audit/events`);
  });
});
