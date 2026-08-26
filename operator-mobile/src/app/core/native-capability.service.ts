import { Injectable, inject } from "@angular/core";
import { Router } from "@angular/router";
import { App } from "@capacitor/app";
import { Browser } from "@capacitor/browser";
import { Capacitor } from "@capacitor/core";
import { Camera, CameraResultType, CameraSource } from "@capacitor/camera";
import { LocalNotifications } from "@capacitor/local-notifications";
import { PushNotifications } from "@capacitor/push-notifications";
import { SecureStorage } from "@aparajita/capacitor-secure-storage";
import { NativeBiometric } from "capacitor-native-biometric";
import { firstValueFrom } from "rxjs";
import { MobilePlatformService } from "./mobile-platform.service";
import { MobileAdminApiService } from "./admin-api.service";
import { environment } from "../../environments/environment";
import {
  createHisHopeNativeMfaBridge,
  HisHopeCameraCapture,
  HisHopeCameraCaptureOptions,
  HisHopeDeepLink,
  HisHopeNativeMfaRequest,
  HisHopeNativeMfaResult,
  HisHopePushCapability,
  HisHopePushRegistrationCoordinator,
  HisHopePushTokenRegistrar,
  isHisHopeDeepLinkAllowed,
} from "@his-hope/mobile-foundation";

const ALLOWED_DEEP_LINKS = [
  { scheme: "hishope", host: "auth", pathPrefix: "/callback" },
  { scheme: "hishope", host: "auth", pathPrefix: "/logout-callback" },
  { scheme: "https", host: "mobile.his-hope.example", pathPrefix: "/auth/callback" },
  { scheme: "https", host: "mobile.his-hope.example", pathPrefix: "/auth/logout-callback" },
];

@Injectable({ providedIn: "root" })
export class NativeCapabilityService {
  private readonly router = inject(Router);
  readonly isNative = Capacitor.isNativePlatform();
  private pendingAuthCallbackUrl: string | null = null;
  private pushRegistrationPromise: Promise<string | null> | null = null;
  private pushListenersInitialized = false;
  private readonly platform = inject(MobilePlatformService);
  private readonly api = inject(MobileAdminApiService);
  private readonly pushCapability: HisHopePushCapability = {
    register: () => this.registerNativePush(),
    unregister: () => PushNotifications.unregister(),
  };
  private readonly pushTokenRegistrar: HisHopePushTokenRegistrar = {
    registerToken: (token, platform) => platform === "web"
      ? Promise.resolve()
      : this.platform.registerPushToken(token, platform),
  };

  async initialize(): Promise<void> {
    if (!this.isNative) return;
    await this.initializePushNotifications();
    await App.addListener("appUrlOpen", (event) => {
      void this.navigateDeepLink(event.url);
    });
    const launch = await App.getLaunchUrl();
    if (launch?.url) await this.navigateDeepLink(launch.url);
  }

  private async initializePushNotifications(): Promise<void> {
    if (this.pushListenersInitialized) return;
    this.pushListenersInitialized = true;
    const platform = Capacitor.getPlatform();
    if (platform === "android") {
      await LocalNotifications.createChannel({
        id: "his_hope_default",
        name: "His.Hope notifications",
        description: "Notifications from His.Hope",
        importance: 5,
        visibility: 1,
        sound: "default",
      });
    }
    if (platform === "ios") {
      const permission = await LocalNotifications.checkPermissions();
      if (permission.display !== "granted") {
        await LocalNotifications.requestPermissions();
      }
    }
    await PushNotifications.addListener("pushNotificationReceived", notification => {
      void LocalNotifications.schedule({
        notifications: [{
          id: Math.floor(Date.now() % 2147483647),
          title: notification.title ?? "His.Hope",
          body: notification.body ?? "",
          ...(platform === "android" ? { channelId: "his_hope_default" } : {}),
          extra: notification.data,
        }],
      });
    });
    await PushNotifications.addListener("pushNotificationActionPerformed", () => {
      void this.router.navigateByUrl("/notifications");
    });
  }

  async openAuthBrowser(url: string): Promise<void> {
    if (!this.isNative) {
      window.location.assign(url);
      return;
    }
    // Chrome Custom Tabs follow Identity Docker Location headers
    // (identityservice:5003) which the emulator cannot resolve. The native
    // auth WebView rewrites those hosts onto the public gateway origin.
    await this.platform.openPinnedAuthBrowser(url);
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
    // The pinned iOS auth view dismisses itself before this callback is routed
    // back into Angular. Android's system browser still needs closing.
    if (url.startsWith("hishope://auth/callback") && Capacitor.getPlatform() !== "ios")
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
    const platform = Capacitor.getPlatform();
    if (platform !== "android" && platform !== "ios") return null;
    this.pushRegistrationPromise = new HisHopePushRegistrationCoordinator(
      this.pushCapability,
      this.pushTokenRegistrar,
      platform,
    ).register().catch(error => {
      // Do not permanently memoize a transient failure while the OIDC token
      // is being hydrated or refreshed. A later authenticated shell can retry.
      this.pushRegistrationPromise = null;
      throw error;
    });
    return this.pushRegistrationPromise;
  }

  async unregisterPush(): Promise<void> {
    if (this.isNative) await PushNotifications.unregister();
    this.pushRegistrationPromise = null;
  }

  private async registerNativePush(): Promise<string | null> {
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
      return await token;
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

  async nativePasskeySupported(): Promise<boolean> {
    return this.platform.isPasskeySupported();
  }

  registerNativePasskey(options: Readonly<Record<string, unknown>>): Promise<Readonly<Record<string, unknown>>> {
    return this.platform.register(options);
  }

  authenticateNativePasskey(options: Readonly<Record<string, unknown>>): Promise<Readonly<Record<string, unknown>>> {
    return this.platform.authenticate(options);
  }

  approveMfa(request: HisHopeNativeMfaRequest): Promise<HisHopeNativeMfaResult> {
    const bridge = createHisHopeNativeMfaBridge({
      passkey: {
        isSupported: () => this.nativePasskeySupported(),
        authenticate: options => this.authenticateNativePasskey(options),
      },
      server: {
        requestOptions: ticket => firstValueFrom(this.api.nativeMfaOptions(ticket)),
        complete: (ticket, assertion) => firstValueFrom(this.api.completeNativeMfa(ticket, assertion)),
      },
    });
    return bridge.approveMfa(request);
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
