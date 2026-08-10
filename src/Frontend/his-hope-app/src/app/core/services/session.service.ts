import { inject, Injectable, NgZone } from '@angular/core';
import { Router } from '@angular/router';
import { AuthService } from './auth.service';
import { BehaviorSubject, Observable, fromEvent, merge, Subscription } from 'rxjs';
import { throttleTime } from 'rxjs/operators';

export interface SessionConfig {
  idleTimeoutMs: number;
  absoluteExpiryMs: number;
  warningBeforeMs: number;
}

export const DEFAULT_SESSION_CONFIG: SessionConfig = {
  idleTimeoutMs: 15 * 60 * 1000,       // 15 phút
  absoluteExpiryMs: 8 * 60 * 60 * 1000, // 8 giờ
  warningBeforeMs: 60 * 1000,           // 60 giây
};

@Injectable({ providedIn: 'root' })
export class SessionService {
  private config: SessionConfig = { ...DEFAULT_SESSION_CONFIG };
  private warningTimer: ReturnType<typeof setTimeout> | null = null;
  private expiryTimer: ReturnType<typeof setTimeout> | null = null;
  private absoluteTimer: ReturnType<typeof setTimeout> | null = null;
  private sessionStart = 0;
  private lastActivity = 0;
  private tracking = false;
  private activitySub: Subscription | null = null;

  private remainingTimeSubject = new BehaviorSubject<number>(0);
  private warningSubject = new BehaviorSubject<boolean>(false);
  private expiredSubject = new BehaviorSubject<void>(undefined);

  /** Observable countdown đến khi force logout */
  remainingTime$: Observable<number> = this.remainingTimeSubject.asObservable();

  /** Emit true khi đang trong warning period (60s cuối) */
  isWarning$: Observable<boolean> = this.warningSubject.asObservable();

  /** Emit khi session expired */
  onExpired$: Observable<void> = this.expiredSubject.asObservable();

  private authService = inject(AuthService);
  private router = inject(Router);
  private ngZone = inject(NgZone);

  constructor() {}

  /** Override default session config (must call before startTracking) */
  configure(partial: Partial<SessionConfig>): void {
    this.config = { ...this.config, ...partial };
  }

  /** Bắt đầu theo dõi session. Gọi sau login thành công. */
  startTracking(): void {
    if (this.tracking) return;
    this.tracking = true;
    this.sessionStart = Date.now();
    this.lastActivity = Date.now();
    this.remainingTimeSubject.next(this.config.idleTimeoutMs);

    // Lắng nghe user interaction — chạy ngoài Angular zone để tránh trigger CD
    this.ngZone.runOutsideAngular(() => {
      this.activitySub = merge(
        fromEvent(document, 'mousemove'),
        fromEvent(document, 'keydown'),
        fromEvent(document, 'click'),
        fromEvent(document, 'touchstart'),
        fromEvent(document, 'scroll'),
        fromEvent(document, 'wheel'),
      ).pipe(
        throttleTime(1000),
      ).subscribe(() => this.ngZone.run(() => this.resetIdleTimer()));
    });

    this.setAbsoluteTimer();
    this.setIdleTimers();
  }

  /** Dừng theo dõi session. Gọi khi logout. */
  stopTracking(): void {
    this.tracking = false;
    this.activitySub?.unsubscribe();
    this.clearAllTimers();
    this.remainingTimeSubject.next(0);
    this.warningSubject.next(false);
  }

  /** Reset idle timer khi user tương tác. */
  resetIdleTimer(): void {
    if (!this.tracking) return;
    this.lastActivity = Date.now();
    this.warningSubject.next(false);
    this.clearIdleTimers();
    this.setIdleTimers();
  }

  /** Lấy thời gian còn lại trước khi idle timeout (ms). */
  getRemainingMs(): number {
    if (!this.tracking) return 0;
    const elapsed = Date.now() - this.lastActivity;
    return Math.max(0, this.config.idleTimeoutMs - elapsed);
  }

  // ── Private ─────────────────────────────────────────────────

  private setIdleTimers(): void {
    const warningAt = this.config.idleTimeoutMs - this.config.warningBeforeMs;

    // Timer warning: bật cảnh báo trước khi hết hạn
    this.warningTimer = setTimeout(() => {
      this.warningSubject.next(true);
    }, warningAt);

    // Timer expiry: force logout khi hết idle timeout
    this.expiryTimer = setTimeout(() => {
      if (Date.now() - this.lastActivity >= this.config.idleTimeoutMs) {
        this.expiredSubject.next(undefined);
        this.forceLogout('idle_timeout');
      }
    }, this.config.idleTimeoutMs);
  }

  private setAbsoluteTimer(): void {
    this.absoluteTimer = setTimeout(() => {
      this.expiredSubject.next(undefined);
      this.forceLogout('absolute_expiry');
    }, this.config.absoluteExpiryMs);
  }

  private clearIdleTimers(): void {
    if (this.warningTimer) clearTimeout(this.warningTimer);
    if (this.expiryTimer) clearTimeout(this.expiryTimer);
    this.warningTimer = null;
    this.expiryTimer = null;
  }

  private clearAllTimers(): void {
    this.clearIdleTimers();
    if (this.absoluteTimer) clearTimeout(this.absoluteTimer);
    this.absoluteTimer = null;
  }

  private forceLogout(reason: 'idle_timeout' | 'absolute_expiry'): void {
    this.stopTracking();
    this.authService.oidcLogout();
    this.router.navigate(['/auth/login'], {
      queryParams: { reason },
    });
  }
}
