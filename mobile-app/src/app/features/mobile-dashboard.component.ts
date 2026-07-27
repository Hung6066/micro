import { ChangeDetectorRef, Component, OnInit, inject } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { HisHopeMobileListComponent, HisHopeMobileListItemComponent, HisHopeStateComponent, HisHopeToolbarComponent } from '@his-hope/frontend-foundation';
import { catchError, finalize, of } from 'rxjs';
import { MobileAdminApiService, MobileDashboardStats } from '../core/admin-api.service';

@Component({
  standalone: true,
  imports: [RouterLink, HisHopeMobileListComponent, HisHopeMobileListItemComponent, HisHopeStateComponent, HisHopeToolbarComponent],
  template: `
    <section class="mobile-page">
      <hh-toolbar label="Dashboard controls"><span hhToolbarTitle>Overview</span><button hh-toolbar-actions class="hh-icon-button" type="button" (click)="load()" aria-label="Refresh dashboard"><span class="material-icons">refresh</span></button></hh-toolbar>
      @if (loading) { <hh-state kind="loading" message="Loading dashboard..." /> }
      @else if (error) { <hh-state kind="error" [message]="error"><button class="hh-button hh-button--secondary" type="button" (click)="load()">Retry</button></hh-state> }
      @else if (stats) {
        <section class="welcome-panel" aria-labelledby="welcome-title"><div><p class="eyebrow">IDENTITY ADMINISTRATION</p><h1 id="welcome-title">Good to see you</h1><p>Manage access, applications and consent from one secure workspace.</p></div><span class="welcome-icon material-icons" aria-hidden="true">verified_user</span></section>
        <div class="stats-grid" aria-label="Administration summary">
          <a class="stat-card" routerLink="/admin/clients"><span class="material-icons">vpn_key</span><strong>{{ stats.totalClients }}</strong><span>Clients</span></a>
          <a class="stat-card" routerLink="/admin/users"><span class="material-icons">people</span><strong>{{ stats.totalUsers }}</strong><span>Users</span></a>
          <a class="stat-card" routerLink="/admin/roles"><span class="material-icons">badge</span><strong>{{ stats.totalRoles }}</strong><span>Roles</span></a>
          <a class="stat-card" routerLink="/admin/consents"><span class="material-icons">checklist</span><strong>{{ stats.totalConsents }}</strong><span>Consents</span></a>
        </div>
        <section class="quick-actions" aria-labelledby="quick-actions-title"><div class="section-heading"><h2 id="quick-actions-title">Quick access</h2><span>Common tasks</span></div><hh-mobile-list label="Quick access"><hh-mobile-list-item variant="action" (activated)="navigate('/admin/clients')"><span hhMobileItemLeading class="action-icon material-icons">vpn_key</span><span hhMobileItemTitle>Manage clients</span><span hhMobileItemDescription>OIDC applications and redirect URIs</span><span hhMobileItemTrailing class="material-icons arrow">arrow_forward</span></hh-mobile-list-item><hh-mobile-list-item variant="action" (activated)="navigate('/admin/users')"><span hhMobileItemLeading class="action-icon material-icons">people</span><span hhMobileItemTitle>Manage users</span><span hhMobileItemDescription>Accounts, roles and access</span><span hhMobileItemTrailing class="material-icons arrow">arrow_forward</span></hh-mobile-list-item><hh-mobile-list-item variant="action" (activated)="navigate('/admin/consents')"><span hhMobileItemLeading class="action-icon material-icons">checklist</span><span hhMobileItemTitle>Review consents</span><span hhMobileItemDescription>Recent user approvals</span><span hhMobileItemTrailing class="material-icons arrow">arrow_forward</span></hh-mobile-list-item></hh-mobile-list></section>
      }
    </section>
  `,
  styles: [`
    :host { display:block; } .mobile-page { display:grid; gap:16px; } .welcome-panel { display:flex; align-items:center; justify-content:space-between; gap:16px; padding:20px; border:1px solid color-mix(in srgb, var(--color-primary) 20%, var(--border-default)); border-radius:var(--radius-card); background:linear-gradient(135deg, color-mix(in srgb, var(--color-primary-soft) 82%, var(--surface-white)), var(--surface-white)); } .welcome-panel h1 { margin:4px 0 6px; font-size:24px; letter-spacing:-.01em; } .welcome-panel p:not(.eyebrow) { max-width:34ch; margin:0; color:var(--text-secondary); line-height:1.45; } .eyebrow { margin:0; color:var(--color-primary); font-size:11px; font-weight:var(--font-weight-semibold); letter-spacing:.08em; } .welcome-icon { flex:0 0 48px; width:48px; height:48px; display:grid; place-items:center; border-radius:16px; background:var(--surface-white); color:var(--color-primary); font-size:26px; box-shadow:0 6px 18px color-mix(in srgb, var(--color-primary) 14%, transparent); } .stats-grid { display:grid; grid-template-columns:repeat(2,minmax(0,1fr)); gap:12px; } .stat-card { display:grid; gap:8px; min-height:132px; padding:16px; box-sizing:border-box; border:1px solid var(--border-default); border-radius:var(--radius-card); background:var(--surface-white); color:var(--text-primary); text-decoration:none; transition:transform .16s ease, border-color .16s ease, box-shadow .16s ease; } .stat-card:active { transform:scale(.98); } .stat-card .material-icons { color:var(--color-primary); } .stat-card strong { font-size:28px; line-height:1; } .stat-card span:last-child { color:var(--text-secondary); } .quick-actions { display:grid; gap:0; padding:16px; border:1px solid var(--border-default); border-radius:var(--radius-card); background:var(--surface-white); } .section-heading { display:flex; align-items:baseline; justify-content:space-between; gap:12px; margin-bottom:6px; } h2 { margin:0; font-size:18px; } .section-heading span { color:var(--text-secondary); font-size:12px; } .quick-actions a { display:flex; align-items:center; gap:12px; min-height:64px; border-bottom:1px solid var(--border-light); color:var(--text-primary); text-decoration:none; } .quick-actions a:last-child { border-bottom:0; } .action-icon { display:grid; place-items:center; width:36px; height:36px; border-radius:12px; background:var(--color-primary-soft); color:var(--color-primary); font-size:20px; } .action-copy { display:grid; gap:3px; flex:1; } .action-copy strong { font-size:14px; } .action-copy small { color:var(--text-secondary); font-size:12px; } .arrow { color:var(--text-secondary); font-size:20px; }
  `],
})
export class MobileDashboardComponent implements OnInit {
  private readonly api = inject(MobileAdminApiService);
  private readonly router = inject(Router);
  private readonly changeDetector = inject(ChangeDetectorRef);
  stats: MobileDashboardStats | null = null; loading = false; error = '';
  ngOnInit(): void { this.load(); }
  navigate(url: string): void { void this.router.navigateByUrl(url); }
  load(): void { this.loading = true; this.error = ''; this.changeDetector.detectChanges(); this.api.getDashboard().pipe(finalize(() => { this.loading = false; this.changeDetector.detectChanges(); }), catchError(() => { this.error = 'Unable to load dashboard.'; return of(null); })).subscribe(value => { this.stats = value; this.changeDetector.detectChanges(); }); }
}
