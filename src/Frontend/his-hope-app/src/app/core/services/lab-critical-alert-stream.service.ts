import { Injectable, inject } from '@angular/core';
import { BehaviorSubject, firstValueFrom } from 'rxjs';
import { HubConnection, HubConnectionBuilder, LogLevel } from '@microsoft/signalr';
import { CriticalAlert } from '@core/models/critical-alert.model';
import { AuthService } from './auth.service';

export interface LabCriticalAlertConnection {
  start(): Promise<void>;
  stop(): Promise<void>;
  on(eventName: string, callback: (payload: CriticalAlert) => void): void;
  off(eventName: string, callback?: (payload: CriticalAlert) => void): void;
}

@Injectable({ providedIn: 'root' })
export class LabCriticalAlertConnectionFactory {
  private readonly authService = inject(AuthService);

  create(): LabCriticalAlertConnection {
    const connection = new HubConnectionBuilder()
      .withUrl('/hubs/lab-critical-alerts', {
        accessTokenFactory: () => firstValueFrom(this.authService.getAccessToken()),
        withCredentials: true,
      })
      .withAutomaticReconnect()
      .configureLogging(LogLevel.Warning)
      .build();

    return connection as HubConnection;
  }
}

@Injectable({ providedIn: 'root' })
export class LabCriticalAlertStreamService {
  private readonly connectionFactory = inject(LabCriticalAlertConnectionFactory);
  private connection?: LabCriticalAlertConnection;
  private latestCreatedHandler?: (payload: CriticalAlert) => void;
  private connectInFlight?: Promise<void>;

  readonly unreadCount$ = new BehaviorSubject<number>(0);
  readonly latestAlert$ = new BehaviorSubject<CriticalAlert | null>(null);

  async connect(): Promise<void> {
    if (this.connection || this.connectInFlight) {
      return this.connectInFlight;
    }

    this.connectInFlight = this.startWithRetry();
    try {
      await this.connectInFlight;
    } finally {
      this.connectInFlight = undefined;
    }
  }

  private async startWithRetry(): Promise<void> {
    let lastError: unknown;

    for (let attempt = 0; attempt < 2; attempt++) {
      const connection = this.connectionFactory.create();
      const handler = (alert: CriticalAlert) => {
        this.latestAlert$.next(alert);
        this.unreadCount$.next(this.unreadCount$.value + 1);
      };
      this.connection = connection;
      this.latestCreatedHandler = handler;
      connection.on('criticalAlertCreated', handler);

      try {
        await connection.start();
        return;
      } catch (error) {
        lastError = error;
        connection.off('criticalAlertCreated', handler);
        await connection.stop().catch(() => undefined);
        if (this.connection === connection) {
          this.connection = undefined;
          this.latestCreatedHandler = undefined;
        }
        if (attempt === 0) {
          await new Promise(resolve => setTimeout(resolve, 500));
        }
      }
    }

    throw lastError;
  }

  async disconnect(): Promise<void> {
    if (!this.connection) {
      this.unreadCount$.next(0);
      this.latestAlert$.next(null);
      return;
    }

    if (this.latestCreatedHandler) {
      this.connection.off('criticalAlertCreated', this.latestCreatedHandler);
    }

    await this.connection.stop();
    this.connection = undefined;
    this.latestCreatedHandler = undefined;
    this.unreadCount$.next(0);
    this.latestAlert$.next(null);
  }

  markAllRead(): void {
    this.unreadCount$.next(0);
  }
}
