import { ChangeDetectorRef, Component, OnInit, inject } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { HisHopeBulkAction, HisHopeBulkActionRequest, HisHopeDataTableColumn, HisHopeDataTableComponent, HisHopeDataTableDetailDirective, HisHopePageQuery, HisHopeTableExportRequest, HisHopeToolbarComponent } from '@his-hope/frontend-foundation';
import { catchError, finalize, of } from 'rxjs';
import { MobileAdminApiService, MobileClient, MobileConsent, MobileResource, MobileRole, MobileUser } from '../core/admin-api.service';

type MobileRow = Record<string, unknown>;

@Component({
  standalone: true,
  imports: [HisHopeDataTableComponent, HisHopeDataTableDetailDirective, HisHopeToolbarComponent],
  template: `
    <section class="mobile-page" [attr.aria-busy]="loading" (touchstart)="onTouchStart($event)" (touchmove)="onTouchMove($event)" (touchend)="onTouchEnd()">
      @if (pullDistance > 0) { <div class="mobile-page__pull" role="status" aria-live="polite"><span class="material-icons" [class.mobile-page__pull--ready]="pullDistance >= 72" aria-hidden="true">south</span>{{ pullDistance >= 72 ? 'Release to refresh' : 'Pull to refresh' }}</div> }
      <hh-toolbar class="mobile-page__toolbar" [label]="title + ' controls'">
        <span hhToolbarTitle>{{ totalItems }} {{ title.toLowerCase() }}</span>
        <button hh-toolbar-actions class="hh-icon-button mobile-page__refresh" type="button" (click)="load()" [disabled]="loading" [attr.aria-label]="'Refresh ' + title" [attr.title]="'Refresh ' + title">
          <span class="material-icons" [class.mobile-page__refresh--spinning]="loading" aria-hidden="true">refresh</span>
        </button>
      </hh-toolbar>
      <hh-data-table [label]="title" [columns]="columns" [rows]="rows" [loading]="loading" [error]="error" [empty]="!loading && !error && rows.length === 0" [emptyMessage]="emptyMessage" [selection]="resource !== 'consents'" [mobilePresentation]="'list'" [rowClickable]="true" mode="server" [totalItems]="totalItems" [query]="query" [pageSize]="20" [urlSync]="false" [bulkActions]="bulkActions" [filterBuilder]="true" (queryChange)="load($event)" (bulkActionRequested)="bulk($event)" (exportRequested)="exportRows($event)" (retry)="load()">
        <ng-template hhDataTableDetail let-row><div class="detail">{{ detail(row) }}</div></ng-template>
      </hh-data-table>
    </section>
  `,
  styles: [`
    :host { display:block; }
    .mobile-page { display:grid; gap:12px; }
    :host ::ng-deep .mobile-page__toolbar .hh-toolbar { flex-wrap:nowrap; gap:8px; margin-bottom:0; }
    :host ::ng-deep .mobile-page__toolbar .hh-toolbar__title { flex:1 1 auto; min-width:0; }
    :host ::ng-deep .mobile-page__toolbar .hh-toolbar__actions { flex:0 0 auto; width:auto; margin-left:auto; }
    .mobile-page__pull { display:flex; align-items:center; justify-content:center; gap:6px; min-height:28px; color:var(--text-secondary); font-size:12px; }
    .mobile-page__pull .material-icons { font-size:18px; transition:transform .16s ease; }
    .mobile-page__pull--ready { transform:rotate(180deg); color:var(--color-primary); }
    .mobile-page__toolbar { position:sticky; top:calc(56px + env(safe-area-inset-top)); z-index:5; min-width:0; padding-block:4px; background:color-mix(in srgb, var(--bg-warm) 94%, transparent); backdrop-filter:blur(12px); }
    .mobile-page__refresh { flex:0 0 44px; width:44px; min-width:44px; min-height:44px; padding:0; }
    .mobile-page__refresh:disabled { opacity:.55; cursor:wait; }
    .mobile-page__refresh--spinning { animation:mobile-refresh-spin .8s linear infinite; }
    .detail { color:var(--text-secondary); line-height:1.5; }
    @keyframes mobile-refresh-spin { to { transform:rotate(360deg); } }
  `],
})
export class MobileResourcePageComponent implements OnInit {
  private readonly route = inject(ActivatedRoute); private readonly api = inject(MobileAdminApiService); private readonly changeDetector = inject(ChangeDetectorRef);
  private touchStartY: number | null = null;
  pullDistance = 0;
  resource: MobileResource = 'clients'; title = 'Clients'; emptyMessage = 'No records found.'; columns: HisHopeDataTableColumn[] = []; rows: MobileRow[] = []; totalItems = 0; loading = false; error = ''; query: HisHopePageQuery = { page: 1, pageSize: 20 };
  readonly bulkActions: HisHopeBulkAction[] = [{ id: 'activate', label: 'Activate selected', icon: 'check_circle', permission: 'admin.users.write' }, { id: 'deactivate', label: 'Deactivate selected', icon: 'block', tone: 'danger', permission: 'admin.users.write' }];
  ngOnInit(): void { this.resource = this.route.snapshot.data['resource'] as MobileResource; this.configure(); this.load(); }
  onTouchStart(event: TouchEvent): void { if (window.scrollY <= 0 && event.touches.length === 1) this.touchStartY = event.touches[0].clientY; }
  onTouchMove(event: TouchEvent): void {
    if (this.touchStartY === null || window.scrollY > 0 || event.touches.length !== 1) return;
    const distance = event.touches[0].clientY - this.touchStartY;
    if (distance > 0) this.pullDistance = Math.min(96, distance);
  }
  onTouchEnd(): void {
    const shouldRefresh = this.pullDistance >= 72 && !this.loading;
    this.touchStartY = null;
    this.pullDistance = 0;
    if (shouldRefresh) this.load();
  }
  configure(): void {
    const configs: Record<MobileResource, { title: string; columns: HisHopeDataTableColumn[]; empty: string }> = {
      clients: { title: 'Clients', empty: 'No OIDC clients found.', columns: [{ key: 'clientId', label: 'Client ID', sortable: true, responsivePriority: 1 }, { key: 'displayName', label: 'Display name', sortable: true, responsivePriority: 1 }, { key: 'clientType', label: 'Type', responsivePriority: 2 }, { key: 'redirectUris', label: 'Redirect URIs', responsivePriority: 3 }] },
      users: { title: 'Users', empty: 'No users found.', columns: [{ key: 'userName', label: 'Username', sortable: true, responsivePriority: 1 }, { key: 'email', label: 'Email', sortable: true, responsivePriority: 2 }, { key: 'roles', label: 'Roles', responsivePriority: 3 }, { key: 'isActive', label: 'Active', responsivePriority: 2, status: true }] },
      roles: { title: 'Roles', empty: 'No roles found.', columns: [{ key: 'name', label: 'Name', sortable: true, responsivePriority: 1 }, { key: 'description', label: 'Description', responsivePriority: 2 }] },
      consents: { title: 'Consents', empty: 'No consents recorded.', columns: [{ key: 'subject', label: 'Subject', sortable: true, responsivePriority: 1 }, { key: 'clientId', label: 'Client ID', responsivePriority: 2 }, { key: 'scopes', label: 'Scopes', responsivePriority: 3 }, { key: 'created', label: 'Created', sortable: true, responsivePriority: 2 }] },
    };
    const config = configs[this.resource]; this.title = config.title; this.emptyMessage = config.empty; this.columns = config.columns;
  }
  load(query = this.query): void { this.query = query; this.loading = true; this.error = ''; this.changeDetector.detectChanges(); this.api.getPage<MobileClient | MobileUser | MobileRole | MobileConsent>(this.resource, query).pipe(finalize(() => { this.loading = false; this.changeDetector.detectChanges(); }), catchError(() => { this.error = `Unable to load ${this.title.toLowerCase()}.`; return of(null); })).subscribe(result => { if (!result) { this.changeDetector.detectChanges(); return; } this.totalItems = result.totalCount; this.rows = result.items.map(item => this.toRow(item)); this.changeDetector.detectChanges(); }); }
  toRow(item: MobileClient | MobileUser | MobileRole | MobileConsent): MobileRow { if (this.resource === 'clients') { const value = item as MobileClient; return { id: value.id ?? value.clientId, clientId: value.clientId, displayName: value.displayName, clientType: value.clientType, redirectUris: value.redirectUris.join(', ') }; } if (this.resource === 'users') { const value = item as MobileUser; return { id: value.id, userName: value.userName, email: value.email, roles: value.roles.join(', '), isActive: value.isActive ? 'Yes' : 'No' }; } if (this.resource === 'roles') { const value = item as MobileRole; return { id: value.id, name: value.name, description: value.description ?? '' }; } const value = item as MobileConsent; return { id: value.id, subject: value.subject, clientId: value.clientId, scopes: value.scopes.join(', '), created: new Date(value.created).toLocaleDateString() }; }
  detail(row: MobileRow): string { return Object.entries(row).filter(([key]) => key !== 'id').map(([key, value]) => `${key}: ${String(value ?? '')}`).join(' · '); }
  bulk(request: HisHopeBulkActionRequest): void { if (this.resource === 'consents') return; this.loading = true; this.api.bulk(this.resource, request.actionId, request.rowKeys).pipe(finalize(() => this.loading = false), catchError(() => { this.error = 'Bulk action failed.'; return of(null); })).subscribe(result => { if (result) this.load(); }); }
  exportRows(_request: HisHopeTableExportRequest): void { this.error = 'Export is available from the desktop admin workspace.'; }
}
