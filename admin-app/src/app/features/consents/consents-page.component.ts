import { Component, OnInit, inject } from '@angular/core';
import { HisHopeDataTableComponent, HisHopeDataTableColumn, HisHopePageHeaderComponent, HisHopePageLayoutComponent, HisHopeToolbarComponent, HisHopePageQuery } from '@his-hope/frontend-foundation';
import { AdminApiService, AdminPageQuery, Consent } from '../../core/services/admin-api.service';
import { catchError, finalize } from 'rxjs/operators';
import { of } from 'rxjs';

@Component({
  selector: 'app-consents-page', standalone: true,
  imports: [HisHopeDataTableComponent, HisHopePageHeaderComponent, HisHopePageLayoutComponent, HisHopeToolbarComponent],
  template: `<hh-page-layout>
    <hh-page-header hhPageHeader title="User Consents" subtitle="Review granted permissions and client scopes" />
    <hh-toolbar hhPageToolbar label="Consent controls">
      <span hhToolbarTitle>{{ totalItems }} consents</span>
      <button hh-toolbar-actions type="button" class="hh-button hh-button--secondary" (click)="loadConsents()">
        <span class="material-icons" aria-hidden="true">refresh</span>
        Refresh
      </button>
    </hh-toolbar>
    <hh-data-table label="User consents" [columns]="columns" [rows]="tableRows" [selection]="true" [loading]="loading" mode="server" [totalItems]="totalItems" [query]="query" [pageSize]="20" (queryChange)="onQueryChange($event)" [error]="error ?? ''"
      [empty]="!loading && !error && tableRows.length === 0" emptyMessage="No consents recorded." (retry)="loadConsents()" />
  </hh-page-layout>`,
})
export class ConsentsPageComponent implements OnInit {
  private readonly api = inject(AdminApiService);
  consents: Consent[] = [];
  readonly columns: HisHopeDataTableColumn[] = [{ key: 'subject', label: 'Subject', sortable: true, responsivePriority: 1 }, { key: 'clientId', label: 'Client ID', responsivePriority: 2 }, { key: 'scopes', label: 'Scopes', responsivePriority: 3 }, { key: 'created', label: 'Created', sortable: true, responsivePriority: 2 }];
  tableRows: Record<string, unknown>[] = [];
  loading = false;
  error: string | null = null;
  totalItems = 0;
  query: AdminPageQuery = { page: 1, pageSize: 20 };
  ngOnInit(): void { this.loadConsents(); }
  loadConsents(query = this.query): void { this.query = query; this.loading = true; this.error = null; this.api.getConsentsPage(query).pipe(finalize(() => this.loading = false), catchError(() => { this.error = 'Failed to load consents.'; return of({ items: [], totalCount: 0, page: query.page, pageSize: query.pageSize, totalPages: 0, hasNextPage: false, hasPreviousPage: false }); })).subscribe(result => { this.totalItems = result.totalCount; this.consents = result.items; this.tableRows = result.items.map(consent => ({ id: consent.id, subject: consent.subject, clientId: consent.clientId, scopes: (consent.scopes || []).join(', '), created: consent.created })); }); }
  onQueryChange(query: HisHopePageQuery): void { this.loadConsents(query); }
}
