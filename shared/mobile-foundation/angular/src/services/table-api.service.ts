import { HttpClient } from "@angular/common/http";
import { Injectable, inject } from "@angular/core";
import { Observable, from, of, timer } from "rxjs";
import {
  catchError,
  filter,
  map,
  switchMap,
  take,
  takeWhile,
  tap,
} from "rxjs/operators";
import {
  HisHopeBulkActionRequest,
  HisHopeBulkActionResult,
  HisHopeTableExportRequest,
  HisHopeTableViewRecord,
} from "@his-hope/frontend-foundation/contracts";
import { HisHopeRequestCacheService } from "@his-hope/frontend-foundation/query";
import {
  HIS_HOPE_TABLE_API_BASE_URL,
  HIS_HOPE_TABLE_API_CACHE_NAMESPACE,
} from "../tokens";

interface HisHopeTableJob {
  jobId: string;
  status: string;
}

/** Shared bulk/export client for server-backed table resources. */
@Injectable({ providedIn: "root" })
export class HisHopeMobileTableApiService {
  private readonly http = inject(HttpClient);
  private readonly requestCache = inject(HisHopeRequestCacheService);
  private readonly baseUrl = inject(HIS_HOPE_TABLE_API_BASE_URL);
  private readonly cacheNamespace = inject(HIS_HOPE_TABLE_API_CACHE_NAMESPACE);

  bulk(
    resource: string,
    request: HisHopeBulkActionRequest,
  ): Observable<HisHopeBulkActionResult> {
    return this.http
      .post<HisHopeBulkActionResult>(
        `${this.baseUrl}/tables/${resource}/bulk`,
        {
          actionId: request.actionId,
          rowKeys: request.rowKeys,
          query: request.query,
          selection: request.selection,
        },
      )
      .pipe(
        tap(() =>
          this.requestCache.invalidate(`${this.cacheNamespace}:${resource}:`),
        ),
      );
  }

  export(resource: string, request: HisHopeTableExportRequest): Observable<Blob> {
    return this.http
      .post(
        `${this.baseUrl}/tables/${resource}/export`,
        {
          format: request.format,
          columns: request.columns,
          rowKeys: request.rowKeys,
          query: request.query,
          async: request.async ?? false,
          maskSensitive: request.maskSensitive ?? true,
        },
        { observe: "response", responseType: "blob" },
      )
      .pipe(
        switchMap((response) => {
          const contentType = response.headers.get("content-type") ?? "";
          if (!contentType.includes("json")) return of(response.body as Blob);
          return from((response.body as Blob).text()).pipe(
            map((text) => JSON.parse(text) as HisHopeTableJob),
            switchMap((job) =>
              this.streamJob(job.jobId).pipe(
                catchError(() => this.waitForJob(job.jobId)),
              ),
            ),
          );
        }),
      );
  }

  getViews(resource: string): Observable<HisHopeTableViewRecord[]> {
    return this.http.get<HisHopeTableViewRecord[]>(
      `${this.baseUrl}/tables/${resource}/views`,
    );
  }

  saveView(
    resource: string,
    name: string,
    payload: unknown,
  ): Observable<HisHopeTableViewRecord> {
    return this.http.put<HisHopeTableViewRecord>(
      `${this.baseUrl}/tables/${resource}/views/${encodeURIComponent(name)}`,
      { payloadJson: JSON.stringify(payload) },
    );
  }

  deleteView(resource: string, name: string): Observable<void> {
    return this.http.delete<void>(
      `${this.baseUrl}/tables/${resource}/views/${encodeURIComponent(name)}`,
    );
  }

  private waitForJob(jobId: string): Observable<Blob> {
    return timer(0, 1000).pipe(
      switchMap(() =>
        this.http.get<HisHopeTableJob>(`${this.baseUrl}/tables/jobs/${jobId}`),
      ),
      takeWhile(
        (job) => !["Completed", "Failed", "Cancelled"].includes(job.status),
        true,
      ),
      filter((job) => job.status === "Completed"),
      take(1),
      switchMap(() =>
        this.http.get(`${this.baseUrl}/tables/jobs/${jobId}/download`, {
          responseType: "blob",
        }),
      ),
    );
  }

  private streamJob(jobId: string): Observable<Blob> {
    return new Observable<HisHopeTableJob>((subscriber) => {
      const source = new EventSource(
        `${this.baseUrl}/tables/jobs/${encodeURIComponent(jobId)}/events`,
        { withCredentials: true },
      );
      source.addEventListener("job", (event) => {
        try {
          const job = JSON.parse(
            (event as MessageEvent).data,
          ) as HisHopeTableJob;
          subscriber.next(job);
          if (["Completed", "Failed", "Cancelled"].includes(job.status)) {
            source.close();
            subscriber.complete();
          }
        } catch (error) {
          source.close();
          subscriber.error(error);
        }
      });
      source.onerror = () => {
        source.close();
        subscriber.error(new Error("SSE job stream unavailable"));
      };
      return () => source.close();
    }).pipe(
      filter((job) => job.status === "Completed"),
      take(1),
      switchMap(() =>
        this.http.get(`${this.baseUrl}/tables/jobs/${jobId}/download`, {
          responseType: "blob",
        }),
      ),
    );
  }
}
