import { inject, Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { environment } from '@env/environment';

export type AuditAction =
  | 'auth.login' | 'auth.logout' | 'auth.refresh'
  | 'data.view' | 'data.create' | 'data.update' | 'data.delete'
  | 'error.client' | 'error.server'
  | 'security.csp-violation'
  | 'navigation.change';

export interface AuditEvent {
  action: AuditAction;
  timestamp: number;
  userId?: string;
  details?: Record<string, unknown>;
  correlationId?: string;
  userAgent?: string;
}

/**
 * AuditService — queue audit events in memory and flush to backend in batches.
 *
 * - Events are queued in memory (not localStorage — security)
 * - Flush when batch size (10) reached or after interval (30s)
 * - Failed flushes are re-queued for retry (non-blocking)
 * - KHÔNG throw error nếu backend unavailable (fire-and-forget)
 */
@Injectable({ providedIn: 'root' })
export class AuditService {
  private queue: AuditEvent[] = [];
  private flushTimer: ReturnType<typeof setInterval> | null = null;
  private readonly BATCH_SIZE = 10;
  private readonly FLUSH_INTERVAL = 30000; // 30 seconds
  private readonly ENDPOINT = `${environment.apiUrl}/audit/events`;
  private userId: string | undefined;

  private http = inject(HttpClient);

  /** Set the current user ID for audit events */
  setUserId(id: string | undefined): void {
    this.userId = id;
  }

  /** Log an audit event */
  log(action: AuditAction, details?: Record<string, unknown>): void {
    this.queue.push({
      action,
      timestamp: Date.now(),
      userId: this.userId,
      details,
      userAgent: typeof navigator !== 'undefined' ? navigator.userAgent : undefined,
    });

    if (this.queue.length >= this.BATCH_SIZE) {
      this.flush();
    } else if (!this.flushTimer) {
      this.startFlushTimer();
    }
  }

  /** Public for testing — force flush immediately */
  flushNow(): void {
    this.flush();
  }

  /** Public for testing — check queue size */
  queueLength(): number {
    return this.queue.length;
  }

  private startFlushTimer(): void {
    this.flushTimer = setTimeout(() => this.flush(), this.FLUSH_INTERVAL);
  }

  private flush(): void {
    if (this.queue.length === 0) return;

    const events = [...this.queue];
    this.queue = [];

    this.http.post(this.ENDPOINT, { events }).subscribe({
      next: () => this.scheduleNextFlush(),
      error: () => {
        // Re-queue on failure — non-blocking
        this.queue.unshift(...events);
        this.scheduleNextFlush();
      },
    });
  }

  private scheduleNextFlush(): void {
    if (this.queue.length > 0) {
      this.flushTimer = setTimeout(() => this.flush(), this.FLUSH_INTERVAL);
    } else {
      this.flushTimer = null;
    }
  }
}
