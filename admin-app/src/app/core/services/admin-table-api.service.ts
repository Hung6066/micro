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
  AdminBulkActionResponse,
  AdminJobContract,
  AdminTableView,
} from "../contracts/admin.contracts";
import {
  HisHopeBulkActionRequest,
  HisHopeTableExportRequest,
} from "@his-hope/frontend-foundation/contracts";
import { HisHopeRequestCacheService } from "@his-hope/frontend-foundation/query";
import { environment } from "../../../environments/environment";

export type AdminTableResource = "users" | "roles" | "clients" | "audit";

@Injectable({ providedIn: "root" })
export class AdminTableApiService {
  private readonly http = inject(HttpClient);
  private readonly requestCache = inject(HisHopeRequestCacheService);
  private readonly baseUrl = environment.adminApiUrl;

  bulk(
    resource: Exclude<AdminTableResource, "audit">,
    request: HisHopeBulkActionRequest,
  ): Observable<AdminBulkActionResponse> {
    return this.http
      .post<AdminBulkActionResponse>(
        `${this.baseUrl}/tables/${resource}/bulk`,
        {
          actionId: request.actionId,
          rowKeys: request.rowKeys,
          query: request.query,
          selection: request.selection,
        },
      )
      .pipe(tap(() => this.requestCache.invalidate(`admin:${resource}:`)));
  }

  export(
    resource: AdminTableResource,
    request: HisHopeTableExportRequest,
  ): Observable<Blob> {
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
            map((text) => JSON.parse(text) as AdminJobContract),
            switchMap((job) =>
              this.streamJob(job.jobId).pipe(
                catchError(() => this.waitForJob(job.jobId)),
              ),
            ),
          );
        }),
      );
  }

  getViews(
    resource: Exclude<AdminTableResource, "audit">,
  ): Observable<AdminTableView[]> {
    return this.http.get<AdminTableView[]>(
      `${this.baseUrl}/tables/${resource}/views`,
    );
  }

  saveView(
    resource: Exclude<AdminTableResource, "audit">,
    name: string,
    payload: unknown,
  ): Observable<AdminTableView> {
    return this.http.put<AdminTableView>(
      `${this.baseUrl}/tables/${resource}/views/${encodeURIComponent(name)}`,
      {
        payloadJson: JSON.stringify(payload),
      },
    );
  }

  deleteView(
    resource: Exclude<AdminTableResource, "audit">,
    name: string,
  ): Observable<void> {
    return this.http.delete<void>(
      `${this.baseUrl}/tables/${resource}/views/${encodeURIComponent(name)}`,
    );
  }

  private waitForJob(jobId: string): Observable<Blob> {
    return timer(0, 1000).pipe(
      switchMap(() =>
        this.http.get<AdminJobContract>(`${this.baseUrl}/tables/jobs/${jobId}`),
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
    return new Observable<AdminJobContract>((subscriber) => {
      const source = new EventSource(
        `${this.baseUrl}/tables/jobs/${encodeURIComponent(jobId)}/events`,
        { withCredentials: true },
      );
      source.addEventListener("job", (event) => {
        try {
          const job = JSON.parse(
            (event as MessageEvent).data,
          ) as AdminJobContract;
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
        this.http.get(
          `${this.baseUrl}/tables/jobs/${encodeURIComponent(jobId)}/download`,
          { responseType: "blob" },
        ),
      ),
    );
  }
}
