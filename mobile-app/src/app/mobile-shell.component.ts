import { Component, inject } from '@angular/core';
import { RouterLink, RouterOutlet } from '@angular/router';
import { HisHopeBrandComponent, HisHopeLanguageSwitcherComponent, HisHopePermissionService, HisHopeThemeService } from '@his-hope/frontend-foundation';
import { MobileAuthService } from './core/auth.service';
import { MobileAdminApiService } from './core/admin-api.service';
import { catchError, of } from 'rxjs';

@Component({
  standalone: true,
  imports: [RouterLink, RouterOutlet, HisHopeBrandComponent, HisHopeLanguageSwitcherComponent],
  template: `
    <main class="mobile-shell">
      <header class="mobile-shell__header"><hh-brand caption="Identity administration" /><div class="header-actions"><hh-language-switcher /><button class="hh-icon-button" type="button" (click)="toggleTheme()" aria-label="Toggle theme"><span class="material-icons">contrast</span></button><button class="hh-icon-button" type="button" (click)="logout()" aria-label="Sign out"><span class="material-icons">logout</span></button></div></header>
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
    .mobile-shell__header { display:flex; align-items:center; justify-content:space-between; gap:12px; padding:max(12px, env(safe-area-inset-top)) 16px 10px; border-bottom:1px solid var(--border-light); background:var(--surface-white); } .header-actions { display:flex; align-items:center; gap:6px; min-width:0; } .header-actions .hh-icon-button { flex:0 0 40px; } .mobile-shell__content { width:min(100% - 24px, 720px); margin:0 auto; padding:16px 0 96px; } .mobile-shell__nav { position:fixed; right:0; bottom:0; left:0; z-index:10; display:grid; grid-template-columns:repeat(5,1fr); padding:8px max(8px, env(safe-area-inset-right)) max(8px, env(safe-area-inset-bottom)) max(8px, env(safe-area-inset-left)); border-top:1px solid var(--border-default); background:color-mix(in srgb, var(--surface-white) 94%, transparent); backdrop-filter:blur(12px); } .mobile-shell__nav a { display:grid; justify-items:center; gap:3px; min-height:48px; padding:4px 2px; border-radius:var(--radius-control); color:var(--text-secondary); font-size:11px; text-decoration:none; } .mobile-shell__nav a.active { background:var(--color-primary-soft); color:var(--color-primary); font-weight:var(--font-weight-semibold); } .mobile-shell__nav .material-icons { font-size:20px; } @media (max-width:380px) { .mobile-shell__header { padding-inline:10px; } .header-actions { gap:2px; } .header-actions .hh-icon-button { flex-basis:36px; width:36px; } }
  `],
})
export class MobileShellComponent {
  readonly auth = inject(MobileAuthService);
  private readonly api = inject(MobileAdminApiService);
  private readonly permissions = inject(HisHopePermissionService);
  private readonly theme = inject(HisHopeThemeService);
  constructor() {
    this.api.getMyPermissions().pipe(catchError(() => of(null))).subscribe(snapshot => {
      if (snapshot) this.permissions.setPermissions(snapshot.permissions);
    });
  }
  logout(): void { this.auth.logout(); }
  toggleTheme(): void { this.theme.setTheme(this.theme.theme() === 'dark' ? 'light' : 'dark'); }
}
