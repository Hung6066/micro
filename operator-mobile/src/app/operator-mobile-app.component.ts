import { Component, computed, inject, OnInit } from "@angular/core";
import { FormsModule } from "@angular/forms";
import { RouterLink, RouterLinkActive, RouterOutlet } from "@angular/router";
import { HisHopeThemeService } from "@his-hope/frontend-foundation";
import { HisHopePermissionService } from "@his-hope/frontend-foundation/auth";
import { HisHopeLanguageSwitcherComponent, HisHopeTranslatePipe } from "@his-hope/frontend-foundation/i18n";
import { HisHopeMobileIconComponent } from "@his-hope/frontend-foundation/ui";
import { HisHopeSelectComponent } from "@his-hope/frontend-foundation/ui";
import {
  HisHopeMobilePinComponent,
  HisHopeMobileShellSecurityComponent,
} from "@his-hope/mobile-foundation/angular";
import { catchError, of } from "rxjs";
import { MobileAdminApiService } from "./core/admin-api.service";
import { MobileAuthService } from "./core/auth.service";
import { OPERATOR_MOBILE_NAV_ITEMS } from "./core/mobile-nav.config";
import { NativeCapabilityService } from "./core/native-capability.service";
import { OperatorMobileTenantContextService } from "./core/operator-mobile-tenant-context.service";
import { OperatorMobileReadCacheService } from "./core/services/operator-mobile-read-cache.service";
import { OperationQueueService } from "./core/offline/operation-queue.service";

@Component({
  standalone: true,
  imports: [
    RouterOutlet,
    FormsModule,
    RouterLink,
    RouterLinkActive,
    HisHopeLanguageSwitcherComponent,
    HisHopeMobileIconComponent,
    HisHopeMobilePinComponent,
    HisHopeMobileShellSecurityComponent,
    HisHopeSelectComponent,
    HisHopeTranslatePipe,
  ],
  templateUrl: "./operator-mobile-app.component.html",
  styleUrls: ["./operator-mobile-app.component.scss"],
})
export class OperatorMobileAppComponent implements OnInit {
  readonly tenant = inject(OperatorMobileTenantContextService);
  readonly native = inject(NativeCapabilityService);
  readonly readCache = inject(OperatorMobileReadCacheService);
  private readonly queue = inject(OperationQueueService);
  private readonly auth = inject(MobileAuthService);
  private readonly api = inject(MobileAdminApiService);
  private readonly permissions = inject(HisHopePermissionService);
  private readonly theme = inject(HisHopeThemeService);

  menuOpen = false;
  biometricAvailable = false;

  readonly visibleNavItems = computed(() =>
    this.permissions.hasSnapshot()
      ? OPERATOR_MOBILE_NAV_ITEMS.filter(
          (item) => !item.permission || this.permissions.has(item.permission),
        )
      : OPERATOR_MOBILE_NAV_ITEMS,
  );

  constructor() {
    this.api
      .getMyPermissions()
      .pipe(catchError(() => of(null)))
      .subscribe((snapshot) => {
        if (snapshot) this.permissions.setPermissions(snapshot.permissions);
      });
  }

  async ngOnInit(): Promise<void> {
    this.biometricAvailable = await this.native.biometricAvailable();
    await this.tenant.initialize();
  }

  async selectTenant(event: Event): Promise<void> {
    await this.tenant.setActiveTenant((event.target as HTMLSelectElement).value);
  }

  async selectTenantValue(value: string | null): Promise<void> {
    await this.tenant.setActiveTenant(value ?? "");
  }

  toggleTheme(): void {
    this.theme.setTheme(this.theme.resolvedTheme() === "dark" ? "light" : "dark");
  }

  async unlock(): Promise<void> {
    await this.auth.unlockWithBiometric();
  }

  logout(): void {
    void this.queue.clear();
    this.auth.logout();
  }
}
