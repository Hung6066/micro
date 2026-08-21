import { Observable, catchError, finalize, of } from "rxjs";
import {
  HisHopeBulkActionRequest,
  HisHopeBulkActionResult,
  HisHopePageQuery,
  HisHopePageResult,
  HisHopeTranslateFn,
} from "@his-hope/frontend-foundation/contracts";

export interface HisHopeMobilePagedResourceOptions<
  TQuery extends HisHopePageQuery,
  TItem,
> {
  readonly i18n: HisHopeTranslateFn;
  readonly initialQuery: TQuery;
  readonly loader: (query: TQuery) => Observable<HisHopePageResult<TItem>>;
  readonly loadErrorMessageKey: string;
  readonly loadErrorFallback: string;
  readonly loadMoreErrorMessageKey: string;
  readonly loadMoreErrorFallback: string;
  readonly onStateChange?: () => void;
}

/** Mobile list controller with cursor/page append for infinite scroll. */
export class HisHopeMobilePagedResourceController<
  TQuery extends HisHopePageQuery,
  TItem,
> {
  private readonly i18n: HisHopeTranslateFn;
  private readonly loader: (
    query: TQuery,
  ) => Observable<HisHopePageResult<TItem>>;
  private readonly loadErrorMessageKey: string;
  private readonly loadErrorFallback: string;
  private readonly loadMoreErrorMessageKey: string;
  private readonly loadMoreErrorFallback: string;
  private readonly onStateChange?: () => void;
  private requestSequence = 0;

  query: TQuery;
  items: TItem[] = [];
  totalCount = 0;
  loading = false;
  loadingMore = false;
  bulkLoading = false;
  actionError: string | null = null;
  loadMoreError = "";
  hasMore = false;
  nextCursor: string | null = null;

  constructor(options: HisHopeMobilePagedResourceOptions<TQuery, TItem>) {
    this.i18n = options.i18n;
    this.loader = options.loader;
    this.loadErrorMessageKey = options.loadErrorMessageKey;
    this.loadErrorFallback = options.loadErrorFallback;
    this.loadMoreErrorMessageKey = options.loadMoreErrorMessageKey;
    this.loadMoreErrorFallback = options.loadMoreErrorFallback;
    this.onStateChange = options.onStateChange;
    this.query = options.initialQuery;
  }

  private notify(): void {
    this.onStateChange?.();
  }

  get error(): string | null {
    return this.actionError;
  }

  get displayError(): string {
    return this.actionError ?? this.loadMoreError;
  }

  setActionError(message: string | null): void {
    this.actionError = message;
    this.notify();
  }

  load(query: TQuery = this.query): void {
    const sequence = ++this.requestSequence;
    this.query = {
      ...query,
      page: query.page || 1,
      pageSize: query.pageSize || 20,
      cursor: query.cursor || undefined,
    };
    this.loading = true;
    this.loadingMore = false;
    this.actionError = null;
    this.loadMoreError = "";
    this.hasMore = false;
    this.nextCursor = null;
    if (!this.query.cursor && (this.query.page || 1) <= 1) {
      this.items = [];
    }
    this.notify();

    this.loader(this.query)
      .pipe(
        finalize(() => {
          if (sequence === this.requestSequence) {
            this.loading = false;
            this.notify();
          }
        }),
        catchError(() => {
          if (sequence === this.requestSequence) {
            this.actionError = this.i18n.t(
              this.loadErrorMessageKey,
              this.loadErrorFallback,
            );
          }
          return of(null);
        }),
      )
      .subscribe((result) => {
        if (!result || sequence !== this.requestSequence) return;
        this.applyPageResult(result, false);
      });
  }

  loadMore(cursor: string | null = this.nextCursor): void {
    if (this.loading || this.loadingMore || !this.hasMore) return;
    const nextQuery: TQuery = cursor
      ? ({ ...this.query, page: 1, cursor } as TQuery)
      : ({
          ...this.query,
          page: (this.query.page || 1) + 1,
          cursor: undefined,
        } as TQuery);
    const sequence = ++this.requestSequence;
    this.loadingMore = true;
    this.loadMoreError = "";
    this.notify();

    this.loader(nextQuery)
      .pipe(
        finalize(() => {
          if (sequence === this.requestSequence) {
            this.loadingMore = false;
            this.notify();
          }
        }),
        catchError(() => {
          if (sequence === this.requestSequence) {
            this.loadMoreError = this.i18n.t(
              this.loadMoreErrorMessageKey,
              this.loadMoreErrorFallback,
            );
          }
          return of(null);
        }),
      )
      .subscribe((result) => {
        if (!result || sequence !== this.requestSequence) return;
        this.query = {
          ...nextQuery,
          cursor: result.nextCursor || undefined,
        } as TQuery;
        this.applyPageResult(result, true);
      });
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
        if (result) this.load({ ...this.query, page: 1, cursor: undefined });
      });
  }

  private applyPageResult(
    result: HisHopePageResult<TItem>,
    append: boolean,
  ): void {
    this.totalCount = result.totalCount;
    this.nextCursor = result.nextCursor ?? null;
    this.hasMore = !!result.nextCursor || result.hasNextPage;
    this.items = append
      ? [...this.items, ...result.items]
      : [...result.items];
    this.notify();
  }
}
