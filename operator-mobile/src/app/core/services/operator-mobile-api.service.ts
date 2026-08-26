import { Injectable, inject } from "@angular/core";
import { HttpClient, HttpHeaders, HttpParams } from "@angular/common/http";
import { Observable } from "rxjs";
import { resolveMobileApiOrigin } from "../mobile-runtime";
import { OperatorMobileTenantContextService } from "../operator-mobile-tenant-context.service";
import type { OperationTransportResult, QueuedOperation } from "../offline/operation-queue.models";

export interface ProductionBatch {
  id: string;
  batchNumber: string;
  status: string;
  plannedQuantity?: number;
  version?: string;
}

export interface LotSummary { id: string; sku: string; quantity: number; disposition: string; }
export interface MaintenanceWorkOrder { id: string; machineId: string; status: string; maintenanceType?: string; notes?: string; }

@Injectable({ providedIn: "root" })
export class OperatorMobileApiService {
  private readonly http = inject(HttpClient);
  private readonly tenant = inject(OperatorMobileTenantContextService);
  private readonly baseUrl = `${resolveMobileApiOrigin()}/api/v1/manufacturing`;

  getProductionBatches(status?: string): Observable<ProductionBatch[]> {
    let params = new HttpParams();
    if (status) params = params.set("status", status);
    if (this.tenant.activeTenantKey()) params = params.set("tenantKey", this.tenant.activeTenantKey()!);
    return this.http.get<ProductionBatch[]>(`${this.baseUrl}/production-batches`, { params });
  }

  getLots(sku?: string): Observable<LotSummary[]> {
    let params = new HttpParams();
    if (sku) params = params.set("sku", sku);
    if (this.tenant.activeTenantKey()) params = params.set("tenantKey", this.tenant.activeTenantKey()!);
    return this.http.get<LotSummary[]>(`${this.baseUrl}/lots`, { params });
  }

  getMaintenanceWorkOrders(status = "Open"): Observable<MaintenanceWorkOrder[]> {
    return this.http.get<MaintenanceWorkOrder[]>(`${this.baseUrl}/maintenance-work-orders`, { params: { status, tenantKey: this.tenant.activeTenantKey() ?? "" } });
  }

  recordProductionOperation(operation: QueuedOperation): Promise<OperationTransportResult> {
    return this.postQueued(`${this.baseUrl}${operation.endpoint}`, operation);
  }

  createQualityInspection(operation: QueuedOperation): Promise<OperationTransportResult> {
    return this.postQueued(`${this.baseUrl}/quality-inspections`, operation);
  }

  completeMaintenanceWorkOrder(operation: QueuedOperation): Promise<OperationTransportResult> {
    return this.postQueued(`${this.baseUrl}${operation.endpoint}`, operation);
  }

  private postQueued(url: string, operation: QueuedOperation): Promise<OperationTransportResult> {
    const headers = new HttpHeaders({
      "X-HisHope-Operation-Id": operation.operationId,
      ...(operation.expectedVersion ? { "If-Match": operation.expectedVersion } : {}),
    });
    return new Promise((resolve) => {
      this.http.post(url, operation.payload, { headers }).subscribe({
        next: () => resolve({ kind: "synced" }),
        error: (error: { status?: number; message?: string }) => {
          const message = error.message ?? "Operation could not be synchronised.";
          if (error.status === 409) resolve({ kind: "conflict", statusCode: 409, message });
          else if (error.status && error.status >= 400 && error.status < 500) resolve({ kind: "failed", message });
          else resolve({ kind: "pending", message });
        },
      });
    });
  }
}
