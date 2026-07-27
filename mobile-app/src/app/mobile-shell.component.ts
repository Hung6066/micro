import { Component, OnInit, inject } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { HisHopeBrandComponent, HisHopeLanguageSwitcherComponent, HisHopeMobileIconComponent, HisHopePermissionService, HisHopeThemeService } from '@his-hope/frontend-foundation';
import { MobileAuthService } from './core/auth.service';
import { NativeCapabilityService } from './core/native-capability.service';
import { MobileAdminApiService } from './core/admin-api.service';
import { catchError, of } from 'rxjs';

@Component({
  standalone: true,
  imports: [RouterLink, RouterLinkActive, RouterOutlet, HisHopeBrandComponent, HisHopeLanguageSwitcherComponent, HisHopeMobileIconComponent],
  template: `
    <main class="mobile-shell">
      <header class="mobile-shell__header">
        <hh-brand caption="Identity administration" />
        <div class="header-actions">
          <hh-language-switcher />
          <div class="header-menu">
            <button class="hh-icon-button" type="button" (click)="menuOpen = !menuOpen" [attr.aria-expanded]="menuOpen" aria-haspopup="menu" aria-label="Open account menu"><hh-mobile-icon name="more" /></button>
            @if (menuOpen) {
              <div class="header-menu__panel" role="menu">
                @if (native.isNative && biometricAvailable) { <button type="button" role="menuitem" (click)="menuOpen = false; unlock()"><hh-mobile-icon name="biometric" size="small" />Unlock with biometrics</button> }
                <button type="button" role="menuitem" (click)="menuOpen = false; toggleTheme()"><hh-mobile-icon name="theme" size="small" />Switch theme</button>
                <button class="header-menu__danger" type="button" role="menuitem" (click)="menuOpen = false; logout()"><hh-mobile-icon name="logout" size="small" />Sign out</button>
              </div>
            }
          </div>
        </div>
      </header>
      <section class="mobile-shell__content"><router-outlet /></section>
      <nav class="mobile-shell__nav" aria-label="Admin navigation">
        <a routerLink="/admin/dashboard" routerLinkActive="active" ariaCurrentWhenActive="page"><hh-mobile-icon name="home" size="small" /><span>Home</span></a>
        <a routerLink="/admin/clients" routerLinkActive="active" ariaCurrentWhenActive="page"><hh-mobile-icon name="clients" size="small" /><span>Clients</span></a>
        <a routerLink="/admin/users" routerLinkActive="active" ariaCurrentWhenActive="page"><hh-mobile-icon name="users" size="small" /><span>Users</span></a>
        <a routerLink="/admin/roles" routerLinkActive="active" ariaCurrentWhenActive="page"><hh-mobile-icon name="roles" size="small" /><span>Roles</span></a>
        <a routerLink="/admin/consents" routerLinkActive="active" ariaCurrentWhenActive="page"><hh-mobile-icon name="consents" size="small" /><span>Consents</span></a>
      </nav>
    </main>
  `,
  styles: [`
    :host { display: block; min-height: 100dvh; }
    .mobile-shell { min-height: 100dvh; background: var(--bg-warm); color: var(--text-primary); }
    .mobile-shell__header { position:sticky; top:0; z-index:20; display:flex; align-items:center; justify-content:space-between; gap:12px; min-height:56px; padding:max(10px, env(safe-area-inset-top)) 16px 10px; border-bottom:1px solid var(--border-light); background:color-mix(in srgb, var(--surface-white) 94%, transparent); backdrop-filter:blur(16px); }
    .header-actions { display:flex; align-items:center; gap:8px; min-width:0; }
    .header-menu { position:relative; }
    .header-menu__panel { position:absolute; top:calc(100% + 8px); right:0; z-index:30; display:grid; gap:4px; width:220px; padding:8px; border:1px solid var(--border-default); border-radius:16px; background:var(--surface-white); box-shadow:0 14px 32px color-mix(in srgb, var(--text-primary) 18%, transparent); }
    .header-menu__panel button { display:flex; align-items:center; gap:12px; min-height:44px; padding:0 12px; border:0; border-radius:10px; background:transparent; color:var(--text-primary); font:inherit; text-align:left; }
    .header-menu__panel button:active, .header-menu__panel button:focus-visible { background:var(--color-primary-soft); outline:0; }
    .header-menu__danger { color:var(--color-danger, #b3261e)!important; }
    .header-actions .hh-icon-button { flex:0 0 40px; min-width:40px; min-height:40px; }
    .mobile-shell__content { width:min(calc(100% - 24px), 720px); margin:0 auto; padding:16px 0 calc(96px + env(safe-area-inset-bottom)); }
    .mobile-shell__nav { position:fixed; left:50%; bottom:max(10px, env(safe-area-inset-bottom)); z-index:10; display:grid; grid-template-columns:repeat(5,1fr); width:min(calc(100% - 24px), 720px); transform:translateX(-50%); padding:7px; border:1px solid color-mix(in srgb, var(--border-default) 82%, var(--surface-white)); border-radius:22px; background:color-mix(in srgb, var(--surface-white) 94%, transparent); box-shadow:0 14px 34px color-mix(in srgb, var(--text-primary) 16%, transparent), 0 2px 8px color-mix(in srgb, var(--text-primary) 8%, transparent); backdrop-filter:blur(18px); }
    .mobile-shell__nav a { position:relative; display:grid; justify-items:center; align-content:center; grid-template-rows:30px auto; gap:3px; min-height:56px; padding:4px 2px 5px; border-radius:16px; color:var(--text-secondary); font-size:11px; line-height:1.2; text-decoration:none; transition:color .16s ease, transform .16s ease, background .16s ease, box-shadow .16s ease; }
    .mobile-shell__nav a:active { transform:scale(.96); }
    .mobile-shell__nav a.active { color:var(--color-primary); font-weight:var(--font-weight-semibold); background:color-mix(in srgb, var(--color-primary-soft) 84%, var(--surface-white)); box-shadow:inset 0 0 0 1px color-mix(in srgb, var(--color-primary) 13%, transparent), 0 4px 12px color-mix(in srgb, var(--color-primary) 10%, transparent); }
    .mobile-shell__nav a.active::before { content:''; position:absolute; top:0; left:50%; z-index:1; width:8px; height:8px; border-radius:50% 50% 50% 0; background:var(--color-primary); transform:translateX(-50%) rotate(-45deg); transform-origin:center; animation:hh-nav-droplet 1.8s ease-in-out infinite; }
    .mobile-shell__nav a.active::after { content:''; position:absolute; right:50%; top:2px; width:22px; height:3px; border-radius:999px; background:var(--color-primary); transform:translateX(50%); }
    .mobile-shell__nav a hh-mobile-icon { width:36px; height:28px; border-radius:10px; color:currentColor; transition:background .16s ease, color .16s ease; }
    .mobile-shell__nav a.active hh-mobile-icon { background:var(--color-primary); color:var(--surface-white); box-shadow:0 4px 12px color-mix(in srgb, var(--color-primary) 28%, transparent); }
    .mobile-shell__nav a:focus-visible, .header-actions button:focus-visible { outline:3px solid color-mix(in srgb, var(--color-primary) 35%, transparent); outline-offset:2px; }
    @keyframes hh-nav-droplet { 0%,100% { margin-left:-9px; opacity:.7; } 50% { margin-left:9px; opacity:1; } }
    @media (prefers-reduced-motion: reduce) { .mobile-shell__nav a.active::before { animation:none; } .mobile-shell__nav a { transition:none; } }
    @media (max-width:380px) { .mobile-shell__header { padding-inline:10px; } .header-actions { gap:3px; } .header-actions .hh-icon-button { flex-basis:36px; width:36px; min-width:36px; } .mobile-shell__nav { width:calc(100% - 16px); padding:6px; border-radius:20px; } .mobile-shell__nav a { font-size:10px; } }
  `],
})
export class MobileShellComponent implements OnInit {
  readonly auth = inject(MobileAuthService);
  readonly native = inject(NativeCapabilityService);
    biometricAvailable = false;
    menuOpen = false;
  private readonly api = inject(MobileAdminApiService);
  private readonly permissions = inject(HisHopePermissionService);
  private readonly theme = inject(HisHopeThemeService);
  constructor() {
    this.api.getMyPermissions().pipe(catchError(() => of(null))).subscribe(snapshot => {
      if (snapshot) this.permissions.setPermissions(snapshot.permissions);
    });
  }
  async ngOnInit(): Promise<void> { this.biometricAvailable = await this.native.biometricAvailable(); }
  async unlock(): Promise<void> { await this.auth.unlockWithBiometric(); }
  logout(): void { this.auth.logout(); }
  toggleTheme(): void { this.theme.setTheme(this.theme.theme() === 'dark' ? 'light' : 'dark'); }
}
