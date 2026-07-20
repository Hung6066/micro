import { TestBed } from '@angular/core/testing';
import { BehaviorSubject } from 'rxjs';
import { LabCriticalAlertStreamService, LabCriticalAlertConnectionFactory } from './lab-critical-alert-stream.service';
import { AuthService } from './auth.service';

let lastBuilderInstance: { lastAccessTokenFactory?: () => string } | null = null;

jest.mock('@microsoft/signalr', () => {
  class HubConnectionBuilder {
    lastAccessTokenFactory?: () => string;

    withUrl(_url: string, options: { accessTokenFactory: () => string }) {
      this.lastAccessTokenFactory = options.accessTokenFactory;
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
    authService.getStoredAccessToken.and.returnValue('token-123');

    TestBed.configureTestingModule({
      providers: [
        LabCriticalAlertStreamService,
        { provide: LabCriticalAlertConnectionFactory, useValue: factory },
        { provide: AuthService, useValue: authService },
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

  it('should read the latest stored token each time SignalR asks for credentials', () => {
    const connectionFactory = TestBed.runInInjectionContext(() => new LabCriticalAlertConnectionFactory());
    const authTokens = ['token-1', 'token-2'];
    authService.getStoredAccessToken.and.callFake(() => authTokens.shift() ?? 'token-final');

    connectionFactory.create();

    expect(lastBuilderInstance?.lastAccessTokenFactory).toBeDefined();
    expect(lastBuilderInstance?.lastAccessTokenFactory?.()).toBe('token-1');
    expect(lastBuilderInstance?.lastAccessTokenFactory?.()).toBe('token-2');
    expect(authService.getStoredAccessToken).toHaveBeenCalledTimes(2);
  });
});
