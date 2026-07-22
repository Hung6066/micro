import { Injectable, OnDestroy, NgZone } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, BehaviorSubject, interval, Subject } from 'rxjs';
import { switchMap, tap, catchError, map, shareReplay, takeUntil, filter, startWith } from 'rxjs/operators';
import { environment } from '../../../environments/environment';
import { Alert } from '../models/alert.model';
import * as signalR from '@microsoft/signalr';

@Injectable({ providedIn: 'root' })
export class AlertService implements OnDestroy {
  private readonly baseUrl = `${environment.apiUrl}/alerts`;
  private readonly wsUrl = `${environment.wsUrl}/alerthub`;

  private readonly activeAlertsSubject = new BehaviorSubject<Alert[]>([]);
  private readonly newCriticalAlertSubject = new Subject<Alert>();
  private readonly destroy$ = new Subject<void>();

  /** All active alerts, updated every 15s */
  readonly activeAlerts$: Observable<Alert[]> = this.activeAlertsSubject.asObservable();

  /** Emits when a new critical or warning alert is first seen */
  readonly newCriticalAlert$: Observable<Alert> = this.newCriticalAlertSubject.asObservable();

  /** Counts of each severity */
  readonly criticalCount$: Observable<number>;
  readonly warningCount$: Observable<number>;

  private previousAlertKeys = new Set<string>();
  private hubConnection?: signalR.HubConnection;

  constructor(private readonly http: HttpClient, private readonly zone: NgZone) {
    // Start polling
    interval(15_000)
      .pipe(
        startWith(0),
        switchMap(() => this.fetchAlerts()),
        tap(alerts => this.detectNewAlerts(alerts)),
        catchError(() => []),
        takeUntil(this.destroy$),
      )
      .subscribe(alerts => this.activeAlertsSubject.next(alerts));

    // Derived counts
    this.criticalCount$ = this.activeAlerts$.pipe(
      map(alerts => alerts.filter(a => a.severity === 'critical' && a.status === 'firing').length),
    );

    this.warningCount$ = this.activeAlerts$.pipe(
      map(alerts => alerts.filter(a => a.severity === 'warning' && a.status === 'firing').length),
    );

    // Optionally connect via SignalR for real-time updates
    this.tryConnectSignalR();
  }

  private fetchAlerts(): Observable<Alert[]> {
    return this.http.get<Alert[]>(this.baseUrl).pipe(
      map(alerts =>
        alerts.map(a => ({
          ...a,
          startsAt: new Date(a.startsAt),
          endsAt: a.endsAt ? new Date(a.endsAt) : undefined,
        })),
      ),
    );
  }

  private detectNewAlerts(alerts: Alert[]): void {
    const currentKeys = new Set(alerts.map(a => a.name));

    for (const alert of alerts) {
      if (
        !this.previousAlertKeys.has(alert.name) &&
        alert.status === 'firing' &&
        (alert.severity === 'critical' || alert.severity === 'warning')
      ) {
        this.newCriticalAlertSubject.next(alert);
      }
    }

    this.previousAlertKeys = currentKeys;
  }

  private tryConnectSignalR(): void {
    try {
      this.hubConnection = new signalR.HubConnectionBuilder()
        .withUrl(this.wsUrl)
        .withAutomaticReconnect()
        .build();

      this.hubConnection.on('AlertUpdate', (alert: Alert) => {
        this.zone.run(() => {
          const current = this.activeAlertsSubject.value;
          const idx = current.findIndex(a => a.name === alert.name);
          if (idx >= 0) {
            current[idx] = alert;
          } else {
            current.push(alert);
          }
          this.activeAlertsSubject.next([...current]);
          this.detectNewAlerts([alert]);
        });
      });

      this.hubConnection.on('AlertCleared', (alertName: string) => {
        this.zone.run(() => {
          const filtered = this.activeAlertsSubject.value.filter(a => a.name !== alertName);
          this.activeAlertsSubject.next(filtered);
        });
      });

      this.hubConnection.start().catch(() => {
        // SignalR is optional; polling handles updates
      });
    } catch {
      // SignalR connection failure is non-fatal
    }
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
    this.hubConnection?.stop();
  }
}
