import { Injectable, inject } from "@angular/core";
import { HttpClient, HttpHeaders } from "@angular/common/http";
import { Observable } from "rxjs";
import { resolveMobileApiOrigin } from "../mobile-runtime";
import { OperatorMobileTenantContextService } from "../operator-mobile-tenant-context.service";
import { OperatorMobileReadCacheService } from "./operator-mobile-read-cache.service";
import type { OperationTransportResult, QueuedOperation } from "../offline/operation-queue.models";
import type {
  HisHopeGenealogyDto,
  HisHopeRecallImpactDto,
  HisHopeInventoryTransactionDto,
  HisHopeMachineCalibrationDto,
  HisHopeMachineTelemetryDto,
  HisHopeManufacturingDowntimeDto,
  HisHopeManufacturingDashboardSummaryDto,
  HisHopeManufacturingProductionKpiDto,
  HisHopeManufacturingOeeDto,
  HisHopeManufacturingExecutiveExceptionDto,
  HisHopeManufacturingProductionCostDto,
  HisHopeManufacturingAvailabilityDto,
  HisHopeFefoLotDto,
  HisHopeManufacturingMachineDto,
  HisHopeManufacturingMachineHealthDto,
  HisHopeMaintenanceWorkOrderDto,
  HisHopeMaintenancePlanDto,
  HisHopeLotStatusHistoryDto,
  HisHopeQualityInspectionDto,
  HisHopeQualitySampleDto,
  HisHopeInspectionPlanVersionDto,
  HisHopeProductionBatchDto,
  HisHopeLotDto,
} from "@his-hope/frontend-foundation/contracts";

export type ProductionBatch = Pick<HisHopeProductionBatchDto, "id" | "batchNumber" | "status" | "plannedQuantity" | "operations"> & { version?: string };
export type LotSummary = Pick<HisHopeLotDto, "id" | "sku" | "quantity" | "uom" | "disposition" | "bestBefore" | "updatedAt"> & { lotCode?: string };
export type LotRelation = HisHopeGenealogyDto["relations"][number];
export type LotGenealogy = Pick<HisHopeGenealogyDto, "relations"> & { lot: LotSummary };
export type RecallImpact = HisHopeRecallImpactDto;
export type QualityInspection = HisHopeQualityInspectionDto;
export type QualitySample = HisHopeQualitySampleDto;
export type InspectionPlanVersion = HisHopeInspectionPlanVersionDto;
export type LotStatusHistory = HisHopeLotStatusHistoryDto;
export type InventoryTransaction = HisHopeInventoryTransactionDto;
export type MaintenanceWorkOrder = HisHopeMaintenanceWorkOrderDto;
export type MaintenancePlan = HisHopeMaintenancePlanDto;
export type Machine = HisHopeManufacturingMachineDto;
export type MachineHealth = HisHopeManufacturingMachineHealthDto;
export type MachineCalibration = HisHopeMachineCalibrationDto;
export type MachineTelemetry = HisHopeMachineTelemetryDto;
export type MachineDowntime = HisHopeManufacturingDowntimeDto;
export type ManufacturingSummary = HisHopeManufacturingDashboardSummaryDto;
export type ProductionKpi = HisHopeManufacturingProductionKpiDto;
export type ManufacturingOee = HisHopeManufacturingOeeDto;
export type ManufacturingException = HisHopeManufacturingExecutiveExceptionDto;
export type ManufacturingProductionCost = HisHopeManufacturingProductionCostDto;
export type ManufacturingAvailability = HisHopeManufacturingAvailabilityDto;
export type FefoLot = HisHopeFefoLotDto;

@Injectable({ providedIn: "root" })
export class OperatorMobileApiService {
  private readonly http = inject(HttpClient);
  private readonly tenant = inject(OperatorMobileTenantContextService);
  private readonly readCache = inject(OperatorMobileReadCacheService);
  private readonly baseUrl = `${resolveMobileApiOrigin()}/api/v1/manufacturing`;

  private cachedGet<T>(key: string, loader: () => Observable<T>): Observable<T> {
    return this.readCache.getOrLoad(`${this.tenant.activeTenantKey() ?? "none"}:${key}`, loader);
  }

  getProductionBatches(status?: string): Observable<ProductionBatch[]> {
    return this.cachedGet(`production-batches:${status ?? "all"}`, () => this.http.get<ProductionBatch[]>(`${this.baseUrl}/production-batches`, {
      params: status ? { status } : {},
    }));
  }

  getManufacturingSummary(): Observable<ManufacturingSummary> {
    return this.cachedGet("dashboard:manufacturing-summary", () => this.http.get<ManufacturingSummary>(`${this.baseUrl}/dashboard/manufacturing-summary`));
  }

  getProductionKpis(): Observable<ProductionKpi> {
    return this.cachedGet("dashboard:production-kpis", () => this.http.get<ProductionKpi>(`${this.baseUrl}/dashboard/production-kpis`));
  }

  getOee(machineId?: string): Observable<ManufacturingOee> {
    return this.cachedGet(`dashboard:oee:${machineId ?? "all"}`, () => this.http.get<ManufacturingOee>(`${this.baseUrl}/dashboard/oee`, {
      params: machineId ? { machineId } : {},
    }));
  }

  getExceptions(expiryWithinDays = 7, downtimeThresholdHours = 4): Observable<ManufacturingException[]> {
    return this.cachedGet(`dashboard:exceptions:${expiryWithinDays}:${downtimeThresholdHours}`, () => this.http.get<ManufacturingException[]>(`${this.baseUrl}/dashboard/exceptions`, {
      params: { expiryWithinDays, downtimeThresholdHours },
    }));
  }

  getProductionCosts(): Observable<ManufacturingProductionCost> {
    return this.cachedGet("dashboard:production-costs", () => this.http.get<ManufacturingProductionCost>(`${this.baseUrl}/dashboard/production-costs`));
  }

  getAvailability(sku: string): Observable<ManufacturingAvailability> {
    return this.cachedGet(`availability:${sku}`, () => this.http.get<ManufacturingAvailability>(`${this.baseUrl}/products/${encodeURIComponent(sku)}/availability`));
  }

  getFefoLots(sku: string): Observable<FefoLot[]> {
    return this.cachedGet(`fefo:${sku}`, () => this.http.get<FefoLot[]>(`${this.baseUrl}/products/${encodeURIComponent(sku)}/fefo`, { params: { limit: "10" } }));
  }

  getLots(sku?: string): Observable<LotSummary[]> {
    return this.cachedGet(`lots:${sku ?? "all"}`, () => this.http.get<LotSummary[]>(`${this.baseUrl}/lots`, {
      params: sku ? { sku } : {},
    }));
  }

  getInspectionPlanVersions(status = "Approved"): Observable<InspectionPlanVersion[]> {
    return this.cachedGet(`inspection-plans:${status}`, () => this.http.get<InspectionPlanVersion[]>(`${this.baseUrl}/inspection-plan-versions`, {
      params: { status },
    }));
  }

  getLotGenealogy(lotId: string, direction = "upstream"): Observable<LotGenealogy> {
    return this.cachedGet(`genealogy:${lotId}:${direction}`, () => this.http.get<LotGenealogy>(`${this.baseUrl}/lots/${lotId}/genealogy`, {
      params: { direction },
    }));
  }

  getRecallImpact(lotId: string): Observable<RecallImpact> {
    return this.cachedGet(`recall:${lotId}`, () => this.http.get<RecallImpact>(`${this.baseUrl}/lots/${lotId}/recall-impact`, {
      params: { maxLots: "500" },
    }));
  }

  getLotQualityInspections(lotId: string): Observable<QualityInspection[]> {
    return this.cachedGet(`quality-inspections:${lotId}`, () => this.http.get<QualityInspection[]>(`${this.baseUrl}/lots/${lotId}/quality-inspections`, {
      params: { limit: "25" },
    }));
  }

  getQualitySamples(inspectionId: string): Observable<QualitySample[]> {
    return this.cachedGet(`quality-samples:${inspectionId}`, () => this.http.get<QualitySample[]>(`${this.baseUrl}/quality-samples`, {
      params: { inspectionId, limit: "25" },
    }));
  }

  getLotStatusHistory(lotId: string): Observable<LotStatusHistory[]> {
    return this.cachedGet(`lot-status:${lotId}`, () => this.http.get<LotStatusHistory[]>(`${this.baseUrl}/lots/${lotId}/status-history`, {
      params: { limit: "25" },
    }));
  }

  getLotInventoryTransactions(lotId: string): Observable<InventoryTransaction[]> {
    return this.cachedGet(`lot-inventory:${lotId}`, () => this.http.get<InventoryTransaction[]>(`${this.baseUrl}/lots/${lotId}/inventory-transactions`, {
      params: { limit: "50" },
    }));
  }

  getMaintenanceWorkOrders(status = "Open"): Observable<MaintenanceWorkOrder[]> {
    return this.cachedGet(`maintenance-orders:${status}`, () => this.http.get<MaintenanceWorkOrder[]>(`${this.baseUrl}/maintenance-work-orders`, {
      params: { status },
    }));
  }

  getMaintenancePlans(active = true): Observable<MaintenancePlan[]> {
    return this.cachedGet(`maintenance-plans:${active}`, () => this.http.get<MaintenancePlan[]>(`${this.baseUrl}/maintenance-plans`, {
      params: { active },
    }));
  }

  getMachines(status?: string): Observable<Machine[]> {
    return this.cachedGet(`machines:${status ?? "all"}`, () => this.http.get<Machine[]>(`${this.baseUrl}/machines`, {
      params: status ? { status } : {},
    }));
  }

  getMachineHealth(dueWithinDays = 7): Observable<MachineHealth> {
    return this.cachedGet(`dashboard:machine-health:${dueWithinDays}`, () => this.http.get<MachineHealth>(`${this.baseUrl}/dashboard/machine-health`, {
      params: { dueWithinDays },
    }));
  }

  getMachineCalibrations(machineId: string): Observable<MachineCalibration[]> {
    return this.cachedGet(`calibrations:${machineId}`, () => this.http.get<MachineCalibration[]>(`${this.baseUrl}/machines/${machineId}/calibrations`, {
      params: { limit: "10" },
    }));
  }

  getMachineTelemetry(machineId: string): Observable<MachineTelemetry[]> {
    return this.cachedGet(`telemetry:${machineId}`, () => this.http.get<MachineTelemetry[]>(`${this.baseUrl}/machines/${machineId}/telemetry`, {
      params: { limit: "10" },
    }));
  }

  getMachineDowntimes(machineId: string, status = "Open"): Observable<MachineDowntime[]> {
    return this.cachedGet(`downtimes:${machineId}:${status}`, () => this.http.get<MachineDowntime[]>(`${this.baseUrl}/machine-downtimes`, {
      params: { machineId, status, limit: "10" },
    }));
  }

  recordProductionOperation(operation: QueuedOperation): Promise<OperationTransportResult> {
    return this.postQueued(`${this.baseUrl}${operation.endpoint}`, operation);
  }

  transitionProductionBatch(operation: QueuedOperation): Promise<OperationTransportResult> {
    return this.postQueued(`${this.baseUrl}${operation.endpoint}`, operation);
  }

  recordOperationMeasurement(operation: QueuedOperation): Promise<OperationTransportResult> {
    return this.postQueued(`${this.baseUrl}${operation.endpoint}`, operation);
  }

  reviewLoss(operation: QueuedOperation): Promise<OperationTransportResult> {
    return this.postQueued(`${this.baseUrl}${operation.endpoint}`, operation);
  }

  createQualityInspection(operation: QueuedOperation): Promise<OperationTransportResult> {
    return this.postQueued(`${this.baseUrl}/quality-inspections`, operation);
  }

  createDeviation(operation: QueuedOperation): Promise<OperationTransportResult> {
    return this.postQueued(`${this.baseUrl}${operation.endpoint}`, operation);
  }

  createQualitySample(operation: QueuedOperation): Promise<OperationTransportResult> {
    return this.postQueued(`${this.baseUrl}/quality-samples`, operation);
  }

  changeQualitySampleDisposition(operation: QueuedOperation): Promise<OperationTransportResult> {
    return this.postQueued(`${this.baseUrl}${operation.endpoint}`, operation);
  }

  changeLotDisposition(operation: QueuedOperation): Promise<OperationTransportResult> {
    return this.postQueued(`${this.baseUrl}${operation.endpoint}`, operation);
  }

  completeMaintenanceWorkOrder(operation: QueuedOperation): Promise<OperationTransportResult> {
    return this.postQueued(`${this.baseUrl}${operation.endpoint}`, operation);
  }

  recordMachineDowntime(operation: QueuedOperation): Promise<OperationTransportResult> {
    return this.postQueued(`${this.baseUrl}${operation.endpoint}`, operation);
  }

  resolveMachineDowntime(operation: QueuedOperation): Promise<OperationTransportResult> {
    return this.postQueued(`${this.baseUrl}${operation.endpoint}`, operation);
  }

  syncOperation(operation: QueuedOperation): Promise<OperationTransportResult> {
    const endpoint = operation.endpoint;
    if (endpoint.includes("/loss-review")) return this.reviewLoss(operation);
    if (endpoint.includes("/measurements")) return this.recordOperationMeasurement(operation);
    if (endpoint.endsWith("/operations")) return this.recordProductionOperation(operation);
    if (/\/production-batches\/[^/]+\/(start|pause|resume|complete|cancel)$/.test(endpoint)) return this.transitionProductionBatch(operation);
    if (endpoint === "/quality-inspections") return this.createQualityInspection(operation);
    if (endpoint === "/quality-samples") return this.createQualitySample(operation);
    if (endpoint.includes("/quality-samples/") && endpoint.endsWith("/disposition")) return this.changeQualitySampleDisposition(operation);
    if (endpoint.includes("/deviations")) return this.createDeviation(operation);
    if (endpoint.includes("/lots/") && endpoint.endsWith("/disposition")) return this.changeLotDisposition(operation);
    if (endpoint.includes("/maintenance-work-orders/") && endpoint.endsWith("/complete")) return this.completeMaintenanceWorkOrder(operation);
    if (endpoint.includes("/downtimes/") && endpoint.endsWith("/resolve")) return this.resolveMachineDowntime(operation);
    if (endpoint.includes("/downtimes")) return this.recordMachineDowntime(operation);
    return Promise.resolve({ kind: "failed", message: "Unsupported queued operation." });
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
