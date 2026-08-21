import { Injectable, NgZone, OnDestroy, inject, signal } from "@angular/core";
import { HIS_HOPE_APP_PIN, HIS_HOPE_BIOMETRIC } from "../tokens";

const DEFAULT_IDLE_MS = 5 * 60 * 1000;
const BACKGROUND_GRACE_MS = 30 * 1000;
const ACTIVITY_EVENTS = ["pointerdown", "keydown", "touchstart"] as const;

/** Idle/background lock requiring biometric or app-PIN re-authentication. */
@Injectable({ providedIn: "root" })
export class HisHopeMobileLockService implements OnDestroy {
  private readonly biometric = inject(HIS_HOPE_BIOMETRIC);
  private readonly appPin = inject(HIS_HOPE_APP_PIN);
  private readonly zone = inject(NgZone);
  readonly locked = signal(false);

  private armed = false;
  private idleMs = DEFAULT_IDLE_MS;
  private idleTimer?: ReturnType<typeof setTimeout>;
  private backgroundedAt: number | null = null;
  private readonly boundResetIdle = (): void => this.resetIdleTimer();
  private readonly boundVisibilityChange = (): void =>
    this.onVisibilityChange();

  arm(idleMs = DEFAULT_IDLE_MS): void {
    if (this.armed || typeof document === "undefined") return;
    this.armed = true;
    this.idleMs = idleMs;
    this.zone.runOutsideAngular(() => {
      ACTIVITY_EVENTS.forEach((event) =>
        document.addEventListener(event, this.boundResetIdle, {
          passive: true,
        }),
      );
      document.addEventListener("visibilitychange", this.boundVisibilityChange);
    });
    this.resetIdleTimer();
  }

  disarm(): void {
    if (!this.armed) return;
    this.armed = false;
    ACTIVITY_EVENTS.forEach((event) =>
      document.removeEventListener(event, this.boundResetIdle),
    );
    document.removeEventListener(
      "visibilitychange",
      this.boundVisibilityChange,
    );
    if (this.idleTimer) clearTimeout(this.idleTimer);
    this.locked.set(false);
  }

  async unlock(reason = "Unlock"): Promise<boolean> {
    const ok = await this.biometric.authenticate(reason);
    if (ok) {
      this.locked.set(false);
      this.resetIdleTimer();
    }
    return ok;
  }

  async unlockWithPin(pin: string): Promise<boolean> {
    const ok = await this.appPin.verifyPin(pin);
    if (ok) {
      this.locked.set(false);
      this.resetIdleTimer();
    }
    return ok;
  }

  pinConfigured(): Promise<boolean> {
    return this.appPin.isConfigured();
  }

  private resetIdleTimer(): void {
    if (!this.armed || this.locked()) return;
    if (this.idleTimer) clearTimeout(this.idleTimer);
    this.idleTimer = setTimeout(
      () => this.zone.run(() => this.locked.set(true)),
      this.idleMs,
    );
  }

  private onVisibilityChange(): void {
    if (document.hidden) {
      this.backgroundedAt = Date.now();
      return;
    }
    const wasBackgroundedTooLong =
      !!this.backgroundedAt &&
      Date.now() - this.backgroundedAt > BACKGROUND_GRACE_MS;
    this.backgroundedAt = null;
    if (wasBackgroundedTooLong) {
      this.zone.run(() => this.locked.set(true));
      return;
    }
    this.resetIdleTimer();
  }

  ngOnDestroy(): void {
    this.disarm();
  }
}
