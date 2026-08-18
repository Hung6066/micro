import { DestroyRef, signal } from "@angular/core";
import {
  Observable,
  Subject,
  Subscription,
  switchMap,
  tap,
  catchError,
  EMPTY,
  finalize,
} from "rxjs";

export type HisHopeResourceLoader<TQuery, TData> = (
  query: TQuery,
) => Observable<TData>;

export interface HisHopeResourceDefinition<TQuery, TData> {
  key: string;
  initialQuery: TQuery;
  load: HisHopeResourceLoader<TQuery, TData>;
}

export class HisHopeResourceStore<TQuery, TData> {
  readonly query = signal<TQuery>(undefined as TQuery);
  readonly data = signal<TData | null>(null);
  readonly loading = signal(false);
  readonly error = signal<unknown | null>(null);
  private readonly querySubject = new Subject<TQuery>();
  private readonly subscription: Subscription;
  private requestId = 0;

  constructor(
    private readonly loader: HisHopeResourceLoader<TQuery, TData>,
    initialQuery: TQuery,
    destroyRef?: DestroyRef,
  ) {
    this.query.set(initialQuery);
    this.subscription = this.querySubject
      .pipe(
        tap(() => {
          this.requestId += 1;
          this.loading.set(true);
          this.error.set(null);
        }),
        switchMap((query) => {
          const requestId = this.requestId;
          return this.loader(query).pipe(
            tap((value) => this.data.set(value)),
            catchError((error) => {
              if (requestId === this.requestId) this.error.set(error);
              return EMPTY;
            }),
            finalize(() => {
              if (requestId === this.requestId) this.loading.set(false);
            }),
          );
        }),
      )
      .subscribe();
    destroyRef?.onDestroy(() => this.destroy());
  }

  setQuery(query: TQuery): void {
    this.query.set(query);
    this.querySubject.next(query);
  }
  load(): void {
    this.querySubject.next(this.query());
  }
  reload(): void {
    this.load();
  }
  destroy(): void {
    this.subscription.unsubscribe();
  }
}
