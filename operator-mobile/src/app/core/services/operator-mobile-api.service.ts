import { Injectable, inject } from "@angular/core";
import { HttpClient, HttpHeaders } from "@angular/common/http";
import { Observable } from "rxjs";
import { resolveMobileApiOrigin } from "../mobile-runtime";
import type { OperationTransportResult, QueuedOperation } from "../offline/operation-queue.models";
import type {
  HisHopeGenealogyDto,
  HisHopeRecallImpactDto,
  HisHopeInventoryTransactionDto,
  HisHopeMachineCalibrationDto,
  HisHopeMachineTelemetryDto,
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

export type ProductionBatch = Pick<HisHopeProductionBatchDto, "id" | "batchNumber" | "status" | "plannedQuantity"> & { version?: string };
export type LotSummary = Pick<HisHopeLotDto, "id" | "sku" | "quantity" | "uom" | "disposition" | "bestBefore"> & { lotCode?: string };
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
  private readonly baseUrl = `${resolveMobileApiOrigin()}/api/v1/manufacturing`;

  getProductionBatches(status?: string): Observable<ProductionBatch[]> {
    return this.http.get<ProductionBatch[]>(`${this.baseUrl}/production-batches`, {
      params: status ? { status } : {},
    });
  }

  getManufacturingSummary(): Observable<ManufacturingSummary> {
    return this.http.get<ManufacturingSummary>(`${this.baseUrl}/dashboard/manufacturing-summary`);
  }

  getProductionKpis(): Observable<ProductionKpi> {
    return this.http.get<ProductionKpi>(`${this.baseUrl}/dashboard/production-kpis`);
  }

  getOee(machineId?: string): Observable<ManufacturingOee> {
    return this.http.get<ManufacturingOee>(`${this.baseUrl}/dashboard/oee`, {
      params: machineId ? { machineId } : {},
    });
  }

  getExceptions(expiryWithinDays = 7, downtimeThresholdHours = 4): Observable<ManufacturingException[]> {
    return this.http.get<ManufacturingException[]>(`${this.baseUrl}/dashboard/exceptions`, {
      params: { expiryWithinDays, downtimeThresholdHours },
    });
  }

  getProductionCosts(): Observable<ManufacturingProductionCost> {
    return this.http.get<ManufacturingProductionCost>(`${this.baseUrl}/dashboard/production-costs`);
  }

  getAvailability(sku: string): Observable<ManufacturingAvailability> {
    return this.http.get<ManufacturingAvailability>(
      `${this.baseUrl}/products/${encodeURIComponent(sku)}/availability`,
    );
  }

  getFefoLots(sku: string): Observable<FefoLot[]> {
    return this.http.get<FefoLot[]>(
      `${this.baseUrl}/products/${encodeURIComponent(sku)}/fefo`,
      { params: { limit: "10" } },
    );
  }

  getLots(sku?: string): Observable<LotSummary[]> {
    return this.http.get<LotSummary[]>(`${this.baseUrl}/lots`, {
      params: sku ? { sku } : {},
    });
  }

  getInspectionPlanVersions(status = "Approved"): Observable<InspectionPlanVersion[]> {
    return this.http.get<InspectionPlanVersion[]>(`${this.baseUrl}/inspection-plan-versions`, {
      params: { status },
    });
  }

  getLotGenealogy(lotId: string, direction = "upstream"): Observable<LotGenealogy> {
    return this.http.get<LotGenealogy>(`${this.baseUrl}/lots/${lotId}/genealogy`, {
      params: { direction },
    });
  }

  getRecallImpact(lotId: string): Observable<RecallImpact> {
    return this.http.get<RecallImpact>(`${this.baseUrl}/lots/${lotId}/recall-impact`, {
      params: { maxLots: "500" },
    });
  }

  getLotQualityInspections(lotId: string): Observable<QualityInspection[]> {
    return this.http.get<QualityInspection[]>(`${this.baseUrl}/lots/${lotId}/quality-inspections`, {
      params: { limit: "25" },
    });
  }

  getLotStatusHistory(lotId: string): Observable<LotStatusHistory[]> {
    return this.http.get<LotStatusHistory[]>(`${this.baseUrl}/lots/${lotId}/status-history`, {
      params: { limit: "25" },
    });
  }

  getLotInventoryTransactions(lotId: string): Observable<InventoryTransaction[]> {
    return this.http.get<InventoryTransaction[]>(`${this.baseUrl}/lots/${lotId}/inventory-transactions`, {
      params: { limit: "50" },
    });
  }

  getMaintenanceWorkOrders(status = "Open"): Observable<MaintenanceWorkOrder[]> {
    return this.http.get<MaintenanceWorkOrder[]>(`${this.baseUrl}/maintenance-work-orders`, {
      params: { status },
    });
  }

  getMaintenancePlans(active = true): Observable<MaintenancePlan[]> {
    return this.http.get<MaintenancePlan[]>(`${this.baseUrl}/maintenance-plans`, {
      params: { active },
    });
  }

  getMachines(status?: string): Observable<Machine[]> {
    return this.http.get<Machine[]>(`${this.baseUrl}/machines`, {
      params: status ? { status } : {},
    });
  }

  getMachineHealth(dueWithinDays = 7): Observable<MachineHealth> {
    return this.http.get<MachineHealth>(`${this.baseUrl}/dashboard/machine-health`, {
      params: { dueWithinDays },
    });
  }

  getMachineCalibrations(machineId: string): Observable<MachineCalibration[]> {
    return this.http.get<MachineCalibration[]>(`${this.baseUrl}/machines/${machineId}/calibrations`, {
      params: { limit: "10" },
    });
  }

  getMachineTelemetry(machineId: string): Observable<MachineTelemetry[]> {
    return this.http.get<MachineTelemetry[]>(`${this.baseUrl}/machines/${machineId}/telemetry`, {
      params: { limit: "10" },
    });
  }

  recordProductionOperation(operation: QueuedOperation): Promise<OperationTransportResult> {
    return this.postQueued(`${this.baseUrl}${operation.endpoint}`, operation);
  }

  createQualityInspection(operation: QueuedOperation): Promise<OperationTransportResult> {
    return this.postQueued(`${this.baseUrl}/quality-inspections`, operation);
  }

  createQualitySample(operation: QueuedOperation): Promise<OperationTransportResult> {
    return this.postQueued(`${this.baseUrl}/quality-samples`, operation);
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
