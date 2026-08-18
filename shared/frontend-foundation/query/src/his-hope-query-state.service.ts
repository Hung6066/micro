import { Injectable } from '@angular/core';
import { BehaviorSubject, Observable, Subject, debounceTime, distinctUntilChanged, map, shareReplay } from 'rxjs';
import { HisHopePageQuery } from '@his-hope/frontend-foundation/contracts';

/** One debounce contract for search, filters and URL-backed table queries. */
@Injectable({ providedIn: 'root' })
export class HisHopeQueryStateService {
  private readonly source = new Subject<HisHopePageQuery>();
  private readonly current = new BehaviorSubject<HisHopePageQuery>({ page: 1, pageSize: 20 });
  readonly query$: Observable<HisHopePageQuery> = this.source.pipe(
    debounceTime(250),
    map(query => ({ ...query, search: query.search?.trim() || undefined })),
    distinctUntilChanged((left, right) => JSON.stringify(left) === JSON.stringify(right)),
    shareReplay({ bufferSize: 1, refCount: true }),
  );

  readonly state$ = this.current.asObservable();

  update(query: HisHopePageQuery): void {
    const next = { ...query, page: query.page || 1, pageSize: query.pageSize || 20 };
    this.current.next(next);
    this.source.next(next);
  }

  patch(patch: Partial<HisHopePageQuery>): void { this.update({ ...this.current.value, ...patch, page: patch.page ?? 1 }); }
}
