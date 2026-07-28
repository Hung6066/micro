import { Component, inject } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { HisHopeOfflineBannerComponent, HisHopeThemeService, HisHopeToastComponent } from '@his-hope/frontend-foundation';
import { MobileAuthService } from './core/auth.service';
import { NativeCapabilityService } from './core/native-capability.service';
import { MobilePlatformService } from './core/mobile-platform.service';
import { MobileTelemetryService } from './core/mobile-telemetry.service';
import { MobileSyncQueueService } from './core/mobile-sync-queue.service';
import { environment } from '../environments/environment';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [RouterOutlet, HisHopeOfflineBannerComponent, HisHopeToastComponent],
  template: `
    <hh-offline-banner></hh-offline-banner>
    <router-outlet></router-outlet>
    @if (platform.upgradeRequired()) {
      <section class="mobile-upgrade" role="alert">
        <h1>Update required</h1>
        <p>Please install the latest His.Hope Mobile version to continue securely.</p>
        @if (platform.storeUrl(); as storeUrl) {
          <a class="mobile-upgrade__action" [href]="storeUrl" target="_blank" rel="noopener">Open app store</a>
        }
      </section>
    }
    <hh-toast-outlet />
  `,
  styles: [`.mobile-upgrade { position:fixed; inset:0; z-index:100; display:grid; place-content:center; gap:10px; padding:24px; text-align:center; background:var(--bg-warm); color:var(--text-primary); } .mobile-upgrade p { max-width:32ch; color:var(--text-secondary); } .mobile-upgrade__action { justify-self:center; padding:12px 18px; border-radius:10px; background:var(--accent); color:white; text-decoration:none; font-weight:600; }`],
})
export class AppComponent {
  private readonly auth = inject(MobileAuthService);
  private readonly theme = inject(HisHopeThemeService);
  private readonly native = inject(NativeCapabilityService);
  readonly platform = inject(MobilePlatformService);
  private readonly telemetry = inject(MobileTelemetryService);
  private readonly sync = inject(MobileSyncQueueService);
  constructor() {
    this.auth.checkAuth().subscribe();
    this.theme.restore();
    this.telemetry.initialize();
    void this.native.initialize();
    void this.platform.configureCertificatePins();
    void this.bootstrapPolicy();
    window.addEventListener('online', () => void this.sync.flush());
    if (navigator.onLine) void this.sync.flush();
  }

  private async bootstrapPolicy(): Promise<void> {
    try {
      const policy = await this.platform.appPolicy();
      if (policy.maintenance || (policy.forceUpgrade && policy.minimumVersion !== environment.appVersion)) {
        document.body.dataset["hishopeUpgradeRequired"] = "true";
      }
    } catch {
      // A policy outage must not prevent an already-installed app from opening.
    }
  }
}
