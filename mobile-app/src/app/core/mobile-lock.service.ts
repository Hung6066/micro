import { Injectable, NgZone, OnDestroy, inject, signal } from "@angular/core";
import { NativeCapabilityService } from "./native-capability.service";

const DEFAULT_IDLE_MS = 5 * 60 * 1000;
const BACKGROUND_GRACE_MS = 30 * 1000;
const ACTIVITY_EVENTS = ["pointerdown", "keydown", "touchstart"] as const;

/** Locks the admin workspace after idle time or after the app has spent more
 *  than a short grace period in the background, requiring biometric
 *  re-authentication before the UI is usable again. Pure TS/DOM \u2014 no native
 *  plugin required. */
@Injectable({ providedIn: "root" })
export class MobileLockService implements OnDestroy {
  private readonly native = inject(NativeCapabilityService);
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

  /** Attempts biometric unlock; returns false if unavailable or declined so
   *  the caller can fall back to a full re-login. */
  async unlock(): Promise<boolean> {
    // Do not gate this call with biometricAvailable(). The native plugin uses
    // `useFallback` to offer the device PIN/password when biometrics are not
    // enrolled, and only the native prompt can make that decision reliably.
    const ok = await this.native.authenticateBiometric("Unlock His.Hope");
    if (ok) {
      this.locked.set(false);
      this.resetIdleTimer();
    }
    return ok;
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
