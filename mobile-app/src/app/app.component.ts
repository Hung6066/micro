import { Component, inject } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { HisHopeOfflineBannerComponent, HisHopeThemeService, HisHopeToastComponent } from '@his-hope/frontend-foundation';
import { MobileAuthService } from './core/auth.service';
import { NativeCapabilityService } from './core/native-capability.service';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [RouterOutlet, HisHopeOfflineBannerComponent, HisHopeToastComponent],
  template: '<hh-offline-banner></hh-offline-banner><router-outlet></router-outlet><hh-toast-outlet />',
})
export class AppComponent {
  private readonly auth = inject(MobileAuthService);
  private readonly theme = inject(HisHopeThemeService);
  private readonly native = inject(NativeCapabilityService);
  constructor() { this.auth.checkAuth().subscribe(); this.theme.restore(); void this.native.initialize(); }
}
