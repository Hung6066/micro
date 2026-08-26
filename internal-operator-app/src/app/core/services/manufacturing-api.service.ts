import { HttpClient, HttpParams } from "@angular/common/http";
import { Injectable, inject } from "@angular/core";
import { Observable } from "rxjs";
import {
  HisHopeLotDto,
  HisHopeLotReservationDto,
  HisHopeGenealogyDto,
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
  HisHopeSalesForecastDto,
  HisHopeSalesForecastMaterialRequirementDto,
  HisHopeManufacturingSalesAllocationDto,
  HisHopeManufacturingAvailabilityDto,
  HisHopeManufacturingDowntimeDto,
  HisHopeMaintenanceWorkOrderDto,
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
  HisHopeSupplierRfqDto,
  HisHopeSupplierQuotationDto,
  HisHopeUomConversionDto,
  HisHopeFacilityDto,
  HisHopeUomDto,
  HisHopeMaterialDto,
  HisHopeProductDto,
  HisHopeCapaDto,
  HisHopeSupplierEvaluationDto,
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

  getSalesForecastMaterialRequirements(forecastId: string): Observable<HisHopeSalesForecastMaterialRequirementDto[]> {
    return this.http.get<HisHopeSalesForecastMaterialRequirementDto[]>(`${this.base()}/planning/forecast-material-requirements/${forecastId}`);
  }

  allocateSales(sku: string, request: { salesOrderId: string; quantity: number; facilityId?: string; expiresAt?: string }): Observable<HisHopeManufacturingSalesAllocationDto> {
    return this.http.post<HisHopeManufacturingSalesAllocationDto>(`${this.base()}/sales/allocations/${encodeURIComponent(sku)}`, request);
  }

  getAvailability(sku: string): Observable<HisHopeManufacturingAvailabilityDto> {
    return this.http.get<HisHopeManufacturingAvailabilityDto>(
      `${this.base()}/products/${encodeURIComponent(sku)}/availability`,
    );
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

  createRecipe(request: HisHopeCreateRecipeRequest): Observable<HisHopeRecipeDto> {
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

  createProductSpecification(request: { tenantKey: string; productSku: string; targetMoisturePercent: number; packaging: string; shelfLifeDays: number; qcSpec: string }): Observable<HisHopeProductSpecificationDto> {
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

  changeLotDisposition(lotId: string, disposition: string): Observable<HisHopeLotDto> {
    return this.http.post<HisHopeLotDto>(`${this.base()}/lots/${lotId}/disposition`, { disposition });
  }

  getLotGenealogy(lotId: string, direction: "upstream" | "downstream" = "upstream"): Observable<HisHopeGenealogyDto> {
    return this.http.get<HisHopeGenealogyDto>(`${this.base()}/lots/${lotId}/genealogy`, { params: { direction } });
  }

  getLotQualityInspections(lotId: string, limit = 25): Observable<HisHopeQualityInspectionDto[]> {
    return this.http.get<HisHopeQualityInspectionDto[]>(`${this.base()}/lots/${lotId}/quality-inspections`, { params: { limit } });
  }

  createQualityInspection(request: { lotId: string; tenantKey: string; status: string; moisturePercent: number; inspector: string; notes?: string }): Observable<HisHopeQualityInspectionDto> {
    return this.http.post<HisHopeQualityInspectionDto>(`${this.base()}/quality-inspections`, request);
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

  createFacility(request: { tenantKey: string; code: string; name: string; active?: boolean }): Observable<HisHopeFacilityDto> {
    return this.http.post<HisHopeFacilityDto>(`${this.base()}/facilities`, request);
  }

  getUoms(active = true): Observable<HisHopeUomDto[]> { return this.http.get<HisHopeUomDto[]>(`${this.base()}/uoms`, { params: { active: String(active) } }); }
  getMaterials(materialType?: string, active = true): Observable<HisHopeMaterialDto[]> { let params = new HttpParams().set("active", String(active)); if (materialType) params = params.set("materialType", materialType); return this.http.get<HisHopeMaterialDto[]>(`${this.base()}/materials`, { params }); }
  createUom(request: { code: string; name: string; dimension: string; active?: boolean }): Observable<HisHopeUomDto> { return this.http.post<HisHopeUomDto>(`${this.base()}/uoms`, request); }
  getUomConversions(active = true): Observable<HisHopeUomConversionDto[]> { return this.http.get<HisHopeUomConversionDto[]>(`${this.base()}/uom-conversions`, { params: { active: String(active) } }); }
  createUomConversion(request: { fromCode: string; toCode: string; factor: number; active?: boolean }): Observable<HisHopeUomConversionDto> { return this.http.post<HisHopeUomConversionDto>(`${this.base()}/uom-conversions`, request); }
  createMaterial(request: { tenantKey: string; sku: string; name: string; baseUomCode: string; materialType?: string; active?: boolean }): Observable<HisHopeMaterialDto> { return this.http.post<HisHopeMaterialDto>(`${this.base()}/materials`, request); }
  getProducts(active = true): Observable<HisHopeProductDto[]> { return this.http.get<HisHopeProductDto[]>(`${this.base()}/products`, { params: { active: String(active) } }); }
  createProduct(request: { tenantKey: string; sku: string; name: string; baseUomCode: string; active?: boolean }): Observable<HisHopeProductDto> { return this.http.post<HisHopeProductDto>(`${this.base()}/products`, request); }

  createSupplier(request: { tenantKey: string; code: string; name: string; active?: boolean }): Observable<HisHopeSupplierDto> {
    return this.http.post<HisHopeSupplierDto>(`${this.base()}/suppliers`, request);
  }

  updateSupplier(supplierId: string, request: { code: string; name: string; active: boolean }): Observable<HisHopeSupplierDto> {
    return this.http.patch<HisHopeSupplierDto>(`${this.base()}/suppliers/${supplierId}`, request);
  }

  getSupplierRfqs(status?: string): Observable<HisHopeSupplierRfqDto[]> { let params = new HttpParams(); if (status) params = params.set("status", status); return this.http.get<HisHopeSupplierRfqDto[]>(`${this.base()}/supplier-rfqs`, { params }); }
  createSupplierRfq(request: { tenantKey: string; rfqNumber: string; materialSku: string; quantity: number; uom: string; neededBy?: string }): Observable<HisHopeSupplierRfqDto> { return this.http.post<HisHopeSupplierRfqDto>(`${this.base()}/supplier-rfqs`, request); }
  createSupplierQuotation(rfqId: string, request: { supplierRfqId: string; supplierId: string; unitPrice: number; currency: string; leadTimeDays: number; notes?: string }): Observable<HisHopeSupplierQuotationDto> { return this.http.post<HisHopeSupplierQuotationDto>(`${this.base()}/supplier-rfqs/${rfqId}/quotations`, request); }
  getSupplierQuotations(rfqId: string): Observable<HisHopeSupplierQuotationDto[]> { return this.http.get<HisHopeSupplierQuotationDto[]>(`${this.base()}/supplier-rfqs/${rfqId}/quotations`); }
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
    tenantKey: string;
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
    },
  ): Observable<HisHopeInboundReceiptDto> {
    return this.http.post<HisHopeInboundReceiptDto>(
      `${this.base()}/purchase-orders/${purchaseOrderId}/receipts`,
      request,
    );
  }

  receiveInboundBatch(purchaseOrderId: string, receipts: Array<{ purchaseOrderId: string; purchaseOrderLineId: string; materialSku: string; receiptNumber: string; supplierLotCode: string; facilityId: string; quantity: number; expiryDate?: string }>): Observable<HisHopeInboundReceiptDto[]> {
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
