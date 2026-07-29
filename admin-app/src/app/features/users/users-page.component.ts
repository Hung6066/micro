import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { HisHopeBulkAction, HisHopeBulkActionRequest, HisHopeDataTableComponent, HisHopeDataTableColumn, HisHopeDataTableDetailDirective, HisHopeI18nService, HisHopePageHeaderComponent, HisHopePageLayoutComponent, HisHopeTableExportRequest, HisHopeToolbarComponent, HisHopeTranslatePipe } from '@his-hope/frontend-foundation';
import { AdminApiService, AdminPageQuery, AdminPageResult, User } from '../../core/services/admin-api.service';
import { HisHopePageQuery } from '@his-hope/frontend-foundation';
import { catchError, finalize } from 'rxjs/operators';
import { Subscription } from 'rxjs';
import { of } from 'rxjs';

@Component({
  selector: 'app-users-page',
  standalone: true,
  imports: [CommonModule, HisHopeDataTableComponent, HisHopeDataTableDetailDirective, HisHopePageHeaderComponent, HisHopePageLayoutComponent, HisHopeToolbarComponent, HisHopeTranslatePipe],
  template: `
    <hh-page-layout>
      <hh-page-header hhPageHeader [title]="'admin.pageUsers' | hhTranslate" [subtitle]="'admin.usersSubtitle' | hhTranslate" />
      <hh-toolbar hhPageToolbar [label]="'admin.users' | hhTranslate">
        <span hhToolbarTitle>{{ tableRows.length }} {{ 'admin.users' | hhTranslate }}</span>
        <button hh-toolbar-actions type="button" class="hh-icon-button" (click)="loadUsers()" aria-label="Refresh users" title="Refresh users">
          <span class="material-icons" aria-hidden="true">refresh</span>
        </button>
      </hh-toolbar>
      <hh-data-table label="Users" [columns]="columns" [rows]="tableRows" [selection]="true" [loading]="loading"
        mode="server" [totalItems]="totalItems" [query]="query" [pageSize]="20" (queryChange)="onQueryChange($event)"
        [error]="error ?? ''" [empty]="!loading && !error && tableRows.length === 0"
        [exportable]="true" [bulkActions]="bulkActions" [filterBuilder]="true" [virtualizeColumns]="true" [expandedRowKeys]="expandedRowKeys" viewStorageKey="admin.users" viewName="default" [serverBackedView]="true" [savedView]="savedView"
        (rowExpandChange)="toggleRowExpand($event)"
        (viewSaveRequested)="saveView($event)" (viewResetRequested)="resetView($event)" [emptyMessage]="'admin.noUsers' | hhTranslate"
        (bulkActionRequested)="onBulkAction($event)" (exportRequested)="onExport($event)" (retry)="loadUsers()">
        <ng-template hhDataTableDetail let-row>
          <div class="hh-data-table-detail">{{ row['email'] }} · {{ row['roles'] }} · {{ row['isActive'] }}</div>
        </ng-template>
      </hh-data-table>
    </hh-page-layout>
  `,
})
export class UsersPageComponent implements OnInit {
  private readonly api = inject(AdminApiService);
  private readonly i18n = inject(HisHopeI18nService);
  users: User[] = [];
  get columns(): HisHopeDataTableColumn[] { this.i18n.locale(); return [
    { key: 'id', label: this.i18n.t('admin.id'), responsivePriority: 3, pinned: 'left' }, { key: 'userName', label: this.i18n.t('admin.username'), sortable: true, responsivePriority: 1, pinned: 'left' },
    { key: 'email', label: this.i18n.t('admin.email'), sortable: true, responsivePriority: 2 }, { key: 'roles', label: this.i18n.t('admin.roles'), responsivePriority: 3 }, { key: 'isActive', label: this.i18n.t('admin.active'), responsivePriority: 2, pinned: 'right' },
  ]; }
  tableRows: Record<string, unknown>[] = [];
  loading = false;
  error: string | null = null;
  totalItems = 0;
  query: AdminPageQuery = { page: 1, pageSize: 20 };
  savedView: any = null;
  expandedRowKeys: string[] = [];
  private pageRequest?: Subscription;
  get bulkActions(): HisHopeBulkAction[] { this.i18n.locale(); return [
    { id: 'activate', label: this.i18n.t('admin.activateSelected'), icon: 'person_add' },
    { id: 'deactivate', label: this.i18n.t('admin.deactivateSelected'), icon: 'person_off', tone: 'danger' },
  ]; }

  ngOnInit(): void { this.loadServerView(); this.loadUsers(); }

  loadServerView(): void { this.api.getTableViews('users').subscribe(views => { const view = views.find(item => item.name === 'default') ?? views[0]; if (view) this.savedView = JSON.parse(view.payloadJson); }); }
  saveView(event: { name: string; payload: unknown }): void { this.api.saveTableView('users', event.name, event.payload).subscribe(); }
  resetView(event: { name: string }): void {
    this.savedView = null;
    this.api.deleteTableView('users', event.name).subscribe({ error: () => this.loadServerView() });
  }
  toggleRowExpand(event: { rowKey: string; expanded: boolean }): void { this.expandedRowKeys = event.expanded ? [...this.expandedRowKeys, event.rowKey] : this.expandedRowKeys.filter(key => key !== event.rowKey); }

  loadUsers(query = this.query): void {
    this.pageRequest?.unsubscribe();
    this.query = query;
    this.loading = true;
    this.error = null;
    this.pageRequest = this.api.getUsersPage(query).pipe(finalize(() => this.loading = false), catchError(() => { this.error = 'Failed to load users.'; return of<AdminPageResult<User>>({ items: [], totalCount: 0, page: query.page, pageSize: query.pageSize, totalPages: 0, hasNextPage: false, hasPreviousPage: false }); }))
      .subscribe(result => {
        this.totalItems = result.totalCount;
        this.users = result.items;
        this.tableRows = result.items.map(user => ({ id: user.id, userName: user.userName, email: user.email, roles: (user.roles || []).join(', '), isActive: user.isActive ? 'Yes' : 'No' }));
      });
  }

  onQueryChange(query: HisHopePageQuery): void { this.loadUsers(query); }

  onBulkAction(request: HisHopeBulkActionRequest): void {
    this.loading = true;
    this.api.bulkUsers(request).pipe(finalize(() => this.loading = false), catchError(() => {
      this.error = 'Failed to update selected users.';
      return of(null);
    })).subscribe(result => { if (result) this.loadUsers(this.query); });
  }

  onExport(request: HisHopeTableExportRequest): void {
    this.api.exportTable('users', request).subscribe(blob => {
      const url = URL.createObjectURL(blob);
      const anchor = document.createElement('a');
      anchor.href = url;
      anchor.download = `users-${new Date().toISOString().slice(0, 10)}.${request.format}`;
      anchor.click();
      URL.revokeObjectURL(url);
    });
  }
}
