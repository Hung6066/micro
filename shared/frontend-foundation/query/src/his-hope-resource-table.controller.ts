import { DestroyRef } from "@angular/core";
import { Observable, catchError, finalize, of } from "rxjs";
import {
  HisHopeBulkActionRequest,
  HisHopeBulkActionResult,
  HisHopePageQuery,
  HisHopePageResult,
  HisHopeSavedTableView,
  HisHopeTableViewRecord,
  HisHopeTranslateFn,
} from "@his-hope/frontend-foundation/contracts";
import { HisHopeResourceStore } from "./his-hope-resource-store";

export interface HisHopeResourceTableOptions<
  TQuery extends HisHopePageQuery,
  TItem,
> {
  readonly destroyRef: DestroyRef;
  readonly i18n: HisHopeTranslateFn;
  readonly initialQuery: TQuery;
  readonly loader: (query: TQuery) => Observable<HisHopePageResult<TItem>>;
  readonly loadErrorMessageKey: string;
  readonly loadErrorFallback: string;
  readonly onStateChange?: () => void;
}

/**
 * Shared list-page state/behavior for a server-backed `hh-data-table`: paging,
 * loading/error surfaces, bulk-action lifecycle and saved-view persistence.
 */
export class HisHopeResourceTableController<
  TQuery extends HisHopePageQuery,
  TItem,
> {
  private readonly i18n: HisHopeTranslateFn;
  private readonly loadErrorMessageKey: string;
  private readonly loadErrorFallback: string;
  readonly resource: HisHopeResourceStore<TQuery, HisHopePageResult<TItem>>;

  private readonly onStateChange?: () => void;
  query: TQuery;
  bulkLoading = false;
  actionError: string | null = null;
  savedView: HisHopeSavedTableView = {};
  expandedRowKeys: string[] = [];

  constructor(options: HisHopeResourceTableOptions<TQuery, TItem>) {
    this.i18n = options.i18n;
    this.loadErrorMessageKey = options.loadErrorMessageKey;
    this.loadErrorFallback = options.loadErrorFallback;
    this.onStateChange = options.onStateChange;
    this.query = options.initialQuery;
    this.resource = new HisHopeResourceStore<TQuery, HisHopePageResult<TItem>>(
      options.loader,
      options.initialQuery,
      options.destroyRef,
    );
  }

  private notify(): void {
    this.onStateChange?.();
  }

  setActionError(message: string | null): void {
    this.actionError = message;
    this.notify();
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
    this.notify();
  }

  onQueryChange(query: TQuery): void {
    this.load(query);
  }

  toggleRowExpand(event: { rowKey: string; expanded: boolean }): void {
    this.expandedRowKeys = event.expanded
      ? [...this.expandedRowKeys, event.rowKey]
      : this.expandedRowKeys.filter((key) => key !== event.rowKey);
    this.notify();
  }

  runBulkAction(
    request: HisHopeBulkActionRequest,
    bulkFn: (
      request: HisHopeBulkActionRequest,
    ) => Observable<HisHopeBulkActionResult>,
    errorMessageKey: string,
    errorFallback: string,
  ): void {
    this.bulkLoading = true;
    this.notify();
    bulkFn(request)
      .pipe(
        finalize(() => {
          this.bulkLoading = false;
          this.notify();
        }),
        catchError(() => {
          this.actionError = this.i18n.t(errorMessageKey, errorFallback);
          return of(null);
        }),
      )
      .subscribe((result) => {
        if (result) this.load(this.query);
      });
  }

  loadServerView(getViews: () => Observable<HisHopeTableViewRecord[]>): void {
    getViews().subscribe((views) => {
      const view = views.find((item) => item.name === "default") ?? views[0];
      if (view) this.savedView = JSON.parse(view.payloadJson);
      this.notify();
    });
  }

  saveView(
    event: { name: string; payload: unknown },
    saveFn: (name: string, payload: unknown) => Observable<HisHopeTableViewRecord>,
  ): void {
    saveFn(event.name, event.payload).subscribe();
  }

  resetView(
    event: { name: string },
    deleteFn: (name: string) => Observable<void>,
    reloadServerView: () => void,
  ): void {
    this.savedView = {};
    this.notify();
    deleteFn(event.name).subscribe({ error: () => reloadServerView() });
  }
}
