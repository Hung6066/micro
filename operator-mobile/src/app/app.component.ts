import { Component, inject, signal } from "@angular/core";
import { RouterOutlet } from "@angular/router";
import { filter, take } from "rxjs/operators";
import { HisHopeThemeService } from "@his-hope/frontend-foundation";
import { HisHopeOfflineBannerComponent, HisHopeToastComponent, HisHopeMobileDeviceBlockedScreenComponent, HisHopeMobileDeviceBlockReason, HisHopeMobileMaintenanceScreenComponent } from "@his-hope/frontend-foundation/ui";
import { HisHopeTranslatePipe } from "@his-hope/frontend-foundation/i18n";
import type { HisHopeDeviceSecurityResult } from "@his-hope/mobile-foundation";
import { MobileAuthService } from "./core/auth.service";
import { NativeCapabilityService } from "./core/native-capability.service";
import { MobilePlatformService } from "./core/mobile-platform.service";
import { MobileTelemetryService } from "./core/mobile-telemetry.service";
import { MobilePlatformCapabilitiesService } from "./core/services/mobile-platform-capabilities.service";
import { MobileDeviceAttestationService } from "./core/mobile-device-attestation.service";
import { environment } from "../environments/environment";

@Component({
  selector: "app-root",
  standalone: true,
  imports: [RouterOutlet, HisHopeOfflineBannerComponent, HisHopeToastComponent, HisHopeMobileMaintenanceScreenComponent, HisHopeMobileDeviceBlockedScreenComponent, HisHopeTranslatePipe],
  template: `<hh-offline-banner></hh-offline-banner>
    @if (!platform.maintenance() && !deviceBlocked()) { <router-outlet /> }
    @if (platform.upgradeRequired()) { <section class="mobile-upgrade" role="alert"><h1>{{ "mobile.upgradeRequiredTitle" | hhTranslate }}</h1><p>{{ "mobile.upgradeRequiredMessage" | hhTranslate }}</p>@if (platform.storeUrl(); as storeUrl) { <a class="mobile-upgrade__action" [href]="storeUrl" target="_blank" rel="noopener">{{ "mobile.upgradeOpenStore" | hhTranslate }}</a> }</section> }
    @if (platform.maintenance()) { <hh-mobile-maintenance-screen [retrying]="maintenanceRetrying()" (retry)="retryMaintenance()" /> }
    @if (deviceBlocked()) { <hh-mobile-device-blocked-screen [reasons]="deviceBlockReasons()" (signOut)="signOut()" /> }
    <hh-toast-outlet />`,
  styles: [` .mobile-upgrade { position: fixed; inset: 0; z-index: 100; display: grid; place-content: center; gap: var(--space-inset); padding: var(--space-2xl); text-align: center; background: var(--bg-warm); color: var(--text-primary); } .mobile-upgrade p { max-width: 32ch; color: var(--text-secondary); } .mobile-upgrade__action { justify-self: center; padding: var(--space-md) var(--font-size-icon-sm); border-radius: var(--radius-chip); background: var(--accent); color: white; text-decoration: none; font-weight: 600; } `],
})
export class AppComponent {
  private readonly auth = inject(MobileAuthService);
  private readonly theme = inject(HisHopeThemeService);
  private readonly native = inject(NativeCapabilityService);
  readonly platform = inject(MobilePlatformService);
  private readonly telemetry = inject(MobileTelemetryService);
  private readonly platformCapabilities = inject(MobilePlatformCapabilitiesService);
  private readonly attestation = inject(MobileDeviceAttestationService);
  readonly deviceBlocked = signal(false);
  readonly deviceBlockReasons = signal<readonly HisHopeMobileDeviceBlockReason[]>([]);
  readonly maintenanceRetrying = signal(false);

  constructor() {
    if (!this.isOidcCallbackUrl()) this.auth.checkAuth().subscribe();
    this.theme.restore(); this.theme.setPlatform("mobile");
    this.telemetry.initialize(); void this.native.initialize(); void this.platform.configureCertificatePins();
    void this.bootstrapPolicy(); void this.checkDeviceIntegrity();
    this.auth.isAuthenticated$.pipe(filter(Boolean), take(1)).subscribe(() => void this.attestation.submitIfEligible());
    window.addEventListener("online", () => void this.platformCapabilities.flushOfflineSync());
    if (navigator.onLine) void this.platformCapabilities.flushOfflineSync();
  }
  signOut(): void { this.auth.logout(); }
  async retryMaintenance(): Promise<void> { this.maintenanceRetrying.set(true); try { await this.platform.appPolicy(); } catch { /* keep screen visible */ } finally { this.maintenanceRetrying.set(false); } }
  private isOidcCallbackUrl(): boolean { if (typeof window === "undefined") return false; const path = window.location.pathname.replace(/\/$/, ""); return path.endsWith("/auth/callback"); }
  private async bootstrapPolicy(): Promise<void> { try { await this.platform.appPolicy(); } catch { /* policy outage must not block startup */ } }
  private async checkDeviceIntegrity(): Promise<void> { try { const result = await this.platform.deviceSecurity(); if (this.shouldBlockDevice(result)) { this.deviceBlockReasons.set(this.buildDeviceBlockReasons(result)); this.deviceBlocked.set(true); return; } void this.attestation.submitIfEligible(result); } catch { /* plugin outage is non-blocking in web/dev */ } }
  private shouldBlockDevice(result: HisHopeDeviceSecurityResult): boolean { if (result.status === "compromised" || result.rootedOrJailbroken) return true; if (result.debuggable) return false; return environment.production && result.emulator; }
  private buildDeviceBlockReasons(result: HisHopeDeviceSecurityResult): HisHopeMobileDeviceBlockReason[] { const reasons: HisHopeMobileDeviceBlockReason[] = []; if (result.rootedOrJailbroken) reasons.push({ key: "mobile.deviceBlockedRooted", fallback: "Rooted or jailbroken device detected." }); if (result.emulator) reasons.push({ key: "mobile.deviceBlockedEmulator", fallback: "Emulator environments are not supported in production." }); if (result.status === "compromised") reasons.push({ key: "mobile.deviceBlockedCompromised", fallback: "Device integrity could not be verified." }); return reasons; }
}
