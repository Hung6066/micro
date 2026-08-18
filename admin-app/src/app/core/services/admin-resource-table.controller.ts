import { DestroyRef } from "@angular/core";
import { Observable, catchError, finalize, of } from "rxjs";
import { HisHopeI18nService } from "@his-hope/frontend-foundation/i18n";
import { HisHopeResourceStore } from "@his-hope/frontend-foundation/query";
import { HisHopeBulkActionRequest } from "@his-hope/frontend-foundation/contracts";
import {
  AdminBulkActionResponse,
  AdminPageQuery,
  AdminPageResult,
  AdminSavedTableView,
  AdminTableView,
} from "../contracts/admin.contracts";

export interface AdminResourceTableOptions<
  TQuery extends AdminPageQuery,
  TItem,
> {
  readonly destroyRef: DestroyRef;
  readonly i18n: HisHopeI18nService;
  readonly initialQuery: TQuery;
  readonly loader: (query: TQuery) => Observable<AdminPageResult<TItem>>;
  readonly loadErrorMessageKey: string;
  readonly loadErrorFallback: string;
}

/**
 * Shared list-page state/behavior for a server-backed `hh-data-table`: paging,
 * loading/error surfaces, bulk-action lifecycle and saved-view persistence.
 * Feature pages keep their own domain row mapping and API calls; this only
 * removes the boilerplate that was copy-pasted across every admin list page.
 */
export class AdminResourceTableController<
  TQuery extends AdminPageQuery,
  TItem,
> {
  private readonly i18n: HisHopeI18nService;
  private readonly loadErrorMessageKey: string;
  private readonly loadErrorFallback: string;
  readonly resource: HisHopeResourceStore<TQuery, AdminPageResult<TItem>>;

  query: TQuery;
  bulkLoading = false;
  actionError: string | null = null;
  savedView: AdminSavedTableView = {};
  expandedRowKeys: string[] = [];

  constructor(options: AdminResourceTableOptions<TQuery, TItem>) {
    this.i18n = options.i18n;
    this.loadErrorMessageKey = options.loadErrorMessageKey;
    this.loadErrorFallback = options.loadErrorFallback;
    this.query = options.initialQuery;
    this.resource = new HisHopeResourceStore<TQuery, AdminPageResult<TItem>>(
      options.loader,
      options.initialQuery,
      options.destroyRef,
    );
  }

  get loading(): boolean {
    return this.resource.loading() || this.bulkLoading;
  }

  get error(): string | null {
    return (
      this.actionError ??
      (this.resource.error()
        ? this.i18n.t(this.loadErrorMessageKey, this.loadErrorFallback)
        : null)
    );
  }

  get totalItems(): number {
    return this.resource.data()?.totalCount ?? 0;
  }

  get items(): TItem[] {
    return this.resource.data()?.items ?? [];
  }

  load(query: TQuery = this.query): void {
    this.query = query;
    this.actionError = null;
    this.resource.setQuery(query);
  }

  onQueryChange(query: TQuery): void {
    this.load(query);
  }

  toggleRowExpand(event: { rowKey: string; expanded: boolean }): void {
    this.expandedRowKeys = event.expanded
      ? [...this.expandedRowKeys, event.rowKey]
      : this.expandedRowKeys.filter((key) => key !== event.rowKey);
  }

  runBulkAction(
    request: HisHopeBulkActionRequest,
    bulkFn: (
      request: HisHopeBulkActionRequest,
    ) => Observable<AdminBulkActionResponse>,
    errorMessageKey: string,
    errorFallback: string,
  ): void {
    this.bulkLoading = true;
    bulkFn(request)
      .pipe(
        finalize(() => (this.bulkLoading = false)),
        catchError(() => {
          this.actionError = this.i18n.t(errorMessageKey, errorFallback);
          return of(null);
        }),
      )
      .subscribe((result) => {
        if (result) this.load(this.query);
      });
  }

  loadServerView(getViews: () => Observable<AdminTableView[]>): void {
    getViews().subscribe((views) => {
      const view = views.find((item) => item.name === "default") ?? views[0];
      if (view) this.savedView = JSON.parse(view.payloadJson);
    });
  }

  saveView(
    event: { name: string; payload: unknown },
    saveFn: (name: string, payload: unknown) => Observable<AdminTableView>,
  ): void {
    saveFn(event.name, event.payload).subscribe();
  }

  resetView(
    event: { name: string },
    deleteFn: (name: string) => Observable<void>,
    reloadServerView: () => void,
  ): void {
    this.savedView = {};
    deleteFn(event.name).subscribe({ error: () => reloadServerView() });
  }
}
