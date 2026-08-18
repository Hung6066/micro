import { HttpClient } from "@angular/common/http";
import { Injectable, inject } from "@angular/core";
import { Observable } from "rxjs";
import { map } from "rxjs/operators";
import { HisHopeRequestCacheService } from "@his-hope/frontend-foundation/query";
import { environment } from "../../../environments/environment";

export interface PlatformResource {
  name: string;
  displayName?: string;
  type?: string;
  status?: string;
  healthStatus?: string;
  engine?: string;
  sizeMb?: number;
  connections?: number;
  healthChecks?: Array<{ name: string; status: string; output?: string }>;
}

export interface DatabaseContinuityStatus {
  enabled: boolean;
  schedulerEnabled?: boolean;
  backupIntervalHours?: number;
  restoreDrillIntervalHours?: number;
  maxAttempts?: number;
  provider: string;
  storageProvider?: string;
  storageUri?: string;
  pitrEnabled: boolean;
  encryption: { provider: string; configured: boolean };
  retentionDays: number;
  keepLastBackupsPerDatabase?: number;
  targetRpoMinutes: number;
  targetRtoMinutes: number;
  lastSuccessfulBackupAt?: string;
  lastSuccessfulRestoreDrillAt?: string;
  executorConfigured: boolean;
  ready: boolean;
  configurationErrors: string[];
  alerts?: string[];
  vault?: {
    configured: boolean;
    reachable: boolean;
    sealed: boolean;
    keyName: string;
    keyVersion?: number;
    error?: string;
  };
  latestJob?: DatabaseContinuityJob;
}

export interface DatabaseContinuityAuditEntry {
  jobId: string;
  operation: string;
  targetEnvironment: string;
  status: string;
  actorSubject: string;
  correlationId: string;
  createdAt: string;
  updatedAt: string;
  errorCode?: string;
  resultJson?: string;
}

export interface DatabaseContinuityJob {
  jobId: string;
  operation: string;
  status: string;
  errorCode?: string;
  correlationId?: string;
  createdAt: string;
  updatedAt: string;
  result?: string;
}

/** Database platform and continuity API used by the platform workflow. */
@Injectable({ providedIn: "root" })
export class DatabasePlatformApiService {
  private readonly http = inject(HttpClient);
  private readonly requestCache = inject(HisHopeRequestCacheService);
  private readonly baseUrl = environment.adminApiUrl;
  private readonly dashboardBffUrl = environment.dashboardBffUrl;

  getPlatformResources(): Observable<PlatformResource[]> {
    return this.requestCache
      .getOrLoad("platform:resources", () =>
        this.http.get<PlatformResource[]>(
          `${this.dashboardBffUrl}/api/resources`,
        ),
      )
      .pipe(
        map((resources) =>
          resources.filter(
            (resource) =>
              resource.type === "database" ||
              resource.engine ||
              resource.name.toLowerCase().includes("db"),
          ),
        ),
      );
  }

  getDatabaseContinuity(): Observable<DatabaseContinuityStatus> {
    return this.http.get<DatabaseContinuityStatus>(
      `${this.baseUrl}/database-continuity/status`,
    );
  }

  getDatabaseContinuityAudit(
    page = 1,
    pageSize = 20,
  ): Observable<DatabaseContinuityAuditEntry[]> {
    return this.http.get<DatabaseContinuityAuditEntry[]>(
      `${this.baseUrl}/database-continuity/audit?page=${page}&pageSize=${pageSize}`,
    );
  }

  requestDatabaseBackup(): Observable<DatabaseContinuityJob> {
    return this.http.post<DatabaseContinuityJob>(
      `${this.baseUrl}/database-continuity/backups`,
      {},
    );
  }

  requestRestoreDrill(): Observable<DatabaseContinuityJob> {
    return this.http.post<DatabaseContinuityJob>(
      `${this.baseUrl}/database-continuity/restore-drills`,
      {},
    );
  }
}
