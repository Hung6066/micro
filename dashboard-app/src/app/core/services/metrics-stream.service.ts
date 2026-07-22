import { Injectable, OnDestroy } from '@angular/core';
import * as signalR from '@microsoft/signalr';
import { Subject, Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { LiveMetricUpdate } from '../models/live-metric-update.model';

@Injectable({ providedIn: 'root' })
export class MetricsStreamService implements OnDestroy {
  private connection!: signalR.HubConnection;
  private metricsSubject = new Subject<LiveMetricUpdate>();
  private isConnected = false;

  readonly liveMetrics$: Observable<LiveMetricUpdate> = this.metricsSubject.asObservable();

  constructor() {
    this.connection = new signalR.HubConnectionBuilder()
      .withUrl(`${environment.wsUrl}/metricshub`, {
        accessTokenFactory: () => localStorage.getItem('access_token') ?? ''
      })
      .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
      .configureLogging(signalR.LogLevel.Warning)
      .build();

    this.connection.on('MetricUpdate', (update: LiveMetricUpdate) => {
      this.metricsSubject.next(update);
    });

    this.connection.onreconnecting(() => {
      console.warn('Metrics stream reconnecting...');
    });

    this.connection.onreconnected(() => {
      console.info('Metrics stream reconnected');
    });

    this.connection.onclose(() => {
      this.isConnected = false;
    });
  }

  async connect(): Promise<void> {
    if (this.isConnected) return;
    try {
      await this.connection.start();
      this.isConnected = true;
    } catch (err) {
      console.error('Failed to connect to metrics stream:', err);
    }
  }

  /** Subscribe to live metrics for a single service (e.g. "identity-service"). */
  async subscribe(serviceName: string): Promise<void> {
    await this.ensureConnected();
    await this.connection.invoke('SubscribeMetrics', serviceName);
  }

  /** Unsubscribe from live metrics for a service. */
  async unsubscribe(serviceName: string): Promise<void> {
    await this.ensureConnected();
    await this.connection.invoke('UnsubscribeMetrics', serviceName);
  }

  /** Subscribe to multiple services at once. */
  async subscribeMany(serviceNames: string[]): Promise<void> {
    await this.ensureConnected();
    for (const name of serviceNames) {
      await this.connection.invoke('SubscribeMetrics', name);
    }
  }

  /** Unsubscribe from multiple services. */
  async unsubscribeMany(serviceNames: string[]): Promise<void> {
    await this.ensureConnected();
    for (const name of serviceNames) {
      await this.connection.invoke('UnsubscribeMetrics', name);
    }
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
    this.metricsSubject.complete();
  }
}
