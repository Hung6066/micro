import { Injectable, inject } from '@angular/core';
import { BehaviorSubject } from 'rxjs';
import { HubConnection, HubConnectionBuilder, LogLevel } from '@microsoft/signalr';
import { CriticalAlert } from '@core/models/critical-alert.model';

export interface LabCriticalAlertConnection {
  start(): Promise<void>;
  stop(): Promise<void>;
  on(eventName: string, callback: (payload: CriticalAlert) => void): void;
  off(eventName: string, callback?: (payload: CriticalAlert) => void): void;
}

@Injectable({ providedIn: 'root' })
export class LabCriticalAlertConnectionFactory {
  create(): LabCriticalAlertConnection {
    const connection = new HubConnectionBuilder()
      .withUrl('/hubs/lab-critical-alerts', { withCredentials: true })
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

  readonly unreadCount$ = new BehaviorSubject<number>(0);
  readonly latestAlert$ = new BehaviorSubject<CriticalAlert | null>(null);

  async connect(): Promise<void> {
    if (this.connection) {
      return;
    }

    this.connection = this.connectionFactory.create();
    this.latestCreatedHandler = (alert: CriticalAlert) => {
      this.latestAlert$.next(alert);
      this.unreadCount$.next(this.unreadCount$.value + 1);
    };

    this.connection.on('criticalAlertCreated', this.latestCreatedHandler);
    await this.connection.start();
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
