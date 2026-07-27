import { ChangeDetectorRef, Component, OnInit, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { HisHopeStateComponent, HisHopeToolbarComponent } from '@his-hope/frontend-foundation';
import { catchError, finalize, of } from 'rxjs';
import { MobileAdminApiService, MobileDashboardStats } from '../core/admin-api.service';

@Component({
  standalone: true,
  imports: [RouterLink, HisHopeStateComponent, HisHopeToolbarComponent],
  template: `
    <section class="mobile-page">
      <hh-toolbar label="Dashboard controls"><span hhToolbarTitle>Overview</span><button hh-toolbar-actions class="hh-icon-button" type="button" (click)="load()" aria-label="Refresh dashboard"><span class="material-icons">refresh</span></button></hh-toolbar>
      @if (loading) { <hh-state kind="loading" message="Loading dashboard..." /> }
      @else if (error) { <hh-state kind="error" [message]="error"><button class="hh-button hh-button--secondary" type="button" (click)="load()">Retry</button></hh-state> }
      @else if (stats) {
        <div class="stats-grid">
          <a class="stat-card" routerLink="/admin/clients"><span class="material-icons">vpn_key</span><strong>{{ stats.totalClients }}</strong><span>Clients</span></a>
          <a class="stat-card" routerLink="/admin/users"><span class="material-icons">people</span><strong>{{ stats.totalUsers }}</strong><span>Users</span></a>
          <a class="stat-card" routerLink="/admin/roles"><span class="material-icons">badge</span><strong>{{ stats.totalRoles }}</strong><span>Roles</span></a>
          <a class="stat-card" routerLink="/admin/consents"><span class="material-icons">checklist</span><strong>{{ stats.totalConsents }}</strong><span>Consents</span></a>
        </div>
        <section class="quick-actions" aria-labelledby="quick-actions-title"><h2 id="quick-actions-title">Quick access</h2><a routerLink="/admin/clients">Manage clients <span class="material-icons">arrow_forward</span></a><a routerLink="/admin/users">Manage users <span class="material-icons">arrow_forward</span></a></section>
      }
    </section>
  `,
  styles: [`
    :host { display:block; } .mobile-page { display:grid; gap:16px; } .stats-grid { display:grid; grid-template-columns:repeat(2,minmax(0,1fr)); gap:12px; } .stat-card { display:grid; gap:8px; min-height:132px; padding:16px; box-sizing:border-box; border:1px solid var(--border-default); border-radius:var(--radius-card); background:var(--surface-white); color:var(--text-primary); text-decoration:none; } .stat-card .material-icons { color:var(--color-primary); } .stat-card strong { font-size:28px; } .stat-card span:last-child { color:var(--text-secondary); } .quick-actions { display:grid; gap:8px; padding:16px; border:1px solid var(--border-default); border-radius:var(--radius-card); background:var(--surface-white); } h2 { margin:0 0 4px; font-size:18px; } .quick-actions a { display:flex; align-items:center; justify-content:space-between; min-height:44px; border-bottom:1px solid var(--border-light); color:var(--text-primary); text-decoration:none; } .quick-actions a:last-child { border-bottom:0; }
  `],
})
export class MobileDashboardComponent implements OnInit {
  private readonly api = inject(MobileAdminApiService);
  private readonly changeDetector = inject(ChangeDetectorRef);
  stats: MobileDashboardStats | null = null; loading = false; error = '';
  ngOnInit(): void { this.load(); }
  load(): void { this.loading = true; this.error = ''; this.changeDetector.detectChanges(); this.api.getDashboard().pipe(finalize(() => { this.loading = false; this.changeDetector.detectChanges(); }), catchError(() => { this.error = 'Unable to load dashboard.'; return of(null); })).subscribe(value => { this.stats = value; this.changeDetector.detectChanges(); }); }
}
