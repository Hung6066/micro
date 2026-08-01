import { Component, OnDestroy, OnInit, inject } from "@angular/core";
import { RouterLink, RouterLinkActive, RouterOutlet } from "@angular/router";
import {
  HisHopeBrandComponent,
  HisHopeLanguageSwitcherComponent,
  HisHopeMobileIconComponent,
  HisHopePermissionService,
  HisHopeThemeService,
  HisHopeTranslatePipe,
} from "@his-hope/frontend-foundation";
import { MobileAuthService } from "./core/auth.service";
import { NativeCapabilityService } from "./core/native-capability.service";
import { MobileLockService } from "./core/mobile-lock.service";
import { MobileAdminApiService } from "./core/admin-api.service";
import { catchError, of } from "rxjs";
import { MobilePinComponent } from "./features/mobile-pin.component";

@Component({
  standalone: true,
  imports: [
    RouterLink,
    RouterLinkActive,
    RouterOutlet,
    HisHopeBrandComponent,
    HisHopeLanguageSwitcherComponent,
    HisHopeMobileIconComponent,
    MobilePinComponent,
    HisHopeTranslatePipe,
  ],
  template: `
    <main class="mobile-shell">
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
                    <hh-mobile-icon name="biometric" size="small" />{{ 'mobile.unlockBiometrics' | hhTranslate }}
                  </button>
                }
                <button
                  type="button"
                  role="menuitem"
                  (click)="menuOpen = false; toggleTheme()"
                >
                  <hh-mobile-icon name="theme" size="small" />{{ 'mobile.switchTheme' | hhTranslate }}
                </button>
                <button
                  class="header-menu__danger"
                  type="button"
                  role="menuitem"
                  (click)="menuOpen = false; logout()"
                >
                    <hh-mobile-icon name="logout" size="small" />{{ 'admin.logout' | hhTranslate }}
                  </button>
                @if (native.isNative) {
                  <app-mobile-pin />
                }
              </div>
            }
          </div>
        </div>
      </header>
      <section class="mobile-shell__content"><router-outlet /></section>
      <nav class="mobile-shell__nav" [attr.aria-label]="'mobile.adminNavigation' | hhTranslate">
        <a
          routerLink="/admin/dashboard"
          routerLinkActive="active"
          ariaCurrentWhenActive="page"
          ><hh-mobile-icon name="home" size="small" /><span>{{ 'admin.dashboard' | hhTranslate }}</span></a
        >
        <a
          routerLink="/admin/clients"
          routerLinkActive="active"
          ariaCurrentWhenActive="page"
          ><hh-mobile-icon name="clients" size="small" /><span>{{ 'admin.clients' | hhTranslate }}</span></a
        >
        <a
          routerLink="/admin/users"
          routerLinkActive="active"
          ariaCurrentWhenActive="page"
          ><hh-mobile-icon name="users" size="small" /><span>{{ 'admin.users' | hhTranslate }}</span></a
        >
        <a
          routerLink="/admin/roles"
          routerLinkActive="active"
          ariaCurrentWhenActive="page"
          ><hh-mobile-icon name="roles" size="small" /><span>{{ 'admin.roles' | hhTranslate }}</span></a
        >
        <a
          routerLink="/admin/consents"
          routerLinkActive="active"
          ariaCurrentWhenActive="page"
          ><hh-mobile-icon name="consents" size="small" /><span
            >{{ 'admin.consents' | hhTranslate }}</span
          ></a
        >
      </nav>
      @if (lock.locked()) {
        <div
          class="lock-overlay"
          role="dialog"
          aria-modal="true"
          [attr.aria-label]="'mobile.sessionLocked' | hhTranslate"
        >
          <hh-mobile-icon name="biometric" size="large" />
          <h2>{{ 'mobile.sessionLocked' | hhTranslate }}</h2>
          <p>{{ 'mobile.unlockContinue' | hhTranslate }}</p>
          <button
            class="hh-button hh-button--primary"
            type="button"
            (click)="attemptUnlock()"
          >
            {{ 'mobile.unlock' | hhTranslate }}
          </button>
          @if (unlockFailed) {
            <p class="lock-overlay__error">
              {{ 'mobile.unlockFailed' | hhTranslate }}
            </p>
          }
          <button
            class="lock-overlay__signout"
            type="button"
            (click)="logout()"
          >
            {{ 'mobile.signOutInstead' | hhTranslate }}
          </button>
        </div>
      }
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
        gap: 12px;
        min-height: 56px;
        padding: max(10px, env(safe-area-inset-top)) 16px 10px;
        border-bottom: 1px solid var(--border-light);
        background: color-mix(in srgb, var(--surface-white) 94%, transparent);
        backdrop-filter: blur(16px);
      }
      .header-actions {
        display: flex;
        align-items: center;
        gap: 8px;
        min-width: 0;
      }
      .header-menu {
        position: relative;
      }
      .header-menu__panel {
        position: absolute;
        top: calc(100% + 8px);
        right: 0;
        z-index: 30;
        display: grid;
        gap: 4px;
        width: 220px;
        max-width: min(220px, calc(100vw - 24px));
        box-sizing: border-box;
        padding: 8px;
        border: 1px solid var(--border-default);
        border-radius: 16px;
        background: var(--surface-white);
        box-shadow: 0 14px 32px
          color-mix(in srgb, var(--text-primary) 18%, transparent);
      }
      .header-menu__panel button {
        display: flex;
        align-items: center;
        gap: 12px;
        min-height: 44px;
        padding: 0 12px;
        border: 0;
        border-radius: 10px;
        background: transparent;
        color: var(--text-primary);
        font: inherit;
        text-align: left;
      }
      .header-menu__panel app-mobile-pin {
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
        min-height: 40px;
      }
      .mobile-shell__content {
        width: min(calc(100% - 24px), 720px);
        margin: 0 auto;
        padding: 16px 0 calc(96px + env(safe-area-inset-bottom));
      }
      .mobile-shell__nav {
        position: fixed;
        left: 50%;
        bottom: max(10px, env(safe-area-inset-bottom));
        z-index: 10;
        display: grid;
        grid-template-columns: repeat(5, 1fr);
        width: min(calc(100% - 24px), 720px);
        transform: translateX(-50%);
        padding: 7px;
        border: 1px solid
          color-mix(in srgb, var(--border-default) 82%, var(--surface-white));
        border-radius: 22px;
        background: color-mix(in srgb, var(--surface-white) 94%, transparent);
        box-shadow:
          0 14px 34px color-mix(in srgb, var(--text-primary) 16%, transparent),
          0 2px 8px color-mix(in srgb, var(--text-primary) 8%, transparent);
        backdrop-filter: blur(18px);
      }
      .mobile-shell__nav a {
        position: relative;
        display: grid;
        justify-items: center;
        align-content: center;
        grid-template-rows: 30px auto;
        gap: 3px;
        min-height: 56px;
        padding: 4px 2px 5px;
        border-radius: 16px;
        color: var(--text-secondary);
        font-size: 11px;
        line-height: 1.2;
        text-decoration: none;
        transition:
          color 0.16s ease,
          transform 0.16s ease,
          background 0.16s ease,
          box-shadow 0.16s ease;
      }
      .mobile-shell__nav a:active {
        transform: scale(0.96);
      }
      .mobile-shell__nav a.active {
        color: var(--color-primary);
        font-weight: var(--font-weight-semibold);
        background: color-mix(
          in srgb,
          var(--color-primary-soft) 84%,
          var(--surface-white)
        );
        box-shadow:
          inset 0 0 0 1px
            color-mix(in srgb, var(--color-primary) 13%, transparent),
          0 4px 12px color-mix(in srgb, var(--color-primary) 10%, transparent);
      }
      .mobile-shell__nav a.active::before {
        content: "";
        position: absolute;
        top: 0;
        left: 50%;
        z-index: 1;
        width: 8px;
        height: 8px;
        border-radius: 50% 50% 50% 0;
        background: var(--color-primary);
        transform: translateX(-50%) rotate(-45deg);
        transform-origin: center;
        animation: hh-nav-droplet 1.8s ease-in-out infinite;
      }
      .mobile-shell__nav a.active::after {
        content: "";
        position: absolute;
        right: 50%;
        top: 2px;
        width: 22px;
        height: 3px;
        border-radius: 999px;
        background: var(--color-primary);
        transform: translateX(50%);
      }
      .mobile-shell__nav a hh-mobile-icon {
        width: 36px;
        height: 28px;
        border-radius: 10px;
        color: currentColor;
        transition:
          background 0.16s ease,
          color 0.16s ease;
      }
      .mobile-shell__nav a.active hh-mobile-icon {
        background: var(--color-primary);
        color: var(--surface-white);
        box-shadow: 0 4px 12px
          color-mix(in srgb, var(--color-primary) 28%, transparent);
      }
      .mobile-shell__nav a:focus-visible,
      .header-actions button:focus-visible {
        outline: 3px solid
          color-mix(in srgb, var(--color-primary) 35%, transparent);
        outline-offset: 2px;
      }
      @keyframes hh-nav-droplet {
        0%,
        100% {
          margin-left: -9px;
          opacity: 0.7;
        }
        50% {
          margin-left: 9px;
          opacity: 1;
        }
      }
      @media (prefers-reduced-motion: reduce) {
        .mobile-shell__nav a.active::before {
          animation: none;
        }
        .mobile-shell__nav a {
          transition: none;
        }
      }
      @media (max-width: 380px) {
        .mobile-shell__header {
          padding-inline: 10px;
        }
        .header-actions {
          gap: 3px;
        }
        .header-actions .hh-icon-button {
          flex-basis: 36px;
          width: 36px;
          min-width: 36px;
        }
        .mobile-shell__nav {
          width: calc(100% - 16px);
          padding: 6px;
          border-radius: 20px;
        }
        .mobile-shell__nav a {
          font-size: 10px;
        }
      }
      .lock-overlay {
        position: fixed;
        inset: 0;
        z-index: 50;
        display: grid;
        justify-items: center;
        align-content: center;
        gap: 10px;
        padding: 24px;
        text-align: center;
        background: color-mix(in srgb, var(--bg-warm) 96%, transparent);
        backdrop-filter: blur(24px);
      }
      .lock-overlay hh-mobile-icon {
        width: 56px;
        height: 56px;
        color: var(--color-primary);
      }
      .lock-overlay h2 {
        margin: 4px 0 0;
      }
      .lock-overlay p {
        margin: 0;
        color: var(--text-secondary);
        max-width: 32ch;
      }
      .lock-overlay__error {
        color: var(--color-danger, #b3261e);
        font-size: 13px;
      }
      .lock-overlay__signout {
        margin-top: 8px;
        border: 0;
        background: transparent;
        color: var(--text-secondary);
        text-decoration: underline;
      }
    `,
  ],
})
export class MobileShellComponent implements OnInit, OnDestroy {
  readonly auth = inject(MobileAuthService);
  readonly native = inject(NativeCapabilityService);
  readonly lock = inject(MobileLockService);
  biometricAvailable = false;
  menuOpen = false;
  unlockFailed = false;
  private readonly api = inject(MobileAdminApiService);
  private readonly permissions = inject(HisHopePermissionService);
  private readonly theme = inject(HisHopeThemeService);
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
    if (this.native.isNative) void this.native.registerPush();
    this.lock.arm();
  }
  ngOnDestroy(): void {
    this.lock.disarm();
  }
  async unlock(): Promise<void> {
    await this.auth.unlockWithBiometric();
  }
  async attemptUnlock(): Promise<void> {
    this.unlockFailed = false;
    this.unlockFailed = !(await this.lock.unlock());
  }
  logout(): void {
    this.auth.logout();
  }
  toggleTheme(): void {
    this.theme.setTheme(this.theme.resolvedTheme() === "dark" ? "light" : "dark");
  }
}
