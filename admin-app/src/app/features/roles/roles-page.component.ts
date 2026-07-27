import { Component, OnInit, inject } from '@angular/core';
import { HisHopeBulkAction, HisHopeBulkActionRequest, HisHopeDataTableComponent, HisHopeDataTableColumn, HisHopeDataTableDetailDirective, HisHopePageHeaderComponent, HisHopePageLayoutComponent, HisHopeTableExportRequest, HisHopeToolbarComponent, HisHopePageQuery } from '@his-hope/frontend-foundation';
import { AdminApiService, AdminPageQuery, Role } from '../../core/services/admin-api.service';
import { catchError, finalize } from 'rxjs/operators';
import { of, Subscription } from 'rxjs';

@Component({
  selector: 'app-roles-page',
  standalone: true,
  imports: [HisHopeDataTableComponent, HisHopeDataTableDetailDirective, HisHopePageHeaderComponent, HisHopePageLayoutComponent, HisHopeToolbarComponent],
  template: `
    <hh-page-layout>
      <hh-page-header hhPageHeader title="Roles" subtitle="Define reusable access policies for the hospital workspace" />
      <hh-toolbar hhPageToolbar label="Role controls">
        <span hhToolbarTitle>{{ totalItems }} roles</span>
        <button hh-toolbar-actions type="button" class="hh-button hh-button--secondary" (click)="loadRoles()">
          <span class="material-icons" aria-hidden="true">refresh</span>
          Refresh
        </button>
      </hh-toolbar>
      <hh-data-table label="Roles" [columns]="columns" [rows]="tableRows" [selection]="true" [loading]="loading" mode="server"
        [totalItems]="totalItems" [query]="query" [pageSize]="20" (queryChange)="onQueryChange($event)"
        [error]="error ?? ''" [empty]="!loading && !error && tableRows.length === 0" [exportable]="true"
        [bulkActions]="bulkActions" [filterBuilder]="true" [virtualizeColumns]="true" [expandedRowKeys]="expandedRowKeys" viewStorageKey="admin.roles" viewName="default" [serverBackedView]="true" [savedView]="savedView"
        (rowExpandChange)="toggleRowExpand($event)"
        (viewSaveRequested)="saveView($event)" (viewResetRequested)="resetView($event)" (bulkActionRequested)="onBulkAction($event)" (exportRequested)="onExport($event)"
        emptyMessage="No roles found." (retry)="loadRoles()">
        <ng-template hhDataTableDetail let-row>
          <div class="hh-data-table-detail">{{ row['description'] || 'No description' }}</div>
        </ng-template>
      </hh-data-table>
    </hh-page-layout>
  `,
})
export class RolesPageComponent implements OnInit {
  private readonly api = inject(AdminApiService);
  roles: Role[] = [];
  readonly columns: HisHopeDataTableColumn[] = [{ key: 'name', label: 'Name', sortable: true, responsivePriority: 1, pinned: 'left' }, { key: 'description', label: 'Description', responsivePriority: 2 }];
  tableRows: Record<string, unknown>[] = [];
  loading = false;
  error: string | null = null;
  totalItems = 0;
  query: AdminPageQuery = { page: 1, pageSize: 20 };
  savedView: any = null;
  expandedRowKeys: string[] = [];
  private pageRequest?: Subscription;
  readonly bulkActions: HisHopeBulkAction[] = [{ id: 'delete', label: 'Delete selected', tone: 'danger' }];

  ngOnInit(): void { this.loadServerView(); this.loadRoles(); }

  loadServerView(): void { this.api.getTableViews('roles').subscribe(views => { const view = views.find(item => item.name === 'default') ?? views[0]; if (view) this.savedView = JSON.parse(view.payloadJson); }); }
  saveView(event: { name: string; payload: unknown }): void { this.api.saveTableView('roles', event.name, event.payload).subscribe(); }
  resetView(event: { name: string }): void {
    this.savedView = null;
    this.api.deleteTableView('roles', event.name).subscribe({ error: () => this.loadServerView() });
  }
  toggleRowExpand(event: { rowKey: string; expanded: boolean }): void { this.expandedRowKeys = event.expanded ? [...this.expandedRowKeys, event.rowKey] : this.expandedRowKeys.filter(key => key !== event.rowKey); }

  loadRoles(query = this.query): void {
    this.pageRequest?.unsubscribe();
    this.query = query;
    this.loading = true;
    this.error = null;
    this.pageRequest = this.api.getRolesPage(query).pipe(finalize(() => this.loading = false), catchError(() => { this.error = 'Failed to load roles.'; return of({ items: [], totalCount: 0, page: query.page, pageSize: query.pageSize, totalPages: 0, hasNextPage: false, hasPreviousPage: false }); }))
      .subscribe(result => { this.totalItems = result.totalCount; this.roles = result.items; this.tableRows = result.items.map(role => ({ id: role.id, name: role.name, description: role.description })); });
  }

  onQueryChange(query: HisHopePageQuery): void { this.loadRoles(query); }

  onBulkAction(request: HisHopeBulkActionRequest): void {
    this.loading = true;
    this.api.bulkTable('roles', request).pipe(finalize(() => this.loading = false), catchError(() => { this.error = 'Failed to update selected roles.'; return of(null); }))
      .subscribe(result => { if (result) this.loadRoles(this.query); });
  }

  onExport(request: HisHopeTableExportRequest): void {
    this.api.exportTable('roles', request).subscribe(blob => {
      const url = URL.createObjectURL(blob); const anchor = document.createElement('a');
      anchor.href = url; anchor.download = `roles-${new Date().toISOString().slice(0, 10)}.${request.format}`; anchor.click(); URL.revokeObjectURL(url);
    });
  }
}
