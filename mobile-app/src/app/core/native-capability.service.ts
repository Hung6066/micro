import { Injectable, inject } from "@angular/core";
import { Router } from "@angular/router";
import { App } from "@capacitor/app";
import { Browser } from "@capacitor/browser";
import { Capacitor } from "@capacitor/core";
import { Camera, CameraResultType, CameraSource } from "@capacitor/camera";
import { PushNotifications } from "@capacitor/push-notifications";
import { SecureStorage } from "@aparajita/capacitor-secure-storage";
import { NativeBiometric } from "capacitor-native-biometric";
import { MobilePlatformService } from "./mobile-platform.service";
import { environment } from "../../environments/environment";
import {
  HisHopeCameraCapture,
  HisHopeCameraCaptureOptions,
  HisHopeDeepLink,
  isHisHopeDeepLinkAllowed,
} from "@his-hope/mobile-foundation";

const ALLOWED_DEEP_LINKS = [
  { scheme: "hishope", host: "auth" },
  { scheme: "https", host: "mobile.his-hope.example", pathPrefix: "/auth" },
];

@Injectable({ providedIn: "root" })
export class NativeCapabilityService {
  private readonly router = inject(Router);
  readonly isNative = Capacitor.isNativePlatform();
  private pendingAuthCallbackUrl: string | null = null;
  private pushRegistrationPromise: Promise<string | null> | null = null;
  private readonly platform = inject(MobilePlatformService);

  async initialize(): Promise<void> {
    if (!this.isNative) return;
    await App.addListener("appUrlOpen", (event) => {
      void this.navigateDeepLink(event.url);
    });
    const launch = await App.getLaunchUrl();
    if (launch?.url) await this.navigateDeepLink(launch.url);
  }

  async openAuthBrowser(url: string): Promise<void> {
    if (!this.isNative) {
      window.location.assign(url);
      return;
    }
    await Browser.open({
      url,
      presentationStyle: "popover",
      toolbarColor: "#195c43",
    });
  }

  consumeAuthCallbackUrl(): string | null {
    const url = this.pendingAuthCallbackUrl;
    this.pendingAuthCallbackUrl = null;
    return url;
  }

  private async navigateDeepLink(url: string): Promise<void> {
    // Android can deliver custom-scheme intents with a URL representation that
    // differs slightly from the WebView URL parser. Keep the OIDC callback
    // explicitly allow-listed by scheme/host/path before using the shared
    // parser, without accepting arbitrary custom-scheme navigation.
    const isOidcCallback = /^hishope:\/\/auth\/callback(?:[?#]|$)/i.test(url);
    if (!isOidcCallback && !isHisHopeDeepLinkAllowed(url, ALLOWED_DEEP_LINKS)) {
      return;
    }
    const link = this.parseDeepLink(url) ?? (isOidcCallback ? this.parseCallbackFallback(url) : null);
    if (!link) return;
    if (url.startsWith("hishope://auth/callback"))
      this.pendingAuthCallbackUrl = url;
    // The custom tab is already handing control back to the app. Waiting for
    // Browser.close() here can delay the router navigation and leave the app
    // on its pre-login screen while the OIDC callback is being exchanged.
    if (url.startsWith("hishope://auth/"))
      void Browser.close().catch(() => undefined);
    const target = link.path + this.queryString(link.query);
    try {
      await this.router.navigateByUrl(target, { replaceUrl: true });
    } catch (error) {
      void error;
    }
  }

  /**
   * Native builds persist to Keychain/Keystore. Web builds (dev/preview only)
   * fall back to sessionStorage — never localStorage — so tokens do not
   * outlive the browser tab.
   */
  async secureGet(key: string): Promise<string | null> {
    if (!this.isNative)
      return typeof sessionStorage === "undefined"
        ? null
        : sessionStorage.getItem(key);
    try {
      return await SecureStorage.getItem(key);
    } catch {
      return null;
    }
  }

  async secureSet(key: string, value: string): Promise<void> {
    if (!this.isNative) {
      sessionStorage?.setItem(key, value);
      return;
    }
    await SecureStorage.setItem(key, value);
  }

  async secureRemove(key: string): Promise<void> {
    if (!this.isNative) {
      sessionStorage?.removeItem(key);
      return;
    }
    await SecureStorage.removeItem(key);
  }

  async biometricAvailable(): Promise<boolean> {
    if (!this.isNative) return false;
    try {
      return (await NativeBiometric.isAvailable({ useFallback: true }))
        .isAvailable;
    } catch {
      return false;
    }
  }

  async authenticateBiometric(
    reason = "Verify your identity to continue",
  ): Promise<boolean> {
    if (!this.isNative) return false;
    try {
      // useFallback lets the OS offer the device passcode when biometrics are
      // unavailable/unenrolled, instead of hard-blocking access to the app.
      await NativeBiometric.verifyIdentity({
        reason,
        title: "His.Hope secure access",
        useFallback: true,
      });
      return true;
    } catch {
      return false;
    }
  }

  async capturePhoto(
    options: HisHopeCameraCaptureOptions = {},
  ): Promise<HisHopeCameraCapture | null> {
    try {
      const photo = await Camera.getPhoto({
        quality: options.quality ?? 82,
        allowEditing: options.allowEditing ?? true,
        width: options.width ?? 1600,
        height: options.height ?? 1600,
        source: CameraSource.Camera,
        resultType: CameraResultType.Uri,
        correctOrientation: true,
        webUseInput: true,
      });
      const uri = photo.webPath ?? photo.path;
      return uri ? { uri, format: photo.format } : null;
    } catch {
      return null;
    }
  }

  async registerPush(): Promise<string | null> {
    if (!this.isNative) return null;
    if (!environment.pushNotificationsEnabled) return null;
    if (this.pushRegistrationPromise) return this.pushRegistrationPromise;
    this.pushRegistrationPromise = this.registerPushInternal();
    return this.pushRegistrationPromise;
  }

  async unregisterPush(): Promise<void> {
    if (this.isNative) await PushNotifications.unregister();
    this.pushRegistrationPromise = null;
  }

  private async registerPushInternal(): Promise<string | null> {
    try {
      let permission = await PushNotifications.checkPermissions();
      if (permission.receive !== "granted")
        permission = await PushNotifications.requestPermissions();
      if (permission.receive !== "granted") return null;
      const token = new Promise<string | null>((resolve) => {
        void PushNotifications.addListener("registration", (result) =>
          resolve(result.value),
        );
        void PushNotifications.addListener("registrationError", () =>
          resolve(null),
        );
      });
      await PushNotifications.register();
      const value = await token;
      if (value) await this.platform.registerPushToken(value, Capacitor.getPlatform() as "android" | "ios");
      return value;
    } catch {
      return null;
    }
  }

  private parseDeepLink(url: string): HisHopeDeepLink | null {
    try {
      const parsed = new URL(url);
      const path =
        parsed.protocol === "hishope:" && parsed.hostname === "auth"
          ? `/auth${parsed.pathname}`
          : parsed.pathname;
      return { path, query: Object.fromEntries(parsed.searchParams.entries()) };
    } catch {
      return null;
    }
  }

  private parseCallbackFallback(url: string): HisHopeDeepLink | null {
    const queryIndex = url.indexOf("?");
    const fragmentIndex = url.indexOf("#");
    const end = fragmentIndex >= 0 ? fragmentIndex : url.length;
    const rawQuery = queryIndex >= 0 && queryIndex < end ? url.slice(queryIndex + 1, end) : "";
    return {
      path: "/auth/callback",
      query: Object.fromEntries(new URLSearchParams(rawQuery).entries()),
    };
  }

  private queryString(query: Readonly<Record<string, string>>): string {
    const value = new URLSearchParams(query).toString();
    return value ? `?${value}` : "";
  }
}
