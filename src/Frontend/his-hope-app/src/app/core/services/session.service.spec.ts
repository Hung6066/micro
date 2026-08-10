import { TestBed } from '@angular/core/testing';
import { fakeAsync, tick, discardPeriodicTasks } from '@angular/core/testing';
import { Router } from '@angular/router';
import { provideHttpClient, withInterceptorsFromDi } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { SessionService, DEFAULT_SESSION_CONFIG } from './session.service';

describe('SessionService', () => {
  let service: SessionService;

  beforeEach(() => {
    const routerSpy = jasmine.createSpyObj('Router', ['navigate']);

    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptorsFromDi()),
        provideHttpClientTesting(),
        { provide: Router, useValue: routerSpy },
      ],
    });
    service = TestBed.inject(SessionService);
    service.configure({
      idleTimeoutMs: 10000,
      absoluteExpiryMs: 60000,
      warningBeforeMs: 2000,
    });
  });

  afterEach(() => {
    service.stopTracking();
  });

  it('should start with remaining time equal to idleTimeout', fakeAsync(() => {
    service.startTracking();
    expect(service.getRemainingMs()).toBe(10000);
  }));

  it('should decrease remaining time over time', fakeAsync(() => {
    service.startTracking();
    tick(1000);
    expect(service.getRemainingMs()).toBe(9000);
    discardPeriodicTasks();
  }));

  it('should reset idle timer on resetIdleTimer call', fakeAsync(() => {
    service.startTracking();
    tick(3000);
    service.resetIdleTimer();
    expect(service.getRemainingMs()).toBe(10000);
    discardPeriodicTasks();
  }));

  it('should emit isWarning when entering warning period', fakeAsync(() => {
    const warningValues: boolean[] = [];
    service.startTracking();
    service.isWarning$.subscribe((v) => warningValues.push(v));

    tick(8001); // past warning threshold (10000 - 2000 = 8000)
    expect(warningValues).toContain(true);
    discardPeriodicTasks();
  }));

  it('should emit expired event when idle timeout reached', fakeAsync(() => {
    let expired = false;
    service.startTracking();
    service.onExpired$.subscribe(() => (expired = true));

    tick(10001); // past idle timeout
    expect(expired).toBeTrue();
    discardPeriodicTasks();
  }));

  it('should stop timers on stopTracking', fakeAsync(() => {
    service.startTracking();
    service.stopTracking();
    tick(11000);
    expect(service.getRemainingMs()).toBe(0);
  }));

  it('should not restart tracking if already tracking', fakeAsync(() => {
    service.startTracking();
    tick(0);
    const remaining1 = service.getRemainingMs();
    service.startTracking(); // second call should be no-op
    expect(service.getRemainingMs()).toBe(remaining1);
  }));

  it('should return 0 remaining time when not tracking', () => {
    expect(service.getRemainingMs()).toBe(0);
  });
});
