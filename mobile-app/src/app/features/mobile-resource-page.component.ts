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
    <section class="mobile-page">
      <hh-toolbar [label]="title + ' controls'"><span hhToolbarTitle>{{ totalItems }} {{ title.toLowerCase() }}</span><button hh-toolbar-actions class="hh-icon-button" type="button" (click)="load()" [attr.aria-label]="'Refresh ' + title"><span class="material-icons">refresh</span></button></hh-toolbar>
      <hh-data-table [label]="title" [columns]="columns" [rows]="rows" [loading]="loading" [error]="error" [empty]="!loading && !error && rows.length === 0" [emptyMessage]="emptyMessage" [selection]="resource !== 'consents'" [mobilePresentation]="'list'" mode="server" [totalItems]="totalItems" [query]="query" [pageSize]="20" [urlSync]="false" [bulkActions]="bulkActions" [filterBuilder]="true" (queryChange)="load($event)" (bulkActionRequested)="bulk($event)" (exportRequested)="exportRows($event)" (retry)="load()">
        <ng-template hhDataTableDetail let-row><div class="detail">{{ detail(row) }}</div></ng-template>
      </hh-data-table>
    </section>
  `,
  styles: [`:host { display:block; } .mobile-page { display:grid; gap:12px; } .detail { color:var(--text-secondary); line-height:1.5; }`],
})
export class MobileResourcePageComponent implements OnInit {
  private readonly route = inject(ActivatedRoute); private readonly api = inject(MobileAdminApiService); private readonly changeDetector = inject(ChangeDetectorRef);
  resource: MobileResource = 'clients'; title = 'Clients'; emptyMessage = 'No records found.'; columns: HisHopeDataTableColumn[] = []; rows: MobileRow[] = []; totalItems = 0; loading = false; error = ''; query: HisHopePageQuery = { page: 1, pageSize: 20 };
  readonly bulkActions: HisHopeBulkAction[] = [{ id: 'activate', label: 'Activate selected', icon: 'check_circle', permission: 'admin.users.write' }, { id: 'deactivate', label: 'Deactivate selected', icon: 'block', tone: 'danger', permission: 'admin.users.write' }];
  ngOnInit(): void { this.resource = this.route.snapshot.data['resource'] as MobileResource; this.configure(); this.load(); }
  configure(): void {
    const configs: Record<MobileResource, { title: string; columns: HisHopeDataTableColumn[]; empty: string }> = {
      clients: { title: 'Clients', empty: 'No OIDC clients found.', columns: [{ key: 'clientId', label: 'Client ID', sortable: true, responsivePriority: 1 }, { key: 'displayName', label: 'Display name', sortable: true, responsivePriority: 1 }, { key: 'clientType', label: 'Type', responsivePriority: 2 }, { key: 'redirectUris', label: 'Redirect URIs', responsivePriority: 3 }] },
      users: { title: 'Users', empty: 'No users found.', columns: [{ key: 'userName', label: 'Username', sortable: true, responsivePriority: 1 }, { key: 'email', label: 'Email', sortable: true, responsivePriority: 2 }, { key: 'roles', label: 'Roles', responsivePriority: 3 }, { key: 'isActive', label: 'Active', responsivePriority: 2 }] },
      roles: { title: 'Roles', empty: 'No roles found.', columns: [{ key: 'name', label: 'Name', sortable: true, responsivePriority: 1 }, { key: 'description', label: 'Description', responsivePriority: 2 }] },
      consents: { title: 'Consents', empty: 'No consents recorded.', columns: [{ key: 'subject', label: 'Subject', sortable: true, responsivePriority: 1 }, { key: 'clientId', label: 'Client ID', responsivePriority: 2 }, { key: 'scopes', label: 'Scopes', responsivePriority: 3 }, { key: 'created', label: 'Created', sortable: true, responsivePriority: 2 }] },
    };
    const config = configs[this.resource]; this.title = config.title; this.emptyMessage = config.empty; this.columns = config.columns;
  }
  load(query = this.query): void { this.query = query; this.loading = true; this.error = ''; this.changeDetector.detectChanges(); this.api.getPage<MobileClient | MobileUser | MobileRole | MobileConsent>(this.resource, query).pipe(finalize(() => { this.loading = false; this.changeDetector.detectChanges(); }), catchError(() => { this.error = `Unable to load ${this.title.toLowerCase()}.`; return of({ items: [], totalCount: 0, page: query.page, pageSize: query.pageSize, totalPages: 0, hasNextPage: false, hasPreviousPage: false }); })).subscribe(result => { this.totalItems = result.totalCount; this.rows = result.items.map(item => this.toRow(item)); this.changeDetector.detectChanges(); }); }
  toRow(item: MobileClient | MobileUser | MobileRole | MobileConsent): MobileRow { if (this.resource === 'clients') { const value = item as MobileClient; return { id: value.id ?? value.clientId, clientId: value.clientId, displayName: value.displayName, clientType: value.clientType, redirectUris: value.redirectUris.join(', ') }; } if (this.resource === 'users') { const value = item as MobileUser; return { id: value.id, userName: value.userName, email: value.email, roles: value.roles.join(', '), isActive: value.isActive ? 'Yes' : 'No' }; } if (this.resource === 'roles') { const value = item as MobileRole; return { id: value.id, name: value.name, description: value.description ?? '' }; } const value = item as MobileConsent; return { id: value.id, subject: value.subject, clientId: value.clientId, scopes: value.scopes.join(', '), created: new Date(value.created).toLocaleDateString() }; }
  detail(row: MobileRow): string { return Object.entries(row).filter(([key]) => key !== 'id').map(([key, value]) => `${key}: ${String(value ?? '')}`).join(' · '); }
  bulk(request: HisHopeBulkActionRequest): void { if (this.resource === 'consents') return; this.loading = true; this.api.bulk(this.resource, request.actionId, request.rowKeys).pipe(finalize(() => this.loading = false), catchError(() => { this.error = 'Bulk action failed.'; return of(null); })).subscribe(result => { if (result) this.load(); }); }
  exportRows(_request: HisHopeTableExportRequest): void { this.error = 'Export is available from the desktop admin workspace.'; }
}
