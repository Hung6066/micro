import { Injectable, OnDestroy } from '@angular/core';
import * as signalR from '@microsoft/signalr';
import { Subject, Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { LogEntry } from '../models/log-entry.model';

@Injectable({ providedIn: 'root' })
export class LogStreamService implements OnDestroy {
  private connection!: signalR.HubConnection;
  private logSubject = new Subject<LogEntry>();
  private isConnected = false;

  readonly logs$: Observable<LogEntry> = this.logSubject.asObservable();

  constructor() {
    this.connection = new signalR.HubConnectionBuilder()
      .withUrl(`${environment.wsUrl}/logshub`, {
        accessTokenFactory: () => localStorage.getItem('access_token') ?? ''
      })
      .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
      .configureLogging(signalR.LogLevel.Warning)
      .build();

    this.connection.on('LogEntry', (entry: LogEntry) => {
      this.logSubject.next(entry);
    });
  }

  async connect(): Promise<void> {
    if (this.isConnected) return;
    try {
      await this.connection.start();
      this.isConnected = true;
    } catch (err) {
      console.error('Failed to connect to log stream:', err);
    }
  }

  async subscribe(service?: string, level?: string): Promise<void> {
    await this.ensureConnected();
    await this.connection.invoke('Subscribe', service ?? '*', level ?? '*');
  }

  async unsubscribe(service?: string, level?: string): Promise<void> {
    await this.ensureConnected();
    await this.connection.invoke('Unsubscribe', service ?? '*', level ?? '*');
  }

  async disconnect(): Promise<void> {
    if (!this.isConnected) return;
    await this.connection.stop();
    this.isConnected = false;
  }

  private async ensureConnected(): Promise<void> {
    if (!this.isConnected) {
      await this.connect();
    }
  }

  ngOnDestroy(): void {
    this.disconnect();
    this.logSubject.complete();
  }
}
