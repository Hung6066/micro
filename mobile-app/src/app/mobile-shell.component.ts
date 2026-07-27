import { Component, OnInit, inject } from '@angular/core';
import { RouterLink, RouterOutlet } from '@angular/router';
import { HisHopeBrandComponent, HisHopeLanguageSwitcherComponent, HisHopePermissionService, HisHopeThemeService } from '@his-hope/frontend-foundation';
import { MobileAuthService } from './core/auth.service';
import { NativeCapabilityService } from './core/native-capability.service';
import { MobileAdminApiService } from './core/admin-api.service';
import { catchError, of } from 'rxjs';

@Component({
  standalone: true,
  imports: [RouterLink, RouterOutlet, HisHopeBrandComponent, HisHopeLanguageSwitcherComponent],
  template: `
    <main class="mobile-shell">
      <header class="mobile-shell__header">
        <hh-brand caption="Identity administration" />
        <div class="header-actions">
          <hh-language-switcher />
          @if (native.isNative && biometricAvailable) {
            <button class="hh-icon-button" type="button" (click)="unlock()" aria-label="Unlock with biometrics"><span class="material-icons">fingerprint</span></button>
          }
          <button class="hh-icon-button" type="button" (click)="toggleTheme()" aria-label="Toggle theme"><span class="material-icons">contrast</span></button>
          <button class="hh-icon-button" type="button" (click)="logout()" aria-label="Sign out"><span class="material-icons">logout</span></button>
        </div>
      </header>
      <section class="mobile-shell__content"><router-outlet /></section>
      <nav class="mobile-shell__nav" aria-label="Admin navigation">
        <a routerLink="/admin/dashboard" routerLinkActive="active"><span class="material-icons">dashboard</span><span>Home</span></a>
        <a routerLink="/admin/clients" routerLinkActive="active"><span class="material-icons">vpn_key</span><span>Clients</span></a>
        <a routerLink="/admin/users" routerLinkActive="active"><span class="material-icons">people</span><span>Users</span></a>
        <a routerLink="/admin/roles" routerLinkActive="active"><span class="material-icons">badge</span><span>Roles</span></a>
        <a routerLink="/admin/consents" routerLinkActive="active"><span class="material-icons">checklist</span><span>Consents</span></a>
      </nav>
    </main>
  `,
  styles: [`
    :host { display: block; min-height: 100dvh; }
    .mobile-shell { min-height: 100dvh; background: var(--bg-warm); color: var(--text-primary); }
    .mobile-shell__header { position:sticky; top:0; z-index:20; display:flex; align-items:center; justify-content:space-between; gap:12px; min-height:56px; padding:max(10px, env(safe-area-inset-top)) 16px 10px; border-bottom:1px solid var(--border-light); background:color-mix(in srgb, var(--surface-white) 94%, transparent); backdrop-filter:blur(16px); }
    .header-actions { display:flex; align-items:center; gap:8px; min-width:0; }
    .header-actions .hh-icon-button { flex:0 0 40px; min-width:40px; min-height:40px; }
    .mobile-shell__content { width:min(calc(100% - 24px), 720px); margin:0 auto; padding:16px 0 calc(112px + env(safe-area-inset-bottom)); }
    .mobile-shell__nav { position:fixed; right:0; bottom:0; left:0; z-index:10; display:grid; grid-template-columns:repeat(5,1fr); padding:8px max(8px, env(safe-area-inset-right)) max(8px, env(safe-area-inset-bottom)) max(8px, env(safe-area-inset-left)); border-top:1px solid var(--border-default); background:color-mix(in srgb, var(--surface-white) 94%, transparent); box-shadow:0 -8px 24px color-mix(in srgb, var(--text-primary) 8%, transparent); backdrop-filter:blur(16px); }
    .mobile-shell__nav a { display:grid; justify-items:center; align-content:center; gap:4px; min-height:52px; padding:5px 2px; border-radius:var(--radius-control); color:var(--text-secondary); font-size:11px; line-height:1.2; text-decoration:none; transition:background .16s ease, color .16s ease; }
    .mobile-shell__nav a.active { background:var(--color-primary-soft); color:var(--color-primary); font-weight:var(--font-weight-semibold); }
    .mobile-shell__nav a:focus-visible, .header-actions button:focus-visible { outline:3px solid color-mix(in srgb, var(--color-primary) 35%, transparent); outline-offset:2px; }
    .mobile-shell__nav .material-icons { font-size:20px; }
    @media (max-width:380px) { .mobile-shell__header { padding-inline:10px; } .header-actions { gap:3px; } .header-actions .hh-icon-button { flex-basis:36px; width:36px; min-width:36px; } .mobile-shell__nav a { font-size:10px; } }
  `],
})
export class MobileShellComponent implements OnInit {
  readonly auth = inject(MobileAuthService);
  readonly native = inject(NativeCapabilityService);
  biometricAvailable = false;
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
