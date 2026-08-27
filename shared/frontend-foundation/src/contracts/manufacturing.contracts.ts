/** Mirrors `His.Hope.Contracts.Manufacturing` (camelCase JSON). */

export interface HisHopeLotDto {
  id: string;
  tenantKey: string;
  sku: string;
  quantity: number;
  uom: string;
  disposition: string;
  bestBefore: string | null;
  createdAt: string;
  updatedAt?: string | null;
}

export interface HisHopeManufacturingDashboardSummaryDto {
  tenantKey: string;
  lotCount: number;
  releasedQuantity: number;
  quarantinedQuantity: number;
  transformationCount: number;
  averageYieldPercent: number;
  totalLossQuantity: number;
  openProductionOrderCount: number;
  activeBatchCount: number;
  pendingInspectionCount: number;
  failedInspectionCount: number;
  asOf: string;
}

export interface HisHopeManufacturingProductionKpiDto {
  tenantKey: string;
  completedBatchCount: number;
  plannedOutputQuantity: number;
  actualOutputQuantity: number;
  totalInputQuantity: number;
  totalLossQuantity: number;
  averageYieldPercent: number;
  targetYieldPercent: number;
  yieldVariancePercent: number;
  asOf: string;
  dataCompleteness?: string;
  sourceEventTypes?: string[];
}

export interface HisHopeLotRelationDto {
  transformationId: string;
  lotId: string;
  sku: string;
  role: string;
  quantity: number;
}

export interface HisHopeGenealogyDto {
  lot: HisHopeLotDto;
  relations: HisHopeLotRelationDto[];
}
export interface HisHopeRecallImpactLotDto {
  lotId: string; sku: string; lotCode: string; disposition: string; quantity: number; uom: string; relation: string; batchNumber: string | null;
}
export interface HisHopeRecallImpactDto {
  rootLotId: string; tenantKey: string; impactedLotCount: number; impactedBatchCount: number; lots: HisHopeRecallImpactLotDto[]; asOf: string;
}
export interface HisHopeEpcisEventDto { eventId: string; eventType: string; occurredAt: string; content: string; }
export interface HisHopeEpcisDocumentDto { id: string; specVersion: string; type: string; events: HisHopeEpcisEventDto[]; exportedAt: string; }

export interface HisHopeFefoLotDto {
  lotId: string;
  sku: string;
  quantity: number;
  reservedQuantity: number;
  availableQuantity: number;
  uom: string;
  bestBefore: string | null;
  createdAt: string;
  lotCode: string;
  lotType: string;
  originCountryCode: string | null;
  manufacturedOn: string | null;
  receivedAt: string | null;
  facilityCode: string | null;
  storageLocationCode: string | null;
  certificateOfAnalysisReference: string | null;
  sourceLotCode: string | null;
  qualityStatus: string;
  createdBy: string | null;
  updatedAt: string | null;
}

export interface HisHopeLotStatusHistoryDto {
  id: string;
  lotId: string;
  tenantKey: string;
  fromDisposition: string;
  toDisposition: string;
  actor: string;
  reasonCode: string | null;
  evidenceReference: string | null;
  correlationId: string;
  occurredAt: string;
}

export interface HisHopeInventoryTransactionDto {
  id: string;
  tenantKey: string;
  lotId: string;
  transactionType: string;
  quantity: number;
  uom: string;
  facilityId: string;
  stockStatus: string;
  correlationId: string;
  occurredAt: string;
}

export interface HisHopeEventReceiptDto {
  id: string;
  eventType: string;
  aggregateId: string;
  receivedAt: string;
}

export interface HisHopeCostProjectionComponentDto {
  ingredientSku: string;
  uom: string;
  requiredInputQuantity: number;
  estimatedUnitPrice: number;
  estimatedCost: number;
  hasPrice: boolean;
}

export interface HisHopeCostProjectionDto {
  tenantKey: string;
  recipeId: string;
  productSku: string;
  recipeVersion: number;
  outputUom: string;
  plannedOutputQuantity: number;
  targetYieldPercent: number;
  projectedLossQuantity: number;
  estimatedMaterialCost: number;
  estimatedMaterialCostPerOutputUnit: number;
  components: HisHopeCostProjectionComponentDto[];
  missingPriceSkus: string[];
  generatedAt: string;
}

export interface HisHopeQualityInspectionDto {
  id: string;
  lotId: string;
  tenantKey: string;
  status: string;
  moisturePercent: number;
  inspector: string;
  notes: string | null;
  inspectedAt: string;
  results: HisHopeQualityTestResultDto[] | null;
  specificationReference: string | null;
}

export interface HisHopeQualityTestResultDto {
  id: string;
  testCode: string;
  testName: string;
  measuredValue: number;
  uom: string;
  result: string;
  lowerLimit: number | null;
  upperLimit: number | null;
  method: string | null;
  evidenceReference: string | null;
}

export interface HisHopeQualitySampleDto {
  id: string;
  inspectionId: string;
  lotId: string;
  tenantKey: string;
  sampleCode: string;
  collectedBy: string;
  collectedAt: string;
  disposition: string;
  dispositionReason?: string | null;
  disposedBy?: string | null;
  disposedAt?: string | null;
  location?: string | null;
  notes?: string | null;
  createdAt: string;
}

export interface HisHopeInspectionPlanVersionDto {
  id: string;
  tenantKey: string;
  planCode: string;
  productSku: string;
  version: number;
  samplingMethod: string;
  samplingFrequency: string;
  acceptanceCriteria: string;
  status: string;
  effectiveFrom?: string | null;
  effectiveTo?: string | null;
  approvedBy?: string | null;
  approvedAt?: string | null;
  createdBy?: string | null;
  createdAt: string;
}

export interface HisHopeManufacturingMachineHealthDto {
  tenantKey: string;
  totalMachineCount: number;
  availableMachineCount: number;
  runningMachineCount: number;
  maintenanceMachineCount: number;
  inactiveMachineCount: number;
  overdueMaintenanceCount: number;
  dueWithinDaysCount: number;
  asOf: string;
}

export interface HisHopeManufacturingOeeDto {
  tenantKey: string;
  machineId?: string | null;
  status: string;
  oeePercent?: number | null;
  availabilityPercent?: number | null;
  performancePercent?: number | null;
  qualityPercent?: number | null;
  plannedProductionMinutes: number;
  runMinutes: number;
  goodQuantity: number;
  rejectQuantity: number;
  idealRatePerMinute?: number | null;
  missingMetrics: string[];
  asOf: string;
}

export interface HisHopeManufacturingMachineDto {
  id: string;
  tenantKey: string;
  code: string;
  name: string;
  status: string;
  lastMaintenanceAt?: string | null;
  nextMaintenanceAt?: string | null;
  active: boolean;
  createdAt: string;
}

export interface HisHopeMachineTelemetryDto {
  id: string;
  eventId: string;
  machineId: string;
  tenantKey: string;
  source: string;
  state?: string | null;
  meterName?: string | null;
  meterValue?: number | null;
  sequence?: number | null;
  observedAt: string;
  receivedAt: string;
}

export interface HisHopeManufacturingProductionCostDto {
  tenantKey: string;
  completedBatchCount: number;
  estimatedMaterialCost: number;
  estimatedCostPerOutputUnit: number;
  missingPriceSkus: string[];
  asOf: string;
}

export interface HisHopeManufacturingExecutiveExceptionDto {
  code: string;
  severity: string;
  title: string;
  detail: string;
  entityId: string | null;
  occurredAt: string;
}

export interface HisHopeManufacturingMaterialRequirementDto {
  tenantKey: string;
  productionOrderId: string | null;
  orderNumber: string | null;
  materialSku: string;
  requiredQuantity: number;
  releasedQuantity: number;
  reservedQuantity: number;
  availableQuantity: number;
  shortageQuantity: number;
  uom: string;
  asOf: string;
}

export interface HisHopeSalesForecastDto {
  id: string;
  tenantKey: string;
  productSku: string;
  periodStart: string;
  periodEnd: string;
  quantity: number;
  uom: string;
  source: string;
  actor: string;
  version: number;
  createdAt: string;
}

export interface HisHopeSalesForecastMaterialRequirementDto {
  forecastId: string;
  tenantKey: string;
  productSku: string;
  periodStart: string;
  periodEnd: string;
  materialSku: string;
  requiredQuantity: number;
  releasedQuantity: number;
  reservedQuantity: number;
  availableQuantity: number;
  shortageQuantity: number;
  uom: string;
  asOf: string;
}

export interface HisHopeManufacturingSalesAllocationDto {
  tenantKey: string;
  sku: string;
  salesOrderId: string;
  requestedQuantity: number;
  allocatedQuantity: number;
  shortageQuantity: number;
  reservations: HisHopeLotReservationDto[];
  asOf: string;
}

export interface HisHopeLotReservationDto {
  id: string;
  tenantKey: string;
  lotId: string;
  referenceType: string;
  referenceId: string;
  quantity: number;
  uom: string;
  status: string;
  createdAt: string;
  expiresAt: string | null;
}

export interface HisHopeManufacturingAvailabilityDto {
  tenantKey: string;
  sku: string;
  releasedQuantity: number;
  reservedQuantity: number;
  availableToPromiseQuantity: number;
  uom: string;
  asOf: string;
}

export interface HisHopeManufacturingDowntimeDto {
  id: string;
  machineId: string;
  tenantKey: string;
  reason: string;
  status: string;
  productionBatchId?: string | null;
  operationExecutionId?: string | null;
  startedAt: string;
  endedAt?: string | null;
  notes?: string | null;
  createdAt: string;
}

export interface HisHopeMaintenanceWorkOrderDto {
  id: string;
  machineId: string;
  tenantKey: string;
  status: string;
  maintenanceType: string;
  dueAt: string;
  assignedTo?: string | null;
  notes?: string | null;
  technician?: string | null;
  completedAt?: string | null;
  evidence?: string | null;
  createdAt: string;
}

export interface HisHopeRecipeDto {
  id: string;
  tenantKey: string;
  productSku: string;
  version: number;
  processStep: string;
  outputUom: string;
  targetYieldPercent: number;
  active: boolean;
  status: string;
  effectiveFrom: string | null;
  effectiveTo: string | null;
  approvedBy: string | null;
  approvedAt: string | null;
  components: Array<{ ingredientSku: string; quantity: number; uom: string }>;
  createdAt: string;
  productSpecificationId: string | null;
}

export interface HisHopeCreateRecipeRequest {
  tenantKey: string;
  productSku: string;
  version: number;
  processStep: string;
  outputUom: string;
  targetYieldPercent: number;
  components: Array<{ ingredientSku: string; quantity: number; uom: string }>;
  active?: boolean;
  status?: string;
  effectiveFrom?: string | null;
  effectiveTo?: string | null;
  productSpecificationId?: string | null;
}

export interface HisHopeProductSpecificationDto {
  id: string;
  tenantKey: string;
  productSku: string;
  targetMoisturePercent: number;
  packaging: string;
  shelfLifeDays: number;
  qcSpec: string;
  status: string;
  approvedBy: string | null;
  approvedAt: string | null;
  createdAt: string;
}

export interface HisHopeManufacturingDeviationDto {
  id: string;
  tenantKey: string;
  productionBatchId: string;
  type: string;
  description: string;
  impact: string;
  status: string;
  requestedBy: string;
  approvedBy: string | null;
  resolutionNotes: string | null;
  createdAt: string;
  approvedAt: string | null;
  closedAt: string | null;
}

export interface HisHopeManufacturingProductionKpiDto {
  tenantKey: string;
  completedBatchCount: number;
  plannedOutputQuantity: number;
  actualOutputQuantity: number;
  totalInputQuantity: number;
  totalLossQuantity: number;
  averageYieldPercent: number;
  targetYieldPercent: number;
  yieldVariancePercent: number;
  asOf: string;
}

export interface HisHopeProductionOrderDto {
  id: string;
  tenantKey: string;
  orderNumber: string;
  productSku: string;
  recipeId: string;
  recipeVersion: number;
  targetQuantity: number;
  outputUom: string;
  status: string;
  createdAt: string;
  releasedAt: string | null;
}

export interface HisHopeProductionBatchDto {
  id: string;
  tenantKey: string;
  productionOrderId: string;
  batchNumber: string;
  status: string;
  plannedQuantity: number;
  actualOutputQuantity: number;
  machineId: string | null;
  outputLotId: string | null;
  createdAt: string;
  startedAt: string | null;
  completedAt: string | null;
  operations?: HisHopeOperationExecutionDto[];
  inputs?: HisHopeProductionInputDto[];
}

export interface HisHopeMaintenancePlanDto {
  id: string;
  machineId: string;
  tenantKey: string;
  planCode: string;
  maintenanceType: string;
  frequencyDays: number;
  nextDueAt: string;
  checklist?: string | null;
  assignedTo?: string | null;
  active: boolean;
  lastGeneratedAt?: string | null;
  createdBy?: string | null;
  createdAt: string;
}

export interface HisHopeMachineCalibrationDto {
  id: string;
  machineId: string;
  tenantKey: string;
  calibrationType: string;
  certificateNumber: string;
  calibratedAt: string;
  nextDueAt: string;
  result: string;
  provider?: string | null;
  evidenceReference?: string | null;
  notes?: string | null;
  createdBy?: string | null;
  createdAt: string;
}

export interface HisHopeProductionBatchCostDto {
  id: string;
  productionBatchId: string;
  tenantKey: string;
  materialCost: number;
  laborCost: number;
  overheadCost: number;
  lossCost: number;
  totalCost: number;
  costPerOutputUnit: number;
  currency: string;
  calculatedAt: string;
  calculatedBy: string | null;
}

export interface HisHopeOperationExecutionDto {
  id: string;
  productionBatchId: string;
  sequence: number;
  processStep: string;
  operator: string;
  inputQuantity: number;
  outputQuantity: number;
  lossQuantity: number;
  status: string;
  required: boolean;
  qcStatus: string;
  startedAt: string | null;
  completedAt: string | null;
}

export interface HisHopeProductionInputDto {
  lotId: string;
  reservationId: string;
  quantity: number;
}

export interface HisHopeLossReviewDto {
  id: string;
  tenantKey: string;
  productionBatchId: string;
  operationExecutionId: string;
  decision: string;
  reviewer: string;
  notes: string | null;
  reviewedAt: string;
}

export interface HisHopeSupplierDto {
  id: string;
  tenantKey: string;
  code: string;
  name: string;
  active: boolean;
  createdAt: string;
  legalName: string;
  taxIdentificationNumber: string | null;
  contactName: string | null;
  contactEmail: string | null;
  contactPhone: string | null;
  countryCode: string | null;
  address: string | null;
  riskLevel: string;
  approvalStatus: string;
  approvedBy: string | null;
  approvedAt: string | null;
  lastReviewedAt: string | null;
  createdBy: string;
  updatedAt: string | null;
}

export interface HisHopeSupplierCertificateDto {
  id: string;
  tenantKey: string;
  supplierId: string;
  certificateType: string;
  certificateNumber: string;
  issuer: string;
  issuedAt: string;
  expiresAt: string;
  status: string;
  evidenceReference?: string | null;
  createdAt: string;
  createdBy: string;
}

export interface HisHopeSupplierMaterialApprovalDto {
  id: string;
  tenantKey: string;
  supplierId: string;
  materialSku: string;
  approvedUom: string;
  effectiveFrom: string;
  effectiveTo?: string | null;
  status: string;
  notes?: string | null;
  createdAt: string;
  createdBy: string;
}

export interface HisHopePurchaseOrderLineDto {
  id: string;
  materialSku: string;
  orderedQuantity: number;
  receivedQuantity: number;
  uom: string;
  unitPrice: number;
}
export interface HisHopeSupplierRfqDto { id: string; tenantKey: string; rfqNumber: string; materialSku: string; quantity: number; uom: string; status: string; neededBy: string | null; createdAt: string; }
export interface HisHopeSupplierQuotationDto { id: string; tenantKey: string; supplierRfqId: string; supplierId: string; unitPrice: number; currency: string; leadTimeDays: number; status: string; notes: string | null; createdAt: string; }
export interface HisHopeCapaDto { id: string; tenantKey: string; deviationId: string | null; supplierId: string | null; title: string; problemDescription: string; rootCause: string; correctiveAction: string; preventiveAction: string; owner: string; status: string; dueAt: string | null; createdAt: string; closedAt: string | null; }
export interface HisHopeSupplierEvaluationDto { id: string; tenantKey: string; supplierId: string; score: number; qualityNotes: string | null; deliveryNotes: string | null; notes: string | null; evaluatedBy: string; evaluatedAt: string; }

export interface HisHopeFacilityDto {
  id: string;
  tenantKey: string;
  code: string;
  name: string;
  active: boolean;
  createdAt: string;
}
export interface HisHopeWarehouseDto { id: string; tenantKey: string; facilityId: string; code: string; name: string; active: boolean; createdAt: string; }
export interface HisHopeStorageLocationDto { id: string; tenantKey: string; warehouseId: string; code: string; name: string; active: boolean; createdAt: string; }
export interface HisHopeUomDto { id: string; code: string; name: string; dimension: string; active: boolean; createdAt: string; }
export interface HisHopeUomConversionDto { id: string; fromCode: string; toCode: string; factor: number; active: boolean; createdAt: string; }
export interface HisHopeMaterialDto { id: string; tenantKey: string; sku: string; name: string; baseUomCode: string; materialType: string; active: boolean; createdAt: string; }
export interface HisHopeProductDto { id: string; tenantKey: string; sku: string; name: string; baseUomCode: string; active: boolean; createdAt: string; }

export interface HisHopeInboundReceiptDto {
  id: string;
  tenantKey: string;
  receiptNumber: string;
  purchaseOrderId: string;
  purchaseOrderLineId: string;
  lotId: string;
  supplierId: string;
  supplierLotCode: string;
  facilityId: string;
  quantity: number;
  uom: string;
  receivedAt: string;
  disposition: string;
  expiryDate: string | null;
  lotCode: string;
  storageLocationCode: string | null;
  deliveryNoteNumber: string | null;
  carrierName: string | null;
  vehicleReference: string | null;
  temperatureOnReceiptC: number | null;
  certificateOfAnalysisReference: string | null;
  receivedBy: string | null;
  acceptedQuantity: number;
  rejectedQuantity: number;
}

export interface HisHopePurchaseOrderDto {
  id: string;
  tenantKey: string;
  orderNumber: string;
  supplierId: string;
  supplierCode: string;
  status: string;
  currency: string;
  orderedAt: string;
  expectedAt: string | null;
  lines: HisHopePurchaseOrderLineDto[];
  supplierName?: string;
}

export type HisHopeLotDisposition =
  | "Released"
  | "Quarantined"
  | "Hold"
  | "Consumed"
  | string;

export type HisHopeProductionOrderStatus = "Draft" | "Released" | string;

export type HisHopeProductionBatchStatus =
  | "Planned"
  | "Started"
  | "Paused"
  | "Completed"
  | string;
