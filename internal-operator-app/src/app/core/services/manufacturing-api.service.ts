import { HttpClient, HttpParams } from "@angular/common/http";
import { Injectable, inject } from "@angular/core";
import { Observable, catchError, of, throwError } from "rxjs";
import {
  HisHopeLotDto,
  HisHopeLotReservationDto,
  HisHopeGenealogyDto,
  HisHopeRecallImpactDto,
  HisHopeEpcisDocumentDto,
  HisHopeFefoLotDto,
  HisHopeInventoryTransactionDto,
  HisHopeEventReceiptDto,
  HisHopeCostProjectionDto,
  HisHopeManufacturingDashboardSummaryDto,
  HisHopeManufacturingProductionKpiDto,
  HisHopeManufacturingMachineHealthDto,
  HisHopeManufacturingOeeDto,
  HisHopeManufacturingMachineDto,
  HisHopeMachineTelemetryDto,
  HisHopeInboundReceiptDto,
  HisHopeManufacturingProductionCostDto,
  HisHopeManufacturingExecutiveExceptionDto,
  HisHopeManufacturingMaterialRequirementDto,
  HisHopeQualityInspectionDto,
  HisHopeQualitySampleDto,
  HisHopeInspectionPlanVersionDto,
  HisHopeSalesForecastDto,
  HisHopeSalesForecastMaterialRequirementDto,
  HisHopeManufacturingSalesAllocationDto,
  HisHopeManufacturingAvailabilityDto,
  HisHopeManufacturingDowntimeDto,
  HisHopeMaintenanceWorkOrderDto,
  HisHopeMaintenancePlanDto,
  HisHopeMachineCalibrationDto,
  HisHopeRecipeDto,
  HisHopeCreateRecipeRequest,
  HisHopeProductSpecificationDto,
  HisHopeManufacturingDeviationDto,
  HisHopeProductionBatchDto,
  HisHopeProductionOrderDto,
  HisHopeOperationExecutionDto,
  HisHopeLossReviewDto,
  HisHopePurchaseOrderDto,
  HisHopeSupplierDto,
  HisHopeSupplierCertificateDto,
  HisHopeSupplierMaterialApprovalDto,
  HisHopeSupplierRfqDto,
  HisHopeSupplierQuotationDto,
  HisHopeUomConversionDto,
  HisHopeFacilityDto,
  HisHopeWarehouseDto,
  HisHopeStorageLocationDto,
  HisHopeUomDto,
  HisHopeMaterialDto,
  HisHopeProductDto,
  HisHopeCapaDto,
  HisHopeSupplierEvaluationDto,
  HisHopeProductionBatchCostDto,
  HisHopeEntityStatusHistoryDto,
  HisHopeCrossEntityWorkflowTraceDto,
  HisHopeManufacturingWorkflowDefinitionDto,
  HisHopeOperationMeasurementDto,
  HisHopeSalesActualDto,
  HisHopeMlFeatureSnapshotDto,
  HisHopeMlDatasetQualityDto,
} from "@his-hope/frontend-foundation/contracts";
import { environment } from "../../../environments/environment";

function manufacturingBaseUrl(): string | null {
  return "manufacturingApiUrl" in environment
    ? (environment.manufacturingApiUrl as string | undefined) ?? null
    : null;
}

@Injectable({ providedIn: "root" })
export class ManufacturingApiService {
  private readonly http = inject(HttpClient);

  isEnabled(): boolean {
    return !!manufacturingBaseUrl();
  }

  getDashboardSummary(): Observable<HisHopeManufacturingDashboardSummaryDto> {
    return this.http.get<HisHopeManufacturingDashboardSummaryDto>(
      `${this.base()}/dashboard/manufacturing-summary`,
    );
  }

  getProductionKpis(): Observable<HisHopeManufacturingProductionKpiDto> {
    return this.http.get<HisHopeManufacturingProductionKpiDto>(
      `${this.base()}/dashboard/production-kpis`,
    );
  }

  getMachineHealth(): Observable<HisHopeManufacturingMachineHealthDto> {
    return this.http.get<HisHopeManufacturingMachineHealthDto>(
      `${this.base()}/dashboard/machine-health`,
    );
  }

  getOee(machineId?: string): Observable<HisHopeManufacturingOeeDto> {
    let params = new HttpParams();
    if (machineId) params = params.set("machineId", machineId);
    return this.http.get<HisHopeManufacturingOeeDto>(`${this.base()}/dashboard/oee`, { params });
  }

  getMachines(status?: string): Observable<HisHopeManufacturingMachineDto[]> {
    let params = new HttpParams();
    if (status) params = params.set("status", status);
    return this.http.get<HisHopeManufacturingMachineDto[]>(`${this.base()}/machines`, { params });
  }

  getProductionCosts(): Observable<HisHopeManufacturingProductionCostDto> {
    return this.http.get<HisHopeManufacturingProductionCostDto>(
      `${this.base()}/dashboard/production-costs`,
    );
  }

  getCostProjection(productSku: string, plannedQuantity: number, recipeVersion?: number): Observable<HisHopeCostProjectionDto> {
    let params = new HttpParams().set("productSku", productSku).set("plannedQuantity", String(plannedQuantity));
    if (recipeVersion !== undefined) params = params.set("recipeVersion", String(recipeVersion));
    return this.http.get<HisHopeCostProjectionDto>(`${this.base()}/dashboard/cost-projection`, { params });
  }

  getExecutiveExceptions(): Observable<HisHopeManufacturingExecutiveExceptionDto[]> {
    return this.http.get<HisHopeManufacturingExecutiveExceptionDto[]>(`${this.base()}/dashboard/exceptions`);
  }

  getMaterialRequirements(productionOrderId?: string): Observable<HisHopeManufacturingMaterialRequirementDto[]> {
    let params = new HttpParams();
    if (productionOrderId) params = params.set("productionOrderId", productionOrderId);
    return this.http.get<HisHopeManufacturingMaterialRequirementDto[]>(`${this.base()}/planning/material-requirements`, { params });
  }

  getSalesForecasts(productSku?: string): Observable<HisHopeSalesForecastDto[]> {
    let params = new HttpParams();
    if (productSku) params = params.set("productSku", productSku);
    return this.http.get<HisHopeSalesForecastDto[]>(`${this.base()}/sales/forecasts`, { params });
  }

  createSalesForecast(request: { productSku: string; periodStart: string; periodEnd: string; quantity: number; uom: string; source?: string; actor?: string; version?: number }): Observable<HisHopeSalesForecastDto> {
    return this.http.post<HisHopeSalesForecastDto>(`${this.base()}/sales/forecasts`, request);
  }

  getOperationMeasurements(batchId: string, limit = 500): Observable<HisHopeOperationMeasurementDto[]> {
    return this.http.get<HisHopeOperationMeasurementDto[]>(`${this.base()}/production-batches/${batchId}/measurements`, { params: { limit } });
  }

  recordOperationMeasurement(batchId: string, request: { productionBatchId: string; measurementType: string; value: number; uom: string; measuredAt: string; operationExecutionId?: string; machineId?: string; lotId?: string; source?: string; sequence?: number; notes?: string }): Observable<HisHopeOperationMeasurementDto> {
    return this.http.post<HisHopeOperationMeasurementDto>(`${this.base()}/production-batches/${batchId}/measurements`, request);
  }

  getSalesActuals(productSku?: string, limit = 500): Observable<HisHopeSalesActualDto[]> {
    let params = new HttpParams().set("limit", String(limit));
    if (productSku) params = params.set("productSku", productSku);
    return this.http.get<HisHopeSalesActualDto[]>(`${this.base()}/sales/actuals`, { params });
  }

  recordSalesActual(request: { productSku: string; periodStart: string; periodEnd: string; quantity: number; uom: string; channel?: string; region?: string; source?: string; actor?: string }): Observable<HisHopeSalesActualDto> {
    return this.http.post<HisHopeSalesActualDto>(`${this.base()}/sales/actuals`, request);
  }

  getMlFeatureSnapshots(datasetKey: string, options?: { from?: string; to?: string; limit?: number }): Observable<HisHopeMlFeatureSnapshotDto[]> {
    let params = new HttpParams();
    if (options?.from) params = params.set("from", options.from);
    if (options?.to) params = params.set("to", options.to);
    if (options?.limit) params = params.set("limit", String(options.limit));
    return this.http.get<HisHopeMlFeatureSnapshotDto[]>(`${this.base()}/ml/datasets/${encodeURIComponent(datasetKey)}/snapshots`, { params });
  }

  createMlFeatureSnapshot(datasetKey: string, request: { datasetKey: string; entityType: string; entityId: string; asOf: string; featuresJson: string; labelJson?: string; sourceEventIdsJson?: string; split?: string; schemaVersion?: number }): Observable<HisHopeMlFeatureSnapshotDto> {
    return this.http.post<HisHopeMlFeatureSnapshotDto>(`${this.base()}/ml/datasets/${encodeURIComponent(datasetKey)}/snapshots`, request);
  }

  getMlDatasetQuality(datasetKey: string): Observable<HisHopeMlDatasetQualityDto> {
    return this.http.get<HisHopeMlDatasetQualityDto>(`${this.base()}/ml/datasets/${encodeURIComponent(datasetKey)}/quality`);
  }

  getSalesForecastMaterialRequirements(forecastId: string): Observable<HisHopeSalesForecastMaterialRequirementDto[]> {
    return this.http.get<HisHopeSalesForecastMaterialRequirementDto[]>(`${this.base()}/planning/forecast-material-requirements/${forecastId}`);
  }

  allocateSales(sku: string, request: { salesOrderId: string; quantity: number; facilityId?: string; expiresAt?: string }): Observable<HisHopeManufacturingSalesAllocationDto> {
    return this.http.post<HisHopeManufacturingSalesAllocationDto>(`${this.base()}/sales/allocations/${encodeURIComponent(sku)}`, request);
  }

  getSalesAllocations(options?: { sku?: string; salesOrderId?: string; limit?: number }): Observable<HisHopeManufacturingSalesAllocationDto[]> {
    let params = new HttpParams().set("limit", String(options?.limit ?? 100));
    if (options?.sku) params = params.set("sku", options.sku);
    if (options?.salesOrderId) params = params.set("salesOrderId", options.salesOrderId);
    return this.http.get<HisHopeManufacturingSalesAllocationDto[]>(`${this.base()}/sales/allocations`, { params });
  }

  getAvailability(sku: string): Observable<HisHopeManufacturingAvailabilityDto> {
    return this.http.get<HisHopeManufacturingAvailabilityDto>(
      `${this.base()}/products/${encodeURIComponent(sku)}/availability`,
    );
  }

  getFefoLots(sku: string, limit = 50): Observable<HisHopeFefoLotDto[]> {
    return this.http.get<HisHopeFefoLotDto[]>(`${this.base()}/products/${encodeURIComponent(sku)}/fefo`, { params: { limit } });
  }

  releaseReservation(reservationId: string): Observable<HisHopeLotReservationDto> {
    return this.http.post<HisHopeLotReservationDto>(`${this.base()}/reservations/${reservationId}/release`, {});
  }

  getInventoryTransactions(lotId: string, limit = 50): Observable<HisHopeInventoryTransactionDto[]> {
    return this.http.get<HisHopeInventoryTransactionDto[]>(`${this.base()}/lots/${lotId}/inventory-transactions`, { params: { limit } });
  }

  getEventReceipts(eventType?: string, limit = 50): Observable<HisHopeEventReceiptDto[]> {
    let params = new HttpParams().set("limit", String(limit));
    if (eventType) params = params.set("eventType", eventType);
    return this.http.get<HisHopeEventReceiptDto[]>(`${this.base()}/events/receipts`, { params });
  }

  getMachineDowntimes(machineId?: string, status?: string): Observable<HisHopeManufacturingDowntimeDto[]> {
    let params = new HttpParams();
    if (machineId) params = params.set("machineId", machineId);
    if (status) params = params.set("status", status);
    return this.http.get<HisHopeManufacturingDowntimeDto[]>(`${this.base()}/machine-downtimes`, { params });
  }

  getRecipes(productSku?: string, active?: boolean): Observable<HisHopeRecipeDto[]> {
    let params = new HttpParams();
    if (productSku) params = params.set("productSku", productSku);
    if (active !== undefined) params = params.set("active", String(active));
    return this.http.get<HisHopeRecipeDto[]>(`${this.base()}/recipes`, { params });
  }

  createRecipe(request: Omit<HisHopeCreateRecipeRequest, "tenantKey">): Observable<HisHopeRecipeDto> {
    return this.http.post<HisHopeRecipeDto>(`${this.base()}/recipes`, request);
  }

  submitRecipe(recipeId: string, actor: string): Observable<HisHopeRecipeDto> {
    return this.http.post<HisHopeRecipeDto>(`${this.base()}/recipes/${recipeId}/submit`, { actor });
  }

  approveRecipe(recipeId: string, actor: string, effectiveFrom?: string, effectiveTo?: string): Observable<HisHopeRecipeDto> {
    return this.http.post<HisHopeRecipeDto>(`${this.base()}/recipes/${recipeId}/approve`, { actor, effectiveFrom, effectiveTo });
  }

  retireRecipe(recipeId: string, actor: string): Observable<HisHopeRecipeDto> {
    return this.http.post<HisHopeRecipeDto>(`${this.base()}/recipes/${recipeId}/retire`, { actor });
  }

  getProductSpecifications(productSku?: string, status?: string): Observable<HisHopeProductSpecificationDto[]> {
    let params = new HttpParams();
    if (productSku) params = params.set("productSku", productSku);
    if (status) params = params.set("status", status);
    return this.http.get<HisHopeProductSpecificationDto[]>(`${this.base()}/product-specifications`, { params });
  }

  createProductSpecification(request: { productSku: string; targetMoisturePercent: number; packaging: string; shelfLifeDays: number; qcSpec: string }): Observable<HisHopeProductSpecificationDto> {
    return this.http.post<HisHopeProductSpecificationDto>(`${this.base()}/product-specifications`, request);
  }

  approveProductSpecification(specificationId: string, actor: string): Observable<HisHopeProductSpecificationDto> {
    return this.http.post<HisHopeProductSpecificationDto>(`${this.base()}/product-specifications/${specificationId}/approve`, { actor });
  }

  retireProductSpecification(specificationId: string, actor: string): Observable<HisHopeProductSpecificationDto> {
    return this.http.post<HisHopeProductSpecificationDto>(`${this.base()}/product-specifications/${specificationId}/retire`, { actor });
  }

  getDeviations(options?: { productionBatchId?: string; status?: string }): Observable<HisHopeManufacturingDeviationDto[]> {
    let params = new HttpParams();
    if (options?.productionBatchId) params = params.set("productionBatchId", options.productionBatchId);
    if (options?.status) params = params.set("status", options.status);
    return this.http.get<HisHopeManufacturingDeviationDto[]>(`${this.base()}/deviations`, { params });
  }

  createDeviation(batchId: string, request: { type: string; description: string; impact: string; requestedBy: string }): Observable<HisHopeManufacturingDeviationDto> {
    return this.http.post<HisHopeManufacturingDeviationDto>(`${this.base()}/production-batches/${batchId}/deviations`, request);
  }

  approveDeviation(deviationId: string, actor: string, notes?: string): Observable<HisHopeManufacturingDeviationDto> {
    return this.http.post<HisHopeManufacturingDeviationDto>(`${this.base()}/deviations/${deviationId}/approve`, { actor, notes });
  }

  rejectDeviation(deviationId: string, actor: string, notes?: string): Observable<HisHopeManufacturingDeviationDto> {
    return this.http.post<HisHopeManufacturingDeviationDto>(`${this.base()}/deviations/${deviationId}/reject`, { actor, notes });
  }

  closeDeviation(deviationId: string, actor: string, notes?: string): Observable<HisHopeManufacturingDeviationDto> {
    return this.http.post<HisHopeManufacturingDeviationDto>(`${this.base()}/deviations/${deviationId}/close`, { actor, notes });
  }

  getManufacturingWorkflow(entityType: string): Observable<HisHopeManufacturingWorkflowDefinitionDto> {
    return this.http.get<HisHopeManufacturingWorkflowDefinitionDto>(`${this.base()}/workflows/${entityType}`);
  }

  getBatchStatusHistory(batchId: string): Observable<HisHopeEntityStatusHistoryDto[]> {
    return this.http.get<HisHopeEntityStatusHistoryDto[]>(`${this.base()}/production-batches/${batchId}/status-history`);
  }

  getPurchaseOrderStatusHistory(purchaseOrderId: string): Observable<HisHopeEntityStatusHistoryDto[]> {
    return this.http.get<HisHopeEntityStatusHistoryDto[]>(`${this.base()}/purchase-orders/${purchaseOrderId}/status-history`);
  }

  getDeviationStatusHistory(deviationId: string): Observable<HisHopeEntityStatusHistoryDto[]> {
    return this.http.get<HisHopeEntityStatusHistoryDto[]>(`${this.base()}/deviations/${deviationId}/status-history`);
  }

  getCrossEntityWorkflow(entityType: string, entityId: string): Observable<HisHopeCrossEntityWorkflowTraceDto> {
    return this.http.get<HisHopeCrossEntityWorkflowTraceDto>(
      `${this.base()}/entities/${encodeURIComponent(entityType)}/${entityId}/cross-workflow`,
    );
  }

  openMachineDowntime(machineId: string, request: { reason: string; startedAt: string; productionBatchId?: string; operationExecutionId?: string; notes?: string }): Observable<HisHopeManufacturingDowntimeDto> {
    return this.http.post<HisHopeManufacturingDowntimeDto>(`${this.base()}/machines/${machineId}/downtimes`, request);
  }

  resolveMachineDowntime(machineId: string, downtimeId: string, request: { endedAt: string; notes?: string }): Observable<HisHopeManufacturingDowntimeDto> {
    return this.http.post<HisHopeManufacturingDowntimeDto>(`${this.base()}/machines/${machineId}/downtimes/${downtimeId}/resolve`, request);
  }

  getMaintenanceWorkOrders(machineId?: string, status?: string): Observable<HisHopeMaintenanceWorkOrderDto[]> {
    let params = new HttpParams();
    if (machineId) params = params.set("machineId", machineId);
    if (status) params = params.set("status", status);
    return this.http.get<HisHopeMaintenanceWorkOrderDto[]>(`${this.base()}/maintenance-work-orders`, { params });
  }

  generateDueMaintenanceWorkOrders(): Observable<HisHopeMaintenanceWorkOrderDto[]> {
    return this.http.post<HisHopeMaintenanceWorkOrderDto[]>(`${this.base()}/maintenance-work-orders/generate`, {});
  }

  getMachineTelemetry(machineId: string, limit = 50): Observable<HisHopeMachineTelemetryDto[]> {
    return this.http.get<HisHopeMachineTelemetryDto[]>(`${this.base()}/machines/${machineId}/telemetry`, { params: { limit } });
  }

  recordMachineTelemetry(machineId: string, request: { eventId: string; observedAt: string; source: string; state?: string; meterName?: string; meterValue?: number; sequence?: number }): Observable<HisHopeMachineTelemetryDto> {
    return this.http.post<HisHopeMachineTelemetryDto>(`${this.base()}/machines/${machineId}/telemetry`, request);
  }

  createMaintenanceWorkOrder(machineId: string, request: { dueAt: string; maintenanceType?: string; assignedTo?: string; notes?: string }): Observable<HisHopeMaintenanceWorkOrderDto> {
    return this.http.post<HisHopeMaintenanceWorkOrderDto>(`${this.base()}/machines/${machineId}/maintenance-work-orders`, request);
  }

  completeMaintenanceWorkOrder(machineId: string, workOrderId: string, request: { technician: string; completedAt: string; nextMaintenanceAt?: string; evidence?: string }): Observable<HisHopeMaintenanceWorkOrderDto> {
    return this.http.post<HisHopeMaintenanceWorkOrderDto>(`${this.base()}/machines/${machineId}/maintenance-work-orders/${workOrderId}/complete`, request);
  }

  getMaintenancePlans(machineId?: string, active?: boolean, limit = 100): Observable<HisHopeMaintenancePlanDto[]> {
    let params = new HttpParams().set("limit", String(limit));
    if (machineId) params = params.set("machineId", machineId);
    if (active !== undefined) params = params.set("active", String(active));
    return this.http.get<HisHopeMaintenancePlanDto[]>(`${this.base()}/maintenance-plans`, { params });
  }

  createMaintenancePlan(machineId: string, request: { planCode: string; maintenanceType: string; frequencyDays: number; nextDueAt: string; checklist?: string; assignedTo?: string; active?: boolean; createdBy?: string }): Observable<HisHopeMaintenancePlanDto> {
    return this.http.post<HisHopeMaintenancePlanDto>(`${this.base()}/machines/${machineId}/maintenance-plans`, request);
  }

  getMachineCalibrations(machineId: string, limit = 100): Observable<HisHopeMachineCalibrationDto[]> {
    return this.http.get<HisHopeMachineCalibrationDto[]>(`${this.base()}/machines/${machineId}/calibrations`, { params: { limit } });
  }

  createMachineCalibration(machineId: string, request: { calibrationType: string; certificateNumber: string; calibratedAt: string; nextDueAt: string; result?: string; provider?: string; evidenceReference?: string; notes?: string; createdBy?: string }): Observable<HisHopeMachineCalibrationDto> {
    return this.http.post<HisHopeMachineCalibrationDto>(`${this.base()}/machines/${machineId}/calibrations`, request);
  }

  getLots(options?: {
    sku?: string;
    disposition?: string;
    limit?: number;
  }): Observable<HisHopeLotDto[]> {
    let params = new HttpParams();
    if (options?.sku) params = params.set("sku", options.sku);
    if (options?.disposition) params = params.set("disposition", options.disposition);
    if (options?.limit) params = params.set("limit", String(options.limit));
    return this.http.get<HisHopeLotDto[]>(`${this.base()}/lots`, { params });
  }

  reserveLot(lotId: string, request: { referenceType: string; referenceId: string; quantity: number; facilityId?: string }): Observable<HisHopeLotReservationDto> {
    return this.http.post<HisHopeLotReservationDto>(`${this.base()}/lots/${lotId}/reservations`, request);
  }

  getLotReservations(lotId: string, status?: string, limit = 100): Observable<HisHopeLotReservationDto[]> {
    let params = new HttpParams().set("limit", String(limit));
    if (status) params = params.set("status", status);
    return this.http.get<HisHopeLotReservationDto[]>(`${this.base()}/lots/${lotId}/reservations`, { params });
  }

  getProductionOrders(status?: string): Observable<HisHopeProductionOrderDto[]> {
    let params = new HttpParams();
    if (status) params = params.set("status", status);
    return this.http.get<HisHopeProductionOrderDto[]>(
      `${this.base()}/production-orders`,
      { params },
    );
  }

  getProductionBatches(status?: string): Observable<HisHopeProductionBatchDto[]> {
    let params = new HttpParams();
    if (status) params = params.set("status", status);
    return this.http.get<HisHopeProductionBatchDto[]>(
      `${this.base()}/production-batches`,
      { params },
    );
  }

  releaseProductionOrder(orderId: string): Observable<HisHopeProductionOrderDto> {
    return this.http.post<HisHopeProductionOrderDto>(
      `${this.base()}/production-orders/${orderId}/release`,
      {},
    );
  }

  startProductionBatch(batchId: string): Observable<HisHopeProductionBatchDto> {
    return this.http.post<HisHopeProductionBatchDto>(
      `${this.base()}/production-batches/${batchId}/start`,
      {},
    );
  }

  createProductionBatch(request: { productionOrderId: string; batchNumber: string; plannedQuantity?: number; machineId?: string; inputs?: Array<{ lotId: string; reservationId: string; quantity: number }> }): Observable<HisHopeProductionBatchDto> {
    return this.http.post<HisHopeProductionBatchDto>(`${this.base()}/production-batches`, request);
  }

  createProductionOrder(request: { orderNumber: string; productSku: string; recipeId: string; targetQuantity: number; outputUom: string }): Observable<HisHopeProductionOrderDto> {
    return this.http.post<HisHopeProductionOrderDto>(`${this.base()}/production-orders`, request);
  }

  cancelProductionOrder(orderId: string): Observable<HisHopeProductionOrderDto> {
    return this.http.post<HisHopeProductionOrderDto>(`${this.base()}/production-orders/${orderId}/cancel`, {});
  }

  getProductionBatchCost(batchId: string): Observable<HisHopeProductionBatchCostDto | null> {
    return this.http.get<HisHopeProductionBatchCostDto>(`${this.base()}/production-batches/${batchId}/cost`).pipe(
      catchError((error) => error.status === 404 ? of(null) : throwError(() => error)),
    );
  }

  calculateProductionBatchCost(batchId: string, request: { laborCost?: number; overheadCost?: number; currency?: string; actor?: string }): Observable<HisHopeProductionBatchCostDto> {
    return this.http.post<HisHopeProductionBatchCostDto>(`${this.base()}/production-batches/${batchId}/cost`, request);
  }

  pauseProductionBatch(batchId: string): Observable<HisHopeProductionBatchDto> {
    return this.http.post<HisHopeProductionBatchDto>(`${this.base()}/production-batches/${batchId}/pause`, {});
  }

  resumeProductionBatch(batchId: string): Observable<HisHopeProductionBatchDto> {
    return this.http.post<HisHopeProductionBatchDto>(`${this.base()}/production-batches/${batchId}/resume`, {});
  }

  completeProductionBatch(batchId: string): Observable<HisHopeProductionBatchDto> {
    return this.http.post<HisHopeProductionBatchDto>(`${this.base()}/production-batches/${batchId}/complete`, {});
  }

  recordProductionOperation(batchId: string, request: {
    sequence: number;
    processStep: string;
    operator: string;
    inputQuantity: number;
    outputQuantity: number;
    required?: boolean;
    qcStatus?: string;
    startedAt?: string;
  }): Observable<HisHopeOperationExecutionDto> {
    return this.http.post<HisHopeOperationExecutionDto>(`${this.base()}/production-batches/${batchId}/operations`, request);
  }

  changeLotDisposition(lotId: string, disposition: string, expectedUpdatedAt?: string | null): Observable<HisHopeLotDto> {
    return this.http.post<HisHopeLotDto>(`${this.base()}/lots/${lotId}/disposition`, { disposition, expectedUpdatedAt });
  }

  getLotGenealogy(lotId: string, direction: "upstream" | "downstream" = "upstream"): Observable<HisHopeGenealogyDto> {
    return this.http.get<HisHopeGenealogyDto>(`${this.base()}/lots/${lotId}/genealogy`, { params: { direction } });
  }

  getLotQualityInspections(lotId: string, limit = 25): Observable<HisHopeQualityInspectionDto[]> {
    return this.http.get<HisHopeQualityInspectionDto[]>(`${this.base()}/lots/${lotId}/quality-inspections`, { params: { limit } });
  }

  createQualityInspection(request: { lotId: string; status: string; moisturePercent: number; inspector: string; notes?: string; specificationReference?: string; results?: Array<{ testCode: string; testName: string; measuredValue: number; uom: string; result: string; lowerLimit?: number; upperLimit?: number; method?: string; evidenceReference?: string }> }): Observable<HisHopeQualityInspectionDto> {
    return this.http.post<HisHopeQualityInspectionDto>(`${this.base()}/quality-inspections`, request);
  }

  getInspectionPlanVersions(productSku?: string, status?: string, limit = 100): Observable<HisHopeInspectionPlanVersionDto[]> {
    let params = new HttpParams().set("limit", String(limit));
    if (productSku) params = params.set("productSku", productSku);
    if (status) params = params.set("status", status);
    return this.http.get<HisHopeInspectionPlanVersionDto[]>(`${this.base()}/inspection-plan-versions`, { params });
  }

  createInspectionPlanVersion(request: { planCode: string; productSku: string; version: number; samplingMethod: string; samplingFrequency: string; acceptanceCriteria: string; status?: string; effectiveFrom?: string; effectiveTo?: string; createdBy?: string }): Observable<HisHopeInspectionPlanVersionDto> {
    return this.http.post<HisHopeInspectionPlanVersionDto>(`${this.base()}/inspection-plan-versions`, request);
  }

  updateInspectionPlanStatus(planId: string, status: string, actor = "operator"): Observable<HisHopeInspectionPlanVersionDto> {
    return this.http.post<HisHopeInspectionPlanVersionDto>(`${this.base()}/inspection-plan-versions/${planId}/status`, { actor }, { params: { status } });
  }

  getQualitySamples(options?: { inspectionId?: string; disposition?: string; limit?: number }): Observable<HisHopeQualitySampleDto[]> {
    let params = new HttpParams().set("limit", String(options?.limit ?? 100));
    if (options?.inspectionId) params = params.set("inspectionId", options.inspectionId);
    if (options?.disposition) params = params.set("disposition", options.disposition);
    return this.http.get<HisHopeQualitySampleDto[]>(`${this.base()}/quality-samples`, { params });
  }

  createQualitySample(request: { inspectionId: string; sampleCode: string; collectedBy: string; collectedAt?: string; location?: string; notes?: string }): Observable<HisHopeQualitySampleDto> {
    return this.http.post<HisHopeQualitySampleDto>(`${this.base()}/quality-samples`, request);
  }

  updateQualitySampleDisposition(sampleId: string, disposition: string, actor = "operator", reason?: string): Observable<HisHopeQualitySampleDto> {
    return this.http.post<HisHopeQualitySampleDto>(`${this.base()}/quality-samples/${sampleId}/disposition`, { disposition, actor, reason });
  }

  reviewLoss(
    batchId: string,
    operationId: string,
    request: { decision: "Approved" | "Rejected"; reviewer: string; notes?: string },
  ): Observable<HisHopeLossReviewDto> {
    return this.http.post<HisHopeLossReviewDto>(
      `${this.base()}/production-batches/${batchId}/operations/${operationId}/loss-review`,
      request,
    );
  }

  getSuppliers(active?: boolean): Observable<HisHopeSupplierDto[]> {
    let params = new HttpParams();
    if (active !== undefined) params = params.set("active", String(active));
    return this.http.get<HisHopeSupplierDto[]>(`${this.base()}/suppliers`, { params });
  }

  getFacilities(active = true): Observable<HisHopeFacilityDto[]> {
    return this.http.get<HisHopeFacilityDto[]>(`${this.base()}/facilities`, { params: { active: String(active) } });
  }

  createFacility(request: { code: string; name: string; active?: boolean }): Observable<HisHopeFacilityDto> {
    return this.http.post<HisHopeFacilityDto>(`${this.base()}/facilities`, request);
  }

  getRecallImpact(lotId: string, maxLots = 500): Observable<HisHopeRecallImpactDto> {
    return this.http.get<HisHopeRecallImpactDto>(`${this.base()}/lots/${lotId}/recall-impact`, { params: { maxLots: String(maxLots) } });
  }

  getEpcisEvents(from?: string, to?: string, limit = 500): Observable<HisHopeEpcisDocumentDto> {
    let params = new HttpParams().set("limit", String(limit));
    if (from) params = params.set("from", from);
    if (to) params = params.set("to", to);
    return this.http.get<HisHopeEpcisDocumentDto>(`${this.base()}/traceability/epcis`, { params });
  }
  updateFacility(id: string, request: { code: string; name: string; active: boolean }): Observable<HisHopeFacilityDto> { return this.http.patch<HisHopeFacilityDto>(`${this.base()}/facilities/${id}`, request); }
  getWarehouses(facilityId?: string, active = true): Observable<HisHopeWarehouseDto[]> { let params = new HttpParams().set("active", String(active)); if (facilityId) params = params.set("facilityId", facilityId); return this.http.get<HisHopeWarehouseDto[]>(`${this.base()}/warehouses`, { params }); }
  createWarehouse(request: { facilityId: string; code: string; name: string; active?: boolean }): Observable<HisHopeWarehouseDto> { return this.http.post<HisHopeWarehouseDto>(`${this.base()}/warehouses`, request); }
  updateWarehouse(id: string, request: { facilityId: string; code: string; name: string; active: boolean }): Observable<HisHopeWarehouseDto> { return this.http.patch<HisHopeWarehouseDto>(`${this.base()}/warehouses/${id}`, request); }
  getStorageLocations(warehouseId?: string, active = true): Observable<HisHopeStorageLocationDto[]> { let params = new HttpParams().set("active", String(active)); if (warehouseId) params = params.set("warehouseId", warehouseId); return this.http.get<HisHopeStorageLocationDto[]>(`${this.base()}/storage-locations`, { params }); }
  createStorageLocation(request: { warehouseId: string; code: string; name: string; active?: boolean }): Observable<HisHopeStorageLocationDto> { return this.http.post<HisHopeStorageLocationDto>(`${this.base()}/storage-locations`, request); }
  updateStorageLocation(id: string, request: { warehouseId: string; code: string; name: string; active: boolean }): Observable<HisHopeStorageLocationDto> { return this.http.patch<HisHopeStorageLocationDto>(`${this.base()}/storage-locations/${id}`, request); }

  getUoms(active = true): Observable<HisHopeUomDto[]> { return this.http.get<HisHopeUomDto[]>(`${this.base()}/uoms`, { params: { active: String(active) } }); }
  getMaterials(materialType?: string, active = true): Observable<HisHopeMaterialDto[]> { let params = new HttpParams().set("active", String(active)); if (materialType) params = params.set("materialType", materialType); return this.http.get<HisHopeMaterialDto[]>(`${this.base()}/materials`, { params }); }
  createUom(request: { code: string; name: string; dimension: string; active?: boolean }): Observable<HisHopeUomDto> { return this.http.post<HisHopeUomDto>(`${this.base()}/uoms`, request); }
  updateUom(id: string, request: { code: string; name: string; dimension: string; active: boolean }): Observable<HisHopeUomDto> { return this.http.patch<HisHopeUomDto>(`${this.base()}/uoms/${id}`, request); }
  getUomConversions(active = true): Observable<HisHopeUomConversionDto[]> { return this.http.get<HisHopeUomConversionDto[]>(`${this.base()}/uom-conversions`, { params: { active: String(active) } }); }
  createUomConversion(request: { fromCode: string; toCode: string; factor: number; active?: boolean }): Observable<HisHopeUomConversionDto> { return this.http.post<HisHopeUomConversionDto>(`${this.base()}/uom-conversions`, request); }
  updateUomConversion(id: string, request: { fromCode: string; toCode: string; factor: number; active: boolean }): Observable<HisHopeUomConversionDto> { return this.http.patch<HisHopeUomConversionDto>(`${this.base()}/uom-conversions/${id}`, request); }
  createMaterial(request: { sku: string; name: string; baseUomCode: string; materialType?: string; active?: boolean }): Observable<HisHopeMaterialDto> { return this.http.post<HisHopeMaterialDto>(`${this.base()}/materials`, request); }
  updateMaterial(id: string, request: { name: string; baseUomCode: string; materialType: string; active: boolean }): Observable<HisHopeMaterialDto> { return this.http.patch<HisHopeMaterialDto>(`${this.base()}/materials/${id}`, request); }
  getProducts(active = true): Observable<HisHopeProductDto[]> { return this.http.get<HisHopeProductDto[]>(`${this.base()}/products`, { params: { active: String(active) } }); }
  createProduct(request: { sku: string; name: string; baseUomCode: string; active?: boolean }): Observable<HisHopeProductDto> { return this.http.post<HisHopeProductDto>(`${this.base()}/products`, request); }
  updateProduct(id: string, request: { name: string; baseUomCode: string; active: boolean }): Observable<HisHopeProductDto> { return this.http.patch<HisHopeProductDto>(`${this.base()}/products/${id}`, request); }

  createSupplier(request: { code: string; name: string; active?: boolean; legalName?: string; taxIdentificationNumber?: string; contactName?: string; contactEmail?: string; contactPhone?: string; countryCode?: string; address?: string; riskLevel?: string }): Observable<HisHopeSupplierDto> {
    return this.http.post<HisHopeSupplierDto>(`${this.base()}/suppliers`, request);
  }

  updateSupplier(supplierId: string, request: { code: string; name: string; active: boolean; legalName?: string; taxIdentificationNumber?: string; contactName?: string; contactEmail?: string; contactPhone?: string; countryCode?: string; address?: string; riskLevel?: string }): Observable<HisHopeSupplierDto> {
    return this.http.patch<HisHopeSupplierDto>(`${this.base()}/suppliers/${supplierId}`, request);
  }

  updateSupplierApproval(supplierId: string, status: string, notes?: string): Observable<HisHopeSupplierDto> {
    return this.http.post<HisHopeSupplierDto>(`${this.base()}/suppliers/${supplierId}/approval`, { status, notes });
  }

  getSupplierCertificates(supplierId: string, limit = 100): Observable<HisHopeSupplierCertificateDto[]> {
    return this.http.get<HisHopeSupplierCertificateDto[]>(`${this.base()}/suppliers/${supplierId}/certificates`, { params: { limit } });
  }

  createSupplierCertificate(supplierId: string, request: { certificateType: string; certificateNumber: string; issuer: string; issuedAt: string; expiresAt: string; evidenceReference?: string }): Observable<HisHopeSupplierCertificateDto> {
    return this.http.post<HisHopeSupplierCertificateDto>(`${this.base()}/suppliers/${supplierId}/certificates`, request);
  }

  getSupplierMaterialApprovals(supplierId: string, limit = 100): Observable<HisHopeSupplierMaterialApprovalDto[]> {
    return this.http.get<HisHopeSupplierMaterialApprovalDto[]>(`${this.base()}/suppliers/${supplierId}/material-approvals`, { params: { limit } });
  }

  createSupplierMaterialApproval(supplierId: string, request: { materialSku: string; approvedUom: string; effectiveFrom: string; effectiveTo?: string; notes?: string }): Observable<HisHopeSupplierMaterialApprovalDto> {
    return this.http.post<HisHopeSupplierMaterialApprovalDto>(`${this.base()}/suppliers/${supplierId}/material-approvals`, request);
  }

  getSupplierRfqs(status?: string): Observable<HisHopeSupplierRfqDto[]> { let params = new HttpParams(); if (status) params = params.set("status", status); return this.http.get<HisHopeSupplierRfqDto[]>(`${this.base()}/supplier-rfqs`, { params }); }
  createSupplierRfq(request: { rfqNumber: string; materialSku: string; quantity: number; uom: string; neededBy?: string }): Observable<HisHopeSupplierRfqDto> { return this.http.post<HisHopeSupplierRfqDto>(`${this.base()}/supplier-rfqs`, request); }
  createSupplierQuotation(rfqId: string, request: { supplierRfqId: string; supplierId: string; unitPrice: number; currency: string; leadTimeDays: number; notes?: string }): Observable<HisHopeSupplierQuotationDto> { return this.http.post<HisHopeSupplierQuotationDto>(`${this.base()}/supplier-rfqs/${rfqId}/quotations`, request); }
  cancelProductionBatch(batchId: string): Observable<HisHopeProductionBatchDto> { return this.http.post<HisHopeProductionBatchDto>(`${this.base()}/production-batches/${batchId}/cancel`, {}); }
  getSupplierQuotations(rfqId: string): Observable<HisHopeSupplierQuotationDto[]> { return this.http.get<HisHopeSupplierQuotationDto[]>(`${this.base()}/supplier-rfqs/${rfqId}/quotations`); }
  updateSupplierQuotationStatus(quotationId: string, status: string): Observable<HisHopeSupplierQuotationDto> { return this.http.post<HisHopeSupplierQuotationDto>(`${this.base()}/supplier-quotations/${quotationId}/status`, { status, actor: "operator" }); }
  updateMachine(machineId: string, request: { code: string; name: string; status: string; nextMaintenanceAt?: string; active: boolean }): Observable<HisHopeManufacturingMachineDto> { return this.http.patch<HisHopeManufacturingMachineDto>(`${this.base()}/machines/${machineId}`, request); }
  getCapas(status?: string): Observable<HisHopeCapaDto[]> { let params = new HttpParams(); if (status) params = params.set("status", status); return this.http.get<HisHopeCapaDto[]>(`${this.base()}/capas`, { params }); }
  createCapa(request: { deviationId?: string; supplierId?: string; title: string; problemDescription: string; rootCause: string; correctiveAction: string; preventiveAction: string; owner: string; dueAt?: string }): Observable<HisHopeCapaDto> { return this.http.post<HisHopeCapaDto>(`${this.base()}/capas`, request); }
  updateCapaStatus(capaId: string, status: string, actor: string, notes?: string): Observable<HisHopeCapaDto> { return this.http.post<HisHopeCapaDto>(`${this.base()}/capas/${capaId}/status`, { status, actor, notes }); }
  getSupplierEvaluations(supplierId?: string): Observable<HisHopeSupplierEvaluationDto[]> { let params = new HttpParams(); if (supplierId) params = params.set("supplierId", supplierId); return this.http.get<HisHopeSupplierEvaluationDto[]>(`${this.base()}/supplier-evaluations`, { params }); }
  createSupplierEvaluation(request: { supplierId: string; score: number; qualityNotes?: string; deliveryNotes?: string; notes?: string }): Observable<HisHopeSupplierEvaluationDto> { return this.http.post<HisHopeSupplierEvaluationDto>(`${this.base()}/supplier-evaluations`, request); }

  getPurchaseOrders(status?: string): Observable<HisHopePurchaseOrderDto[]> {
    let params = new HttpParams();
    if (status) params = params.set("status", status);
    return this.http.get<HisHopePurchaseOrderDto[]>(
      `${this.base()}/purchase-orders`,
      { params },
    );
  }

  updatePurchaseOrderStatus(purchaseOrderId: string, status: string, actor = "operator"): Observable<HisHopePurchaseOrderDto> {
    return this.http.post<HisHopePurchaseOrderDto>(`${this.base()}/purchase-orders/${purchaseOrderId}/status`, { status, actor });
  }

  createPurchaseOrder(request: {
    supplierId: string;
    orderNumber: string;
    currency: string;
    expectedAt?: string;
    status?: string;
    lines: Array<{ materialSku: string; orderedQuantity: number; uom: string; unitPrice: number }>;
  }): Observable<HisHopePurchaseOrderDto> {
    return this.http.post<HisHopePurchaseOrderDto>(`${this.base()}/purchase-orders`, request);
  }

  receiveInboundLot(
    purchaseOrderId: string,
    request: {
      purchaseOrderId: string;
      purchaseOrderLineId: string;
      materialSku: string;
      receiptNumber: string;
      supplierLotCode: string;
      facilityId: string;
      quantity: number;
      expiryDate?: string;
      receivedAt?: string;
      traceabilityLotCode?: string;
      originCountryCode?: string;
      manufacturedOn?: string;
      storageLocationCode?: string;
      deliveryNoteNumber?: string;
      carrierName?: string;
      vehicleReference?: string;
      temperatureOnReceiptC?: number;
      certificateOfAnalysisReference?: string;
      receivedBy?: string;
      acceptedQuantity?: number;
      rejectedQuantity?: number;
    },
  ): Observable<HisHopeInboundReceiptDto> {
    return this.http.post<HisHopeInboundReceiptDto>(
      `${this.base()}/purchase-orders/${purchaseOrderId}/receipts`,
      request,
    );
  }

  receiveInboundBatch(purchaseOrderId: string, receipts: Array<{ purchaseOrderId: string; purchaseOrderLineId: string; materialSku: string; receiptNumber: string; supplierLotCode: string; facilityId: string; quantity: number; expiryDate?: string; traceabilityLotCode?: string; originCountryCode?: string; storageLocationCode?: string; deliveryNoteNumber?: string; certificateOfAnalysisReference?: string; receivedBy?: string; acceptedQuantity?: number; rejectedQuantity?: number }>): Observable<HisHopeInboundReceiptDto[]> {
    return this.http.post<HisHopeInboundReceiptDto[]>(`${this.base()}/purchase-orders/${purchaseOrderId}/receipts/batch`, { receipts });
  }

  getInboundReceipts(purchaseOrderId?: string): Observable<HisHopeInboundReceiptDto[]> {
    let params = new HttpParams();
    if (purchaseOrderId) params = params.set("purchaseOrderId", purchaseOrderId);
    return this.http.get<HisHopeInboundReceiptDto[]>(`${this.base()}/inbound-receipts`, { params });
  }

  private base(): string {
    const url = manufacturingBaseUrl();
    if (!url) {
      throw new Error("Manufacturing API is not configured for this shell.");
    }
    return url;
  }
}
