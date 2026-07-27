import { Injectable, inject } from '@angular/core';
import { Router } from '@angular/router';
import { App } from '@capacitor/app';
import { Browser } from '@capacitor/browser';
import { Capacitor } from '@capacitor/core';
import { Camera, CameraResultType, CameraSource } from '@capacitor/camera';
import { PushNotifications } from '@capacitor/push-notifications';
import { SecureStorage } from '@aparajita/capacitor-secure-storage';
import { NativeBiometric } from 'capacitor-native-biometric';
import { HisHopeCameraCapture, HisHopeCameraCaptureOptions, HisHopeDeepLink } from '@his-hope/mobile-foundation';

@Injectable({ providedIn: 'root' })
export class NativeCapabilityService {
  private readonly router = inject(Router);
  readonly isNative = Capacitor.isNativePlatform();
  private pendingAuthCallbackUrl: string | null = null;
  private pushRegistrationPromise: Promise<string | null> | null = null;

  async initialize(): Promise<void> {
    if (!this.isNative) return;
    await App.addListener('appUrlOpen', event => {
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
      presentationStyle: 'popover',
      toolbarColor: '#195c43',
    });
  }

  consumeAuthCallbackUrl(): string | null {
    const url = this.pendingAuthCallbackUrl;
    this.pendingAuthCallbackUrl = null;
    return url;
  }

  private async navigateDeepLink(url: string): Promise<void> {
    const link = this.parseDeepLink(url);
    if (!link) return;
    if (url.startsWith('hishope://auth/callback')) this.pendingAuthCallbackUrl = url;
    if (url.startsWith('hishope://auth/')) await Browser.close().catch(() => undefined);
    await this.router.navigateByUrl(link.path + this.queryString(link.query));
  }

  async secureGet(key: string): Promise<string | null> {
    if (!this.isNative) return null;
    try {
      return await SecureStorage.getItem(key);
    } catch {
      return null;
    }
  }

  async secureSet(key: string, value: string): Promise<void> {
    if (this.isNative) await SecureStorage.setItem(key, value);
  }

  async secureRemove(key: string): Promise<void> {
    if (this.isNative) await SecureStorage.removeItem(key);
  }

  async biometricAvailable(): Promise<boolean> {
    if (!this.isNative) return false;
    try {
      return (await NativeBiometric.isAvailable({ useFallback: false })).isAvailable;
    } catch {
      return false;
    }
  }

  async authenticateBiometric(reason = 'Verify your identity to continue'): Promise<boolean> {
    if (!this.isNative) return false;
    try {
      await NativeBiometric.verifyIdentity({ reason, title: 'His.Hope secure access', useFallback: false });
      return true;
    } catch {
      return false;
    }
  }

  async capturePhoto(options: HisHopeCameraCaptureOptions = {}): Promise<HisHopeCameraCapture | null> {
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
      if (permission.receive !== 'granted') permission = await PushNotifications.requestPermissions();
      if (permission.receive !== 'granted') return null;
      const token = new Promise<string | null>(resolve => {
        void PushNotifications.addListener('registration', result => resolve(result.value));
        void PushNotifications.addListener('registrationError', () => resolve(null));
      });
      await PushNotifications.register();
      return token;
    } catch {
      return null;
    }
  }

  private parseDeepLink(url: string): HisHopeDeepLink | null {
    try {
      const parsed = new URL(url);
      const path = parsed.protocol === 'hishope:' && parsed.hostname === 'auth'
        ? `/auth${parsed.pathname}`
        : parsed.pathname;
      return { path, query: Object.fromEntries(parsed.searchParams.entries()) };
    } catch {
      return null;
    }
  }

  private queryString(query: Readonly<Record<string, string>>): string {
    const value = new URLSearchParams(query).toString();
    return value ? `?${value}` : '';
  }
}
