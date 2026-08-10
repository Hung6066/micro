import { TestBed } from '@angular/core/testing';
import { LabCriticalAlertStreamService, LabCriticalAlertConnectionFactory } from './lab-critical-alert-stream.service';
import { AuthService } from './auth.service';

let lastBuilderInstance: {
  lastUrl?: string;
  lastOptions?: { accessTokenFactory?: () => string; withCredentials?: boolean };
} | null = null;

jest.mock('@microsoft/signalr', () => {
  class HubConnectionBuilder {
    lastUrl?: string;
    lastOptions?: { accessTokenFactory?: () => string; withCredentials?: boolean };

    withUrl(url: string, options: { accessTokenFactory?: () => string; withCredentials?: boolean }) {
      this.lastUrl = url;
      this.lastOptions = options;
      lastBuilderInstance = this;
      return this;
    }

    withAutomaticReconnect() {
      return this;
    }

    configureLogging() {
      return this;
    }

    build() {
      return {} as never;
    }
  }

  return {
    HubConnectionBuilder,
    LogLevel: { Warning: 'Warning' },
  };
});

describe('LabCriticalAlertStreamService', () => {
  let service: LabCriticalAlertStreamService;
  let fakeConnection: {
    start: jasmine.Spy;
    stop: jasmine.Spy;
    on: jasmine.Spy;
    off: jasmine.Spy;
  };
  let handlers: Record<string, (payload: any) => void>;
  let factory: jasmine.SpyObj<LabCriticalAlertConnectionFactory>;
  let authService: jasmine.SpyObj<AuthService>;

  beforeEach(() => {
    lastBuilderInstance = null;
    handlers = {};
    fakeConnection = {
      start: jasmine.createSpy('start').and.returnValue(Promise.resolve()),
      stop: jasmine.createSpy('stop').and.returnValue(Promise.resolve()),
      on: jasmine.createSpy('on').and.callFake((event: string, handler: (payload: any) => void) => {
        handlers[event] = handler;
      }),
      off: jasmine.createSpy('off'),
    };

    factory = jasmine.createSpyObj<LabCriticalAlertConnectionFactory>('LabCriticalAlertConnectionFactory', ['create']);
    factory.create.and.returnValue(fakeConnection as any);
    authService = jasmine.createSpyObj<AuthService>('AuthService', ['getStoredAccessToken']);
    authService.getStoredAccessToken.and.returnValue('jwt-token');
    TestBed.configureTestingModule({
      providers: [
        LabCriticalAlertStreamService,
        { provide: AuthService, useValue: authService },
        { provide: LabCriticalAlertConnectionFactory, useValue: factory },
      ],
    });

    service = TestBed.inject(LabCriticalAlertStreamService);
  });

  it('should connect with the stored access token and increment unread count on criticalAlertCreated', async () => {
    const unreadValues: number[] = [];
    service.unreadCount$.subscribe((value) => unreadValues.push(value));

    await service.connect();

    expect(factory.create).toHaveBeenCalledTimes(1);
    expect(fakeConnection.start).toHaveBeenCalled();

    handlers['criticalAlertCreated']({ id: 'alert-1' });

    expect(unreadValues[unreadValues.length - 1]).toBe(1);
    expect(service.latestAlert$.value?.id).toBe('alert-1');
  });

  it('should reset unread state when disconnected', async () => {
    service.unreadCount$.subscribe();

    await service.connect();
    handlers['criticalAlertCreated']({ id: 'alert-1' });
    await service.disconnect();

    expect(fakeConnection.stop).toHaveBeenCalled();
    expect(service.unreadCount$.value).toBe(0);
    expect(service.latestAlert$.value).toBeNull();
  });

  it('should create a SignalR connection using browser credentials', () => {
    const connectionFactory = TestBed.runInInjectionContext(() => new LabCriticalAlertConnectionFactory());

    connectionFactory.create();

    expect(lastBuilderInstance?.lastUrl).toBe('/hubs/lab-critical-alerts');
    expect(lastBuilderInstance?.lastOptions?.withCredentials).toBeTrue();
    expect(lastBuilderInstance?.lastOptions?.accessTokenFactory?.()).toBe('jwt-token');
  });
});
