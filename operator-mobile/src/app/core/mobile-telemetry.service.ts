import { Injectable, inject } from "@angular/core";
import { MobilePlatformService } from "./mobile-platform.service";
import { environment } from "../../environments/environment";
import { Capacitor } from "@capacitor/core";
import type { HisHopeMobilePlatform } from "@his-hope/mobile-foundation";
import * as Sentry from "@sentry/angular";

@Injectable({ providedIn: "root" })
export class MobileTelemetryService {
  private readonly platform = inject(MobilePlatformService);
  private initialized = false;

  // Native iOS/Android telemetry must use the pinned API boundary below. The
  // browser Sentry transport is retained only for web preview builds.
  private get directSentryEnabled(): boolean {
    return !Capacitor.isNativePlatform() && !!environment.sentryDsn;
  }

  initialize(): void {
    if (this.initialized || typeof window === "undefined") return;
    this.initialized = true;
    if (this.directSentryEnabled) {
      Sentry.init({
        dsn: environment.sentryDsn,
        environment: environment.sentryEnvironment,
        release: `his-hope-mobile@${environment.appVersion}`,
        tracesSampleRate: environment.production ? 0.05 : 0,
        beforeSend: (event) => {
          // Mobile telemetry must never carry credentials or patient data.
          if (event.request) {
            delete event.request.data;
            delete event.request.cookies;
            if (event.request.headers) {
              delete event.request.headers["Authorization"];
              delete event.request.headers["authorization"];
              delete event.request.headers["Cookie"];
              delete event.request.headers["cookie"];
            }
          }
          delete event.user;
          return event;
        },
      });
    }
    window.addEventListener("error", (event) => {
      if (this.directSentryEnabled) {
        if (event.error instanceof Error) Sentry.captureException(event.error);
        else Sentry.captureMessage(String(event.message || "Unhandled window error"));
      }
      void this.platform.reportCrash({
        message: String(event.message || "Unhandled window error").slice(0, 2000),
        route: this.telemetryRoute(),
        appVersion: environment.appVersion,
        platform: this.runtimePlatform(),
      }).catch(() => undefined);
    });
    window.addEventListener("unhandledrejection", (event) => {
      if (this.directSentryEnabled) {
        if (event.reason instanceof Error) Sentry.captureException(event.reason);
        else Sentry.captureMessage(this.describe(event.reason));
      }
      void this.platform.reportCrash({
        message: this.describe(event.reason).slice(0, 2000),
        route: this.telemetryRoute(),
        appVersion: environment.appVersion,
        platform: this.runtimePlatform(),
      }).catch(() => undefined);
    });
  }

  record(name: string, durationMs?: number, metadata?: Record<string, string | number | boolean>): void {
    const route = this.telemetryRoute();
    if (this.directSentryEnabled) {
      Sentry.startSpan(
        { name: name.slice(0, 120), op: "mobile.rum", attributes: { route, duration_ms: durationMs ?? 0 } },
        (span) => {
          span.setAttribute("mobile.platform", this.runtimePlatform());
          span.setAttribute("mobile.app_version", environment.appVersion);
          if (metadata) {
            for (const [key, value] of Object.entries(metadata)) {
              if (/token|secret|password|email|phone|patient|user|id/i.test(key)) continue;
              span.setAttribute(`mobile.${key}`, String(value).slice(0, 200));
            }
          }
        },
      );
    }
    void this.platform.reportRum({ name, durationMs, route, appVersion: environment.appVersion, platform: this.runtimePlatform(), metadata }).catch(() => undefined);
  }

  private runtimePlatform(): HisHopeMobilePlatform {
    if (!Capacitor.isNativePlatform()) return "web";
    const platform = Capacitor.getPlatform();
    return platform === "android" || platform === "ios" ? platform : "web";
  }

  private describe(value: unknown): string {
    if (value instanceof Error) return value.message;
    if (typeof value === "string") return value;
    try { return JSON.stringify(value); } catch { return "Unhandled promise rejection"; }
  }

  private telemetryRoute(): string {
    return window.location.pathname
      .replace(/[0-9a-f]{8}-[0-9a-f-]{27,}/gi, ":id")
      .replace(/\/\d+(?=\/|$)/g, "/:id")
      .slice(0, 200);
  }
}
