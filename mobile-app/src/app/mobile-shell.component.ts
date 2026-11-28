import { Component, OnDestroy, OnInit, computed, inject } from "@angular/core";
import { Router, RouterOutlet } from "@angular/router";
import { HisHopeThemeService } from "@his-hope/frontend-foundation";
import { HisHopePermissionService } from "@his-hope/frontend-foundation/auth";
import {
  HisHopeLanguageSwitcherComponent,
  HisHopeTranslatePipe,
} from "@his-hope/frontend-foundation/i18n";
import {
  HisHopeBrandComponent,
  HisHopeMobileConfigNavComponent,
  HisHopeMobileIconComponent,
} from "@his-hope/frontend-foundation/ui";
import {
  HisHopeMobilePinComponent,
  HisHopeMobileShellSecurityComponent,
} from "@his-hope/mobile-foundation/angular";
import { MobileAuthService } from "./core/auth.service";
import { NativeCapabilityService } from "./core/native-capability.service";
import { MobileAdminApiService } from "./core/admin-api.service";
import { MOBILE_NAV_ITEMS } from "./core/mobile-nav.config";
import { MobilePlatformCapabilitiesService } from "./core/services/mobile-platform-capabilities.service";
import { catchError, filter, from, of, switchMap, take } from "rxjs";

@Component({
  standalone: true,
  imports: [
    RouterOutlet,
    HisHopeBrandComponent,
    HisHopeLanguageSwitcherComponent,
    HisHopeMobileConfigNavComponent,
    HisHopeMobileIconComponent,
    HisHopeMobilePinComponent,
    HisHopeMobileShellSecurityComponent,
    HisHopeTranslatePipe,
  ],
  template: `
    <main class="mobile-shell">
      <hh-mobile-shell-security (signOut)="logout()" />
      <header class="mobile-shell__header">
        <hh-brand [caption]="'admin.identityAdministration' | hhTranslate" />
        <div class="header-actions">
          <hh-language-switcher />
          <div class="header-menu">
            <button
              class="hh-icon-button"
              type="button"
              (click)="menuOpen = !menuOpen"
              [attr.aria-expanded]="menuOpen"
              aria-haspopup="menu"
              [attr.aria-label]="'mobile.openAccountMenu' | hhTranslate"
            >
              <hh-mobile-icon name="more" />
            </button>
            @if (menuOpen) {
              <div class="header-menu__panel" role="menu">
                @if (native.isNative && biometricAvailable) {
                  <button
                    type="button"
                    role="menuitem"
                    (click)="menuOpen = false; unlock()"
                  >
                    <hh-mobile-icon name="biometric" size="small" />{{
                      "mobile.unlockBiometrics" | hhTranslate
                    }}
                  </button>
                }
                <button
                  type="button"
                  role="menuitem"
                  (click)="menuOpen = false; toggleTheme()"
                >
                  <hh-mobile-icon name="theme" size="small" />{{
                    "mobile.switchTheme" | hhTranslate
                  }}
                </button>
                <button
                  class="header-menu__danger"
                  type="button"
                  role="menuitem"
                  (click)="menuOpen = false; logout()"
                >
                  <hh-mobile-icon name="logout" size="small" />{{
                    "admin.logout" | hhTranslate
                  }}
                </button>
                <button
                  type="button"
                  role="menuitem"
                  (click)="menuOpen = false; openNotifications()"
                >
                  <hh-mobile-icon name="notifications" size="small" />{{
                    "mobile.notifications" | hhTranslate
                  }}
                </button>
                <hh-mobile-pin />
              </div>
            }
          </div>
        </div>
      </header>
      <section class="mobile-shell__content"><router-outlet /></section>
      <hh-mobile-config-nav [items]="visibleNavItems()" />
    </main>
  `,
  styles: [
    `
      :host {
        display: block;
        min-height: 100dvh;
      }
      .mobile-shell {
        min-height: 100dvh;
        background: var(--bg-warm);
        color: var(--text-primary);
      }
      .mobile-shell__header {
        position: sticky;
        top: 0;
        z-index: 20;
        display: flex;
        align-items: center;
        justify-content: space-between;
        gap: var(--space-md);
        min-height: var(--mobile-toolbar-height);
        padding: max(var(--space-inset), env(safe-area-inset-top)) var(--space-lg) var(--space-inset);
        border-bottom: 1px solid var(--border-light);
        background: color-mix(in srgb, var(--surface-white) 94%, transparent);
        backdrop-filter: blur(var(--blur-shell));
      }
      .header-actions {
        display: flex;
        align-items: center;
        gap: var(--space-sm);
        min-width: 0;
      }
      .header-menu {
        position: relative;
      }
      .header-menu__panel {
        position: absolute;
        top: calc(100% + var(--space-sm));
        right: 0;
        z-index: 30;
        display: grid;
        gap: var(--space-2xs);
        width: var(--max-width-popover-compact);
        max-width: min(var(--max-width-popover-compact), calc(100vw - var(--space-2xl)));
        box-sizing: border-box;
        padding: var(--space-sm);
        border: 1px solid var(--border-default);
        border-radius: var(--radius-panel);
        background: var(--surface-white);
        box-shadow: var(--shadow-shell-menu);
      }
      .header-menu__panel button {
        display: flex;
        align-items: center;
        gap: var(--space-md);
        min-height: var(--touch-target);
        padding: 0 var(--space-md);
        border: 0;
        border-radius: var(--radius-chip);
        background: transparent;
        color: var(--text-primary);
        font: inherit;
        text-align: left;
      }
      .header-menu__panel hh-mobile-pin {
        display: block;
        min-width: 0;
      }
      .header-menu__panel button:active,
      .header-menu__panel button:focus-visible {
        background: var(--color-primary-soft);
        outline: 0;
      }
      .header-menu__danger {
        color: var(--color-danger, #b3261e) !important;
      }
      .header-actions .hh-icon-button {
        flex: 0 0 40px;
        min-width: 40px;
        min-height: var(--button-height);
      }
      .mobile-shell__content {
        width: min(calc(100% - var(--space-2xl)), 720px);
        margin: 0 auto;
        padding: var(--space-lg) 0 calc(96px + env(safe-area-inset-bottom));
      }
      .header-actions button:focus-visible {
        outline: var(--focus-ring-width-strong) solid
          color-mix(in srgb, var(--color-primary) 35%, transparent);
        outline-offset: var(--focus-ring-width);
      }
      @media (max-width: 380px) {
        .mobile-shell__header {
          padding-inline: var(--space-inset);
        }
        .header-actions {
          gap: var(--space-xxs);
        }
        .header-actions .hh-icon-button {
          flex-basis: 36px;
          width: var(--control-height-compact);
          min-width: 36px;
        }
      }
    `,
  ],
})
export class MobileShellComponent implements OnInit, OnDestroy {
  private readonly router = inject(Router);
  readonly auth = inject(MobileAuthService);
  readonly native = inject(NativeCapabilityService);
  private readonly platformCapabilities = inject(MobilePlatformCapabilitiesService);
  private readonly api = inject(MobileAdminApiService);
  private readonly permissions = inject(HisHopePermissionService);
  private readonly theme = inject(HisHopeThemeService);
  biometricAvailable = false;
  menuOpen = false;
  private nativeRefreshRelease?: () => void;
  // Keep every entry until the permission snapshot lands, otherwise the bar
  // renders empty for the first frames after a cold start.
  readonly visibleNavItems = computed(() =>
    this.permissions.hasSnapshot()
      ? MOBILE_NAV_ITEMS.filter(
          (item) => !item.permission || this.permissions.has(item.permission),
        )
      : MOBILE_NAV_ITEMS,
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
    this.theme.setPlatform("mobile");
    this.biometricAvailable = await this.native.biometricAvailable();
    if (this.native.isNative) {
      // The route guard can render the shell while the OIDC callback is still
      // finishing secure token persistence. Register FCM only after the
      // authenticated stream is true, so the API call has a usable token.
      this.auth.isAuthenticated$
        .pipe(
          filter((isAuthenticated) => isAuthenticated),
          take(1),
          switchMap(() => from(this.native.registerPush())),
          catchError(() => of(null)),
        )
        .subscribe();
    }
    void this.platformCapabilities
      .bindNativeRefresh(async (event) => {
        await event.complete();
      })
      .then((release) => {
        this.nativeRefreshRelease = release;
      });
  }

  ngOnDestroy(): void {
    this.nativeRefreshRelease?.();
  }

  async unlock(): Promise<void> {
    await this.auth.unlockWithBiometric();
  }

  logout(): void {
    this.auth.logout();
  }

  openNotifications(): void {
    void this.router.navigateByUrl("/admin/notifications");
  }

  toggleTheme(): void {
    this.theme.setTheme(
      this.theme.resolvedTheme() === "dark" ? "light" : "dark",
    );
  }
}
