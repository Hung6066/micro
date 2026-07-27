import { ChangeDetectorRef, Component, OnInit, inject } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { HisHopeBulkAction, HisHopeBulkActionRequest, HisHopeDataTableColumn, HisHopeDataTableComponent, HisHopeDataTableDetailDirective, HisHopeMobileActionSheetComponent, HisHopeMobileBottomSheetComponent, HisHopeMobileIconComponent, HisHopeMobileInfiniteListComponent, HisHopeMobileRefresherComponent, HisHopeMobileSearchbarComponent, HisHopePageQuery, HisHopeTableExportRequest, HisHopeToolbarComponent } from '@his-hope/frontend-foundation';
import { catchError, finalize, of } from 'rxjs';
import { MobileAdminApiService, MobileClient, MobileConsent, MobileResource, MobileRole, MobileUser } from '../core/admin-api.service';

type MobileRow = Record<string, unknown>;

@Component({
  standalone: true,
  imports: [HisHopeDataTableComponent, HisHopeDataTableDetailDirective, HisHopeMobileActionSheetComponent, HisHopeMobileBottomSheetComponent, HisHopeMobileIconComponent, HisHopeMobileInfiniteListComponent, HisHopeMobileRefresherComponent, HisHopeMobileSearchbarComponent, HisHopeToolbarComponent],
  template: `
    <section class="mobile-page" [attr.aria-busy]="loading">
      <hh-toolbar class="mobile-page__toolbar" [label]="title + ' controls'">
        <span hhToolbarTitle>{{ totalItems }} {{ title.toLowerCase() }}</span>
        <button hh-toolbar-actions class="hh-icon-button mobile-page__refresh" type="button" (click)="load()" [disabled]="loading" [attr.aria-label]="'Refresh ' + title" [attr.title]="'Refresh ' + title">
          <hh-mobile-icon name="refresh" [class.mobile-page__refresh--spinning]="loading" />
        </button>
        <button hh-toolbar-actions class="hh-icon-button" type="button" aria-label="More actions" title="More actions" (click)="actionsOpen = true"><hh-mobile-icon name="more" /></button>
      </hh-toolbar>
      <hh-mobile-searchbar [value]="query.search ?? ''" [placeholder]="'Search ' + title.toLowerCase()" (valueChange)="search($event)" />
      <hh-mobile-refresher (refreshed)="load()">
        <hh-mobile-infinite-list [label]="title" [loading]="loadingMore" [hasMore]="hasMore" [loadedCount]="rows.length" [totalCount]="totalItems" [nextCursor]="nextCursor || ''" (loadMoreRequested)="loadMore($event.cursor)">
          <hh-data-table [label]="title" [columns]="columns" [rows]="rows" [loading]="loading" [error]="error" [empty]="!loading && !error && rows.length === 0" [emptyMessage]="emptyMessage" [selection]="resource !== 'consents'" [mobilePresentation]="'list'" [rowClickable]="true" [searchable]="false" mode="server" [totalItems]="totalItems" [query]="query" [pageSize]="20" [urlSync]="false" [bulkActions]="bulkActions" [filterBuilder]="true" [mobileLoadMore]="false" (queryChange)="load($event)" (rowClick)="selectRow($event)" (bulkActionRequested)="bulk($event)" (exportRequested)="exportRows($event)" (retry)="load()">
            <ng-template hhDataTableDetail let-row><div class="detail">{{ detail(row) }}</div></ng-template>
          </hh-data-table>
        </hh-mobile-infinite-list>
      </hh-mobile-refresher>
      <hh-mobile-action-sheet [open]="actionsOpen" label="{{ title }} actions" (close)="actionsOpen = false"><button type="button" class="hh-mobile-sheet-action" (click)="actionsOpen = false; load()"><hh-mobile-icon name="refresh" />Refresh list</button></hh-mobile-action-sheet>
      <hh-mobile-bottom-sheet [open]="!!selectedRow" label="{{ title }} details" (close)="selectedRow = null">@if (selectedRow; as row) { <dl class="detail-list">@for (entry of detailEntries(row); track entry[0]) { <div><dt>{{ entry[0] }}</dt><dd>{{ entry[1] }}</dd></div> }</dl> }</hh-mobile-bottom-sheet>
    </section>
  `,
  styles: [`
    :host { display:block; }
    .mobile-page { display:grid; gap:12px; }
    :host ::ng-deep .mobile-page__toolbar .hh-toolbar { flex-wrap:nowrap; gap:8px; margin-bottom:0; }
    :host ::ng-deep .mobile-page__toolbar .hh-toolbar__title { flex:1 1 auto; min-width:0; }
    :host ::ng-deep .mobile-page__toolbar .hh-toolbar__actions { flex:0 0 auto; width:auto; margin-left:auto; }
    .mobile-page__toolbar { position:sticky; top:calc(56px + env(safe-area-inset-top)); z-index:5; min-width:0; padding-block:4px; background:color-mix(in srgb, var(--bg-warm) 94%, transparent); backdrop-filter:blur(12px); }
    .mobile-page__refresh { flex:0 0 44px; width:44px; min-width:44px; min-height:44px; padding:0; }
    .mobile-page__refresh:disabled { opacity:.55; cursor:wait; }
    .mobile-page__refresh--spinning { animation:mobile-refresh-spin .8s linear infinite; }
    .detail { color:var(--text-secondary); line-height:1.5; }.detail-list { display:grid; gap:12px; margin:0; }.detail-list div { display:grid; gap:3px; }.detail-list dt { color:var(--text-muted); font-size:12px; }.detail-list dd { margin:0; color:var(--text-primary); overflow-wrap:anywhere; }.hh-mobile-sheet-action { display:flex; align-items:center; gap:12px; min-height:48px; padding:0 8px; border:0; border-bottom:1px solid var(--border-light); background:transparent; color:var(--text-primary); font:inherit; text-align:left; }
    @keyframes mobile-refresh-spin { to { transform:rotate(360deg); } }
  `],
})
export class MobileResourcePageComponent implements OnInit {
  private readonly route = inject(ActivatedRoute); private readonly api = inject(MobileAdminApiService); private readonly changeDetector = inject(ChangeDetectorRef);
  actionsOpen = false; selectedRow: MobileRow | null = null;
  resource: MobileResource = 'clients'; title = 'Clients'; emptyMessage = 'No records found.'; columns: HisHopeDataTableColumn[] = []; rows: MobileRow[] = []; totalItems = 0; loading = false; loadingMore = false; error = ''; loadMoreError = ''; hasMore = false; nextCursor: string | null = null; query: HisHopePageQuery = { page: 1, pageSize: 20 };
  private requestSequence = 0;
  readonly bulkActions: HisHopeBulkAction[] = [{ id: 'activate', label: 'Activate selected', icon: 'check_circle', permission: 'admin.users.write' }, { id: 'deactivate', label: 'Deactivate selected', icon: 'block', tone: 'danger', permission: 'admin.users.write' }];
  ngOnInit(): void { this.resource = this.route.snapshot.data['resource'] as MobileResource; this.configure(); this.load(); }
  search(value: string): void { this.load({ ...this.query, page: 1, cursor: undefined, search: value || undefined }); }
  selectRow(row: MobileRow): void { this.selectedRow = row; }
  detailEntries(row: MobileRow): Array<[string, string]> { return Object.entries(row).filter(([key]) => key !== 'id').map(([key, value]) => [key, String(value ?? '')]); }
  configure(): void {
    const configs: Record<MobileResource, { title: string; columns: HisHopeDataTableColumn[]; empty: string }> = {
      clients: { title: 'Clients', empty: 'No OIDC clients found.', columns: [{ key: 'clientId', label: 'Client ID', sortable: true, responsivePriority: 1 }, { key: 'displayName', label: 'Display name', sortable: true, responsivePriority: 1 }, { key: 'clientType', label: 'Type', responsivePriority: 2 }, { key: 'redirectUris', label: 'Redirect URIs', responsivePriority: 3 }] },
      users: { title: 'Users', empty: 'No users found.', columns: [{ key: 'userName', label: 'Username', sortable: true, responsivePriority: 1 }, { key: 'email', label: 'Email', sortable: true, responsivePriority: 2 }, { key: 'roles', label: 'Roles', responsivePriority: 3 }, { key: 'isActive', label: 'Active', responsivePriority: 2, status: true }] },
      roles: { title: 'Roles', empty: 'No roles found.', columns: [{ key: 'name', label: 'Name', sortable: true, responsivePriority: 1 }, { key: 'description', label: 'Description', responsivePriority: 2 }] },
      consents: { title: 'Consents', empty: 'No consents recorded.', columns: [{ key: 'subject', label: 'Subject', sortable: true, responsivePriority: 1 }, { key: 'clientId', label: 'Client ID', responsivePriority: 2 }, { key: 'scopes', label: 'Scopes', responsivePriority: 3 }, { key: 'created', label: 'Created', sortable: true, responsivePriority: 2 }] },
    };
    const config = configs[this.resource]; this.title = config.title; this.emptyMessage = config.empty; this.columns = config.columns;
  }
  load(query = this.query): void {
    const sequence = ++this.requestSequence;
    this.query = { ...query, page: query.page || 1, pageSize: query.pageSize || 20, cursor: query.cursor || undefined };
    this.loading = true;
    this.loadingMore = false;
    this.error = '';
    this.loadMoreError = '';
    this.hasMore = false;
    this.nextCursor = null;
    this.rows = this.query.cursor || this.query.page > 1 ? this.rows : [];
    this.changeDetector.detectChanges();
    this.api.getPage<MobileClient | MobileUser | MobileRole | MobileConsent>(this.resource, this.query).pipe(
      finalize(() => { if (sequence === this.requestSequence) { this.loading = false; this.changeDetector.detectChanges(); } }),
      catchError(() => { if (sequence === this.requestSequence) this.error = `Unable to load ${this.title.toLowerCase()}.`; return of(null); })
    ).subscribe(result => {
      if (!result || sequence !== this.requestSequence) { this.changeDetector.detectChanges(); return; }
      this.totalItems = result.totalCount;
      this.rows = result.items.map(item => this.toRow(item));
      this.nextCursor = result.nextCursor ?? null;
      this.hasMore = !!result.nextCursor || result.hasNextPage;
      this.changeDetector.detectChanges();
    });
  }
  loadMore(cursor: string | null = this.nextCursor): void {
    if (this.loading || this.loadingMore || !this.hasMore) return;
    const nextQuery: HisHopePageQuery = cursor
      ? { ...this.query, page: 1, cursor }
      : { ...this.query, page: (this.query.page || 1) + 1, cursor: undefined };
    const sequence = ++this.requestSequence;
    this.loadingMore = true;
    this.loadMoreError = '';
    this.changeDetector.detectChanges();
    this.api.getPage<MobileClient | MobileUser | MobileRole | MobileConsent>(this.resource, nextQuery).pipe(
      finalize(() => { if (sequence === this.requestSequence) { this.loadingMore = false; this.changeDetector.detectChanges(); } }),
      catchError(() => { if (sequence === this.requestSequence) this.loadMoreError = `Unable to load more ${this.title.toLowerCase()}.`; return of(null); })
    ).subscribe(result => {
      if (!result || sequence !== this.requestSequence) { this.changeDetector.detectChanges(); return; }
      this.query = { ...nextQuery, cursor: result.nextCursor || undefined };
      this.totalItems = result.totalCount;
      this.nextCursor = result.nextCursor ?? null;
      this.rows = [...this.rows, ...result.items.map(item => this.toRow(item))];
      this.hasMore = !!result.nextCursor || result.hasNextPage;
      this.changeDetector.detectChanges();
    });
  }
  toRow(item: MobileClient | MobileUser | MobileRole | MobileConsent): MobileRow { if (this.resource === 'clients') { const value = item as MobileClient; return { id: value.id ?? value.clientId, clientId: value.clientId, displayName: value.displayName, clientType: value.clientType, redirectUris: value.redirectUris.join(', ') }; } if (this.resource === 'users') { const value = item as MobileUser; return { id: value.id, userName: value.userName, email: value.email, roles: value.roles.join(', '), isActive: value.isActive ? 'Yes' : 'No' }; } if (this.resource === 'roles') { const value = item as MobileRole; return { id: value.id, name: value.name, description: value.description ?? '' }; } const value = item as MobileConsent; return { id: value.id, subject: value.subject, clientId: value.clientId, scopes: value.scopes.join(', '), created: new Date(value.created).toLocaleDateString() }; }
  detail(row: MobileRow): string { return Object.entries(row).filter(([key]) => key !== 'id').map(([key, value]) => `${key}: ${String(value ?? '')}`).join(' · '); }
  bulk(request: HisHopeBulkActionRequest): void { if (this.resource === 'consents') return; this.loading = true; this.api.bulk(this.resource, request.actionId, request.rowKeys).pipe(finalize(() => this.loading = false), catchError(() => { this.error = 'Bulk action failed.'; return of(null); })).subscribe(result => { if (result) this.load(); }); }
  exportRows(_request: HisHopeTableExportRequest): void { this.error = 'Export is available from the desktop admin workspace.'; }
}
