namespace His.Hope.Contracts.Manufacturing;

/// <summary>
/// Stable defaults used by manufacturing wire contracts. Domain transition
/// rules remain owned by ManufacturingService.Domain.
/// </summary>
public static class ManufacturingContractDefaults
{
    public const string ReleasedDisposition = "Released";
    public const string PendingQualityStatus = "Pending";
    public const string DraftStatus = "Draft";
    public const string ApprovedStatus = "Approved";
    public const string UnspecifiedLotType = "Unspecified";
    public const string ProductionProcessStep = "production";
    public const string SystemActor = "system";
    public const string UnknownChannel = "unknown";
    public const string SalesSource = "Sales";
    public const string SalesSourceLower = "sales";
    public const string AuthenticatedSessionSignature = "AuthenticatedSession";
    public const string AvailableMachineStatus = "Available";
    public const string PassResult = "Pass";
    public const string PreventiveMaintenanceType = "Preventive";
}

public sealed record CreateLotRequest(
    string TenantKey,
    string Sku,
    decimal Quantity,
    string Uom,
    string Disposition = ManufacturingContractDefaults.ReleasedDisposition,
    DateOnly? BestBefore = null,
    string? LotCode = null,
    string LotType = ManufacturingContractDefaults.UnspecifiedLotType,
    string? OriginCountryCode = null,
    DateOnly? ManufacturedOn = null,
    string? FacilityCode = null,
    string? StorageLocationCode = null,
    string? CertificateOfAnalysisReference = null,
    string? SourceLotCode = null,
    string? RecordedBy = null);
public sealed record CreateTransformationRequest(string TenantKey, string OutputSku, decimal OutputQuantity, string OutputUom, IReadOnlyList<TransformationInput> Inputs, string ProcessStep = ManufacturingContractDefaults.ProductionProcessStep, Guid? RecipeId = null, Guid? MachineId = null);
public sealed record TransformationInput(Guid LotId, decimal Quantity, Guid? ReservationId = null);
public sealed record LotDto(
    Guid Id,
    string TenantKey,
    string Sku,
    decimal Quantity,
    string Uom,
    string Disposition,
    DateOnly? BestBefore,
    DateTimeOffset CreatedAt,
    string LotCode = "",
    string LotType = "Unspecified",
    string? OriginCountryCode = null,
    DateOnly? ManufacturedOn = null,
    DateTimeOffset? ReceivedAt = null,
    string? FacilityCode = null,
    string? StorageLocationCode = null,
    string? CertificateOfAnalysisReference = null,
    string? SourceLotCode = null,
    string QualityStatus = ManufacturingContractDefaults.PendingQualityStatus,
    string? CreatedBy = null,
    DateTimeOffset? UpdatedAt = null);
public sealed record LotStatusHistoryDto(Guid Id, Guid LotId, string TenantKey, string FromDisposition, string ToDisposition, string Actor, string? ReasonCode, string? EvidenceReference, Guid CorrelationId, DateTimeOffset OccurredAt);
public sealed record EntityStatusHistoryDto(Guid Id, string EntityType, Guid EntityId, string TenantKey, string FromStatus, string ToStatus, string Actor, DateTimeOffset OccurredAt);
public sealed record WorkflowStepDefinitionDto(string Key, string I18nGroup);
public sealed record ManufacturingWorkflowDefinitionDto(string EntityType, IReadOnlyList<WorkflowStepDefinitionDto> Steps, IReadOnlyDictionary<string, string>? StatusAliases, IReadOnlyList<string>? TerminalStatuses);
public sealed record CrossEntityWorkflowStepDto(string Key, string EntityType, Guid EntityId, string Title, string Status, string Route, string State, DateTimeOffset? OccurredAt);
public sealed record CrossEntityWorkflowTraceDto(string AnchorEntityType, Guid AnchorEntityId, IReadOnlyList<CrossEntityWorkflowStepDto> Steps);
public sealed record TransformationDto(Guid Id, string TenantKey, string ProcessStep, Guid? RecipeId, Guid? MachineId, IReadOnlyList<TransformationInput> Inputs, LotDto Output, decimal InputQuantity, decimal YieldPercent, decimal LossQuantity, DateTimeOffset CreatedAt);
public sealed record GenealogyDto(LotDto Lot, IReadOnlyList<LotRelationDto> Relations);
public sealed record LotRelationDto(Guid TransformationId, Guid LotId, string Sku, string Role, decimal Quantity);
public sealed record RecallImpactLotDto(Guid LotId, string Sku, string LotCode, string Disposition, decimal Quantity, string Uom, string Relation, string? BatchNumber);
public sealed record RecallImpactDto(Guid RootLotId, string TenantKey, int ImpactedLotCount, int ImpactedBatchCount, IReadOnlyList<RecallImpactLotDto> Lots, DateTimeOffset AsOf);
public sealed record EpcisEventDto(Guid EventId, string EventType, DateTimeOffset OccurredAt, string Content);
public sealed record EpcisDocumentDto(string Id, string SpecVersion, string Type, IReadOnlyList<EpcisEventDto> Events, DateTimeOffset ExportedAt);
public sealed record AvailabilityDto(
    string TenantKey,
    string Sku,
    decimal ReleasedQuantity,
    decimal ReservedQuantity,
    decimal AvailableToPromiseQuantity,
    string Uom,
    DateTimeOffset AsOf);
public sealed record ManufacturingDashboardSummaryDto(string TenantKey, int LotCount, decimal ReleasedQuantity, decimal QuarantinedQuantity, int TransformationCount, decimal AverageYieldPercent, decimal TotalLossQuantity, int OpenProductionOrderCount, int ActiveBatchCount, int PendingInspectionCount, int FailedInspectionCount, DateTimeOffset AsOf);
public sealed record ManufacturingProductionKpiDto(string TenantKey, int CompletedBatchCount, decimal PlannedOutputQuantity, decimal ActualOutputQuantity, decimal TotalInputQuantity, decimal TotalLossQuantity, decimal AverageYieldPercent, decimal TargetYieldPercent, decimal YieldVariancePercent, DateTimeOffset AsOf, string DataCompleteness = "complete", IReadOnlyList<string>? SourceEventTypes = null);
public sealed record ManufacturingMachineHealthDto(string TenantKey, int TotalMachineCount, int AvailableMachineCount, int RunningMachineCount, int MaintenanceMachineCount, int InactiveMachineCount, int OverdueMaintenanceCount, int DueWithinDaysCount, DateTimeOffset AsOf);
public sealed record ManufacturingOeeDto(string TenantKey, Guid? MachineId, string Status, decimal? OeePercent, decimal? AvailabilityPercent, decimal? PerformancePercent, decimal? QualityPercent, decimal PlannedProductionMinutes, decimal RunMinutes, decimal GoodQuantity, decimal RejectQuantity, decimal? IdealRatePerMinute, IReadOnlyList<string> MissingMetrics, DateTimeOffset AsOf);
public sealed record ManufacturingProductionCostDto(string TenantKey, int CompletedBatchCount, decimal EstimatedMaterialCost, decimal EstimatedCostPerOutputUnit, IReadOnlyList<string> MissingPriceSkus, DateTimeOffset AsOf);
public sealed record ManufacturingExecutiveExceptionDto(string Code, string Severity, string Title, string Detail, Guid? EntityId, DateTimeOffset OccurredAt);
public sealed record ManufacturingMaterialRequirementDto(string TenantKey, string? ProductionOrderId, string? OrderNumber, string MaterialSku, decimal RequiredQuantity, decimal ReleasedQuantity, decimal ReservedQuantity, decimal AvailableQuantity, decimal ShortageQuantity, string Uom, DateTimeOffset AsOf);
public sealed record CreateSalesForecastRequest(string ProductSku, DateOnly PeriodStart, DateOnly PeriodEnd, decimal Quantity, string Uom, string Source = "Sales", string Actor = "system", int Version = 1);
public sealed record SalesForecastDto(Guid Id, string TenantKey, string ProductSku, DateOnly PeriodStart, DateOnly PeriodEnd, decimal Quantity, string Uom, string Source, string Actor, int Version, DateTimeOffset CreatedAt);
public sealed record SalesForecastMaterialRequirementDto(Guid ForecastId, string TenantKey, string ProductSku, DateOnly PeriodStart, DateOnly PeriodEnd, string MaterialSku, decimal RequiredQuantity, decimal ReleasedQuantity, decimal ReservedQuantity, decimal AvailableQuantity, decimal ShortageQuantity, string Uom, DateTimeOffset AsOf);
public sealed record EventReceiptDto(Guid Id, string EventType, string AggregateId, DateTime ReceivedAt);
public sealed record TransformationSummaryDto(Guid Id, string TenantKey, string ProcessStep, Guid? RecipeId, Guid? MachineId, Guid OutputLotId, decimal InputQuantity, decimal OutputQuantity, decimal YieldPercent, decimal LossQuantity, DateTimeOffset CreatedAt);
public sealed record LotDispositionRequest(string Disposition, string? Actor = null, string? ReasonCode = null, string? EvidenceReference = null, DateTimeOffset? ExpectedUpdatedAt = null);
public sealed record QualityTestResultRequest(string TestCode, string TestName, decimal MeasuredValue, string Uom, string Result, decimal? LowerLimit = null, decimal? UpperLimit = null, string? Method = null, string? EvidenceReference = null);
public sealed record QualityTestResultDto(Guid Id, string TestCode, string TestName, decimal MeasuredValue, string Uom, string Result, decimal? LowerLimit, decimal? UpperLimit, string? Method, string? EvidenceReference);
public sealed record CreateQualityInspectionRequest(Guid LotId, string TenantKey, string Status, decimal MoisturePercent, string Inspector, string? Notes = null, DateTimeOffset? InspectedAt = null, IReadOnlyList<QualityTestResultRequest>? Results = null, string? SpecificationReference = null, Guid? InspectionPlanVersionId = null);
public sealed record QualityInspectionDto(Guid Id, Guid LotId, string TenantKey, string Status, decimal MoisturePercent, string Inspector, string? Notes, DateTimeOffset InspectedAt, IReadOnlyList<QualityTestResultDto>? Results = null, string? SpecificationReference = null, Guid? InspectionPlanVersionId = null);
public sealed record CreateQualitySampleRequest(Guid InspectionId, string SampleCode, string CollectedBy, DateTimeOffset? CollectedAt = null, string? Location = null, string? Notes = null);
public sealed record QualitySampleDto(Guid Id, Guid InspectionId, Guid LotId, string TenantKey, string SampleCode, string CollectedBy, DateTimeOffset CollectedAt, string Disposition, string? DispositionReason, string? DisposedBy, DateTimeOffset? DisposedAt, string? Location, string? Notes, DateTimeOffset CreatedAt);
public sealed record QualitySampleDispositionRequest(string Disposition, string Actor, string? Reason = null);
public sealed record CreateInspectionPlanVersionRequest(string TenantKey, string PlanCode, string ProductSku, int Version, string SamplingMethod, string SamplingFrequency, string AcceptanceCriteria, string Status = ManufacturingContractDefaults.DraftStatus, DateTimeOffset? EffectiveFrom = null, DateTimeOffset? EffectiveTo = null, string? CreatedBy = null);
public sealed record InspectionPlanLifecycleRequest(string Actor, DateTimeOffset? EffectiveFrom = null, DateTimeOffset? EffectiveTo = null);
public sealed record InspectionPlanVersionDto(Guid Id, string TenantKey, string PlanCode, string ProductSku, int Version, string SamplingMethod, string SamplingFrequency, string AcceptanceCriteria, string Status, DateTimeOffset? EffectiveFrom, DateTimeOffset? EffectiveTo, string? ApprovedBy, DateTimeOffset? ApprovedAt, string? CreatedBy, DateTimeOffset CreatedAt);
public sealed record CreateProductSpecificationRequest(string TenantKey, string ProductSku, decimal TargetMoisturePercent, string Packaging, int ShelfLifeDays, string QcSpec, string Status = ManufacturingContractDefaults.DraftStatus);
public sealed record ProductSpecificationLifecycleRequest(string Actor);
public sealed record ProductSpecificationDto(Guid Id, string TenantKey, string ProductSku, decimal TargetMoisturePercent, string Packaging, int ShelfLifeDays, string QcSpec, string Status, string? ApprovedBy, DateTimeOffset? ApprovedAt, DateTimeOffset CreatedAt);
public sealed record CreateRecipeRequest(string TenantKey, string ProductSku, int Version, string ProcessStep, string OutputUom, decimal TargetYieldPercent, IReadOnlyList<RecipeComponentRequest>? Components = null, bool Active = true, string Status = ManufacturingContractDefaults.ApprovedStatus, DateTimeOffset? EffectiveFrom = null, DateTimeOffset? EffectiveTo = null, Guid? ProductSpecificationId = null);
public sealed record RecipeComponentRequest(string IngredientSku, decimal Quantity, string Uom);
public sealed record RecipeDto(Guid Id, string TenantKey, string ProductSku, int Version, string ProcessStep, string OutputUom, decimal TargetYieldPercent, bool Active, string Status, DateTimeOffset? EffectiveFrom, DateTimeOffset? EffectiveTo, string? ApprovedBy, DateTimeOffset? ApprovedAt, IReadOnlyList<RecipeComponentDto> Components, DateTimeOffset CreatedAt, Guid? ProductSpecificationId = null);
public sealed record RecipeLifecycleRequest(string Actor, DateTimeOffset? EffectiveFrom = null, DateTimeOffset? EffectiveTo = null);
public sealed record RecipeComponentDto(string IngredientSku, decimal Quantity, string Uom);
public sealed record CreateDeviationRequest(string Type, string Description, string Impact, string RequestedBy);
public sealed record DeviationActionRequest(string Actor, string? Notes = null);
public sealed record ManufacturingDeviationDto(Guid Id, string TenantKey, Guid ProductionBatchId, string Type, string Description, string Impact, string Status, string RequestedBy, string? ApprovedBy, string? ResolutionNotes, DateTimeOffset CreatedAt, DateTimeOffset? ApprovedAt, DateTimeOffset? ClosedAt);
public sealed record CreateSopArtifactRequest(string ArtifactKey, string Title, int Version, string Content, string ContentType = "text/markdown", string Status = ManufacturingContractDefaults.DraftStatus, DateTimeOffset? EffectiveFrom = null, DateTimeOffset? EffectiveTo = null, string? CreatedBy = null);
public sealed record SopArtifactLifecycleRequest(string Actor, DateTimeOffset? EffectiveFrom = null, DateTimeOffset? EffectiveTo = null);
public sealed record SopArtifactDto(Guid Id, string TenantKey, string ArtifactKey, string Title, int Version, string Content, string ContentType, string Status, string Checksum, DateTimeOffset? EffectiveFrom, DateTimeOffset? EffectiveTo, string? ApprovedBy, DateTimeOffset? ApprovedAt, string? CreatedBy, DateTimeOffset CreatedAt);
public sealed record SopArtifactAcknowledgmentRequest(string? Notes = null);
public sealed record SopArtifactAcknowledgmentDto(Guid Id, Guid SopArtifactId, string TenantKey, string Actor, string? Notes, DateTimeOffset AcknowledgedAt);
public sealed record CreateBusinessSignatureRequest(string EntityType, Guid EntityId, string Action, string Reason, string? EvidenceReference = null, string SignatureMethod = ManufacturingContractDefaults.AuthenticatedSessionSignature);
public sealed record BusinessSignatureDto(Guid Id, string TenantKey, string EntityType, Guid EntityId, string Action, string Reason, string? EvidenceReference, string Actor, string SignatureMethod, string SignatureHash, DateTimeOffset SignedAt);
public sealed record CreateMachineRequest(string TenantKey, string Code, string Name, string Status = ManufacturingContractDefaults.AvailableMachineStatus, DateTimeOffset? LastMaintenanceAt = null, DateTimeOffset? NextMaintenanceAt = null, bool Active = true);
public sealed record UpdateMachineRequest(string Code, string Name, string Status, DateTimeOffset? NextMaintenanceAt, bool Active);
public sealed record RecordMaintenanceRequest(DateTimeOffset MaintainedAt, DateTimeOffset? NextMaintenanceAt, string Status = ManufacturingContractDefaults.AvailableMachineStatus);
public sealed record MachineDto(Guid Id, string TenantKey, string Code, string Name, string Status, DateTimeOffset? LastMaintenanceAt, DateTimeOffset? NextMaintenanceAt, bool Active, DateTimeOffset CreatedAt);
public sealed record CreateMachineCalibrationRequest(string CalibrationType, string CertificateNumber, DateTimeOffset CalibratedAt, DateTimeOffset NextDueAt, string Result = ManufacturingContractDefaults.PassResult, string? Provider = null, string? EvidenceReference = null, string? Notes = null, string? CreatedBy = null);
public sealed record MachineCalibrationDto(Guid Id, Guid MachineId, string TenantKey, string CalibrationType, string CertificateNumber, DateTimeOffset CalibratedAt, DateTimeOffset NextDueAt, string Result, string? Provider, string? EvidenceReference, string? Notes, string? CreatedBy, DateTimeOffset CreatedAt);
public sealed record CreateMaintenanceWorkOrderRequest(DateTimeOffset DueAt, string MaintenanceType = ManufacturingContractDefaults.PreventiveMaintenanceType, string? AssignedTo = null, string? Notes = null);
public sealed record CompleteMaintenanceWorkOrderRequest(string Technician, DateTimeOffset CompletedAt, DateTimeOffset? NextMaintenanceAt = null, string? Evidence = null);
public sealed record MaintenanceWorkOrderDto(Guid Id, Guid MachineId, string TenantKey, string Status, string MaintenanceType, DateTimeOffset DueAt, string? AssignedTo, string? Notes, string? Technician, DateTimeOffset? CompletedAt, string? Evidence, DateTimeOffset CreatedAt);
public sealed record CreateMaintenancePlanRequest(Guid MachineId, string PlanCode, string MaintenanceType, int FrequencyDays, DateTimeOffset NextDueAt, string? Checklist = null, string? AssignedTo = null, bool Active = true, string? CreatedBy = null);
public sealed record MaintenancePlanDto(Guid Id, Guid MachineId, string TenantKey, string PlanCode, string MaintenanceType, int FrequencyDays, DateTimeOffset NextDueAt, string? Checklist, string? AssignedTo, bool Active, DateTimeOffset? LastGeneratedAt, string? CreatedBy, DateTimeOffset CreatedAt);
public sealed record GenerateMaintenanceWorkOrdersRequest(DateTimeOffset? AsOf = null);
public sealed record RecordMachineTelemetryRequest(Guid EventId, DateTimeOffset ObservedAt, string Source, string? State = null, string? MeterName = null, decimal? MeterValue = null, long? Sequence = null);
public sealed record MachineTelemetryDto(Guid Id, Guid EventId, Guid MachineId, string TenantKey, string Source, string? State, string? MeterName, decimal? MeterValue, long? Sequence, DateTimeOffset ObservedAt, DateTimeOffset ReceivedAt);
public sealed record RecordOperationMeasurementRequest(Guid ProductionBatchId, Guid? OperationExecutionId, Guid? MachineId, Guid? LotId, string MeasurementType, decimal Value, string Uom, DateTimeOffset MeasuredAt, string? Source = null, long? Sequence = null, string? Notes = null);
public sealed record OperationMeasurementDto(Guid Id, string TenantKey, Guid ProductionBatchId, Guid? OperationExecutionId, Guid? MachineId, Guid? LotId, string MeasurementType, decimal Value, string Uom, DateTimeOffset MeasuredAt, string RecordedBy, string Source, long? Sequence, string? Notes, DateTimeOffset CreatedAt);
public sealed record RecordSalesActualRequest(string ProductSku, DateOnly PeriodStart, DateOnly PeriodEnd, decimal Quantity, string Uom, string Channel = ManufacturingContractDefaults.UnknownChannel, string? Region = null, string Source = ManufacturingContractDefaults.SalesSourceLower, string Actor = ManufacturingContractDefaults.SystemActor);
public sealed record SalesActualDto(Guid Id, string TenantKey, string ProductSku, DateOnly PeriodStart, DateOnly PeriodEnd, decimal Quantity, string Uom, string Channel, string? Region, string Source, string Actor, DateTimeOffset CreatedAt);
public sealed record MlFeatureSnapshotRequest(string DatasetKey, string EntityType, Guid EntityId, DateTimeOffset AsOf, string FeaturesJson, string? LabelJson = null, string? SourceEventIdsJson = null, string? Split = null, int SchemaVersion = 1);
public sealed record MlFeatureSnapshotDto(Guid Id, string TenantKey, string DatasetKey, string EntityType, Guid EntityId, DateTimeOffset AsOf, string FeaturesJson, string? LabelJson, string? SourceEventIdsJson, string Split, int SchemaVersion, DateTimeOffset CreatedAt);
public sealed record MlDatasetQualityDto(string TenantKey, string DatasetKey, int RowCount, int LabeledRowCount, int FeatureSchemaVersion, DateTimeOffset AsOf, IReadOnlyList<string> Warnings);
public sealed record CreateDowntimeRequest(string Reason, DateTimeOffset StartedAt, Guid? ProductionBatchId = null, Guid? OperationExecutionId = null, string? Notes = null);
public sealed record ResolveDowntimeRequest(DateTimeOffset EndedAt, string? Notes = null);
public sealed record DowntimeDto(Guid Id, Guid MachineId, string TenantKey, string Reason, string Status, Guid? ProductionBatchId, Guid? OperationExecutionId, DateTimeOffset StartedAt, DateTimeOffset? EndedAt, string? Notes, DateTimeOffset CreatedAt);
public sealed record CreateProductionOrderRequest(string OrderNumber, string ProductSku, Guid RecipeId, decimal TargetQuantity, string OutputUom);
public sealed record ProductionOrderDto(Guid Id, string TenantKey, string OrderNumber, string ProductSku, Guid RecipeId, int RecipeVersion, decimal TargetQuantity, string OutputUom, string Status, DateTimeOffset CreatedAt, DateTimeOffset? ReleasedAt);
public sealed record CreateProductionBatchRequest(Guid ProductionOrderId, string BatchNumber, decimal? PlannedQuantity = null, Guid? MachineId = null, IReadOnlyList<ProductionInputRequest>? Inputs = null);
public sealed record ProductionInputRequest(Guid LotId, Guid ReservationId, decimal Quantity);
public sealed record ProductionBatchDto(Guid Id, string TenantKey, Guid ProductionOrderId, string BatchNumber, string Status, decimal PlannedQuantity, decimal ActualOutputQuantity, Guid? MachineId, Guid? OutputLotId, DateTimeOffset CreatedAt, DateTimeOffset? StartedAt, DateTimeOffset? CompletedAt, IReadOnlyList<OperationExecutionDto> Operations, IReadOnlyList<ProductionInputDto> Inputs);
public sealed record ProductionInputDto(Guid LotId, Guid ReservationId, decimal Quantity);
public sealed record RecordOperationRequest(int Sequence, string ProcessStep, string Operator, decimal InputQuantity, decimal OutputQuantity, bool Required = true, string QcStatus = ManufacturingContractDefaults.PendingQualityStatus, DateTimeOffset? StartedAt = null);
public sealed record OperationExecutionDto(Guid Id, Guid ProductionBatchId, int Sequence, string ProcessStep, string Operator, decimal InputQuantity, decimal OutputQuantity, decimal LossQuantity, string Status, bool Required, string QcStatus, DateTimeOffset? StartedAt, DateTimeOffset? CompletedAt);
public sealed record LossReviewRequest(string Decision, string Reviewer, string? Notes = null);
public sealed record LossReviewDto(Guid Id, string TenantKey, Guid ProductionBatchId, Guid OperationExecutionId, string Decision, string Reviewer, string? Notes, DateTimeOffset ReviewedAt);
public sealed record CreateLotReservationRequest(string ReferenceType, Guid ReferenceId, decimal Quantity, string? FacilityId = null, DateTimeOffset? ExpiresAt = null);
public sealed record CreateSalesAllocationRequest(Guid SalesOrderId, decimal Quantity, string? FacilityId = null, DateTimeOffset? ExpiresAt = null);
public sealed record SalesAllocationDto(string TenantKey, string Sku, Guid SalesOrderId, decimal RequestedQuantity, decimal AllocatedQuantity, decimal ShortageQuantity, IReadOnlyList<LotReservationDto> Reservations, DateTimeOffset AsOf);
public sealed record LotReservationDto(Guid Id, string TenantKey, Guid LotId, string ReferenceType, Guid ReferenceId, decimal Quantity, string Uom, string Status, DateTimeOffset CreatedAt, DateTimeOffset? ExpiresAt);
public sealed record FefoLotDto(Guid LotId, string Sku, decimal Quantity, decimal ReservedQuantity, decimal AvailableQuantity, string Uom, DateOnly? BestBefore, DateTimeOffset CreatedAt);
public sealed record CreateSupplierRequest(
    string TenantKey,
    string Code,
    string Name,
    bool Active = true,
    string? LegalName = null,
    string? TaxIdentificationNumber = null,
    string? ContactName = null,
    string? ContactEmail = null,
    string? ContactPhone = null,
    string? CountryCode = null,
    string? Address = null,
    string RiskLevel = "Standard",
    string? CreatedBy = null);
public sealed record UpdateSupplierRequest(
    string Code,
    string Name,
    bool Active,
    string? LegalName = null,
    string? TaxIdentificationNumber = null,
    string? ContactName = null,
    string? ContactEmail = null,
    string? ContactPhone = null,
    string? CountryCode = null,
    string? Address = null,
    string RiskLevel = "Standard");
public sealed record SupplierApprovalRequest(string Status, string? Actor = null, string? Notes = null);
public sealed record SupplierDto(
    Guid Id,
    string TenantKey,
    string Code,
    string Name,
    bool Active,
    DateTimeOffset CreatedAt,
    string LegalName = "",
    string? TaxIdentificationNumber = null,
    string? ContactName = null,
    string? ContactEmail = null,
    string? ContactPhone = null,
    string? CountryCode = null,
    string? Address = null,
    string RiskLevel = "Standard",
    string ApprovalStatus = "Draft",
    string? ApprovedBy = null,
    DateTimeOffset? ApprovedAt = null,
    DateTimeOffset? LastReviewedAt = null,
    string CreatedBy = "system",
    DateTimeOffset? UpdatedAt = null);
public sealed record CreateSupplierCertificateRequest(string CertificateType, string CertificateNumber, string Issuer, DateTimeOffset IssuedAt, DateTimeOffset ExpiresAt, string? EvidenceReference = null);
public sealed record SupplierCertificateDto(Guid Id, string TenantKey, Guid SupplierId, string CertificateType, string CertificateNumber, string Issuer, DateTimeOffset IssuedAt, DateTimeOffset ExpiresAt, string Status, string? EvidenceReference, DateTimeOffset CreatedAt, string CreatedBy);
public sealed record CreateSupplierMaterialApprovalRequest(string MaterialSku, string ApprovedUom, DateTimeOffset EffectiveFrom, DateTimeOffset? EffectiveTo = null, string? Notes = null);
public sealed record SupplierMaterialApprovalDto(Guid Id, string TenantKey, Guid SupplierId, string MaterialSku, string ApprovedUom, DateTimeOffset EffectiveFrom, DateTimeOffset? EffectiveTo, string Status, string? Notes, DateTimeOffset CreatedAt, string CreatedBy);
public sealed record CreateFacilityRequest(string TenantKey, string Code, string Name, bool Active = true);
public sealed record UpdateFacilityRequest(string Code, string Name, bool Active);
public sealed record FacilityDto(Guid Id, string TenantKey, string Code, string Name, bool Active, DateTimeOffset CreatedAt);
public sealed record CreateWarehouseRequest(string TenantKey, Guid FacilityId, string Code, string Name, bool Active = true);
public sealed record UpdateWarehouseRequest(Guid FacilityId, string Code, string Name, bool Active);
public sealed record WarehouseDto(Guid Id, string TenantKey, Guid FacilityId, string Code, string Name, bool Active, DateTimeOffset CreatedAt);
public sealed record CreateStorageLocationRequest(string TenantKey, Guid WarehouseId, string Code, string Name, bool Active = true);
public sealed record UpdateStorageLocationRequest(Guid WarehouseId, string Code, string Name, bool Active);
public sealed record StorageLocationDto(Guid Id, string TenantKey, Guid WarehouseId, string Code, string Name, bool Active, DateTimeOffset CreatedAt);
public sealed record CreateUomRequest(string Code, string Name, string Dimension, bool Active = true);
public sealed record UpdateUomRequest(string Code, string Name, string Dimension, bool Active);
public sealed record UomDto(Guid Id, string Code, string Name, string Dimension, bool Active, DateTimeOffset CreatedAt);
public sealed record CreateUomConversionRequest(string FromCode, string ToCode, decimal Factor, bool Active = true);
public sealed record UpdateUomConversionRequest(string FromCode, string ToCode, decimal Factor, bool Active);
public sealed record UomConversionDto(Guid Id, string FromCode, string ToCode, decimal Factor, bool Active, DateTimeOffset CreatedAt);
public sealed record CreateMaterialRequest(string TenantKey, string Sku, string Name, string BaseUomCode, string MaterialType = "RawMaterial", bool Active = true);
public sealed record UpdateMaterialRequest(string Name, string BaseUomCode, string MaterialType, bool Active);
public sealed record MaterialDto(Guid Id, string TenantKey, string Sku, string Name, string BaseUomCode, string MaterialType, bool Active, DateTimeOffset CreatedAt);
public sealed record CreateProductRequest(string TenantKey, string Sku, string Name, string BaseUomCode, bool Active = true);
public sealed record UpdateProductRequest(string Name, string BaseUomCode, bool Active);
public sealed record ProductDto(Guid Id, string TenantKey, string Sku, string Name, string BaseUomCode, bool Active, DateTimeOffset CreatedAt);
public sealed record CreateSupplierRfqRequest(string TenantKey, string RfqNumber, string MaterialSku, decimal Quantity, string Uom, DateTimeOffset? NeededBy = null);
public sealed record SupplierRfqDto(Guid Id, string TenantKey, string RfqNumber, string MaterialSku, decimal Quantity, string Uom, string Status, DateTimeOffset? NeededBy, DateTimeOffset CreatedAt);
public sealed record CreateSupplierQuotationRequest(Guid SupplierRfqId, Guid SupplierId, decimal UnitPrice, string Currency, int LeadTimeDays, string? Notes = null);
public sealed record SupplierQuotationDto(Guid Id, string TenantKey, Guid SupplierRfqId, Guid SupplierId, decimal UnitPrice, string Currency, int LeadTimeDays, string Status, string? Notes, DateTimeOffset CreatedAt);
public sealed record UpdateSupplierQuotationStatusRequest(string Status, string? Actor = null);
public sealed record CreateCapaRequest(Guid? DeviationId, Guid? SupplierId, string Title, string ProblemDescription, string RootCause, string CorrectiveAction, string PreventiveAction, string Owner, DateTimeOffset? DueAt = null);
public sealed record CapaDto(Guid Id, string TenantKey, Guid? DeviationId, Guid? SupplierId, string Title, string ProblemDescription, string RootCause, string CorrectiveAction, string PreventiveAction, string Owner, string Status, DateTimeOffset? DueAt, DateTimeOffset CreatedAt, DateTimeOffset? ClosedAt);
public sealed record UpdateCapaStatusRequest(string Status, string Actor, string? Notes = null);
public sealed record CreateSupplierEvaluationRequest(Guid SupplierId, int Score, string? QualityNotes = null, string? DeliveryNotes = null, string? Notes = null);
public sealed record SupplierEvaluationDto(Guid Id, string TenantKey, Guid SupplierId, int Score, string? QualityNotes, string? DeliveryNotes, string? Notes, string EvaluatedBy, DateTimeOffset EvaluatedAt);
public sealed record CreatePurchaseOrderRequest(string TenantKey, Guid SupplierId, string OrderNumber, string Currency, IReadOnlyList<PurchaseOrderLineRequest> Lines, string Status = "Draft", DateTimeOffset? OrderedAt = null, DateTimeOffset? ExpectedAt = null);
public sealed record UpdatePurchaseOrderRequest(Guid SupplierId, string OrderNumber, string Currency, IReadOnlyList<PurchaseOrderLineRequest> Lines, DateTimeOffset? ExpectedAt = null);
public sealed record PurchaseOrderLineRequest(string MaterialSku, decimal OrderedQuantity, string Uom, decimal UnitPrice);
public sealed record PurchaseOrderDto(Guid Id, string TenantKey, string OrderNumber, Guid SupplierId, string SupplierCode, string Status, string Currency, DateTimeOffset OrderedAt, DateTimeOffset? ExpectedAt, IReadOnlyList<PurchaseOrderLineDto> Lines, string SupplierName = "");
public sealed record PurchaseOrderLineDto(Guid Id, string MaterialSku, decimal OrderedQuantity, decimal ReceivedQuantity, string Uom, decimal UnitPrice);
public sealed record ReceiveInboundLotRequest(
    Guid PurchaseOrderId,
    Guid PurchaseOrderLineId,
    string MaterialSku,
    string ReceiptNumber,
    string SupplierLotCode,
    string FacilityId,
    decimal Quantity,
    DateOnly? ExpiryDate = null,
    DateTimeOffset? ReceivedAt = null,
    string? TraceabilityLotCode = null,
    string? OriginCountryCode = null,
    DateOnly? ManufacturedOn = null,
    string? StorageLocationCode = null,
    string? DeliveryNoteNumber = null,
    string? CarrierName = null,
    string? VehicleReference = null,
    decimal? TemperatureOnReceiptC = null,
    string? CertificateOfAnalysisReference = null,
    string? ReceivedBy = null,
    decimal? AcceptedQuantity = null,
    decimal? RejectedQuantity = null);
public sealed record ReceiveInboundBatchRequest(IReadOnlyList<ReceiveInboundLotRequest> Receipts);
public sealed record InboundReceiptDto(
    Guid Id,
    string TenantKey,
    string ReceiptNumber,
    Guid PurchaseOrderId,
    Guid PurchaseOrderLineId,
    Guid LotId,
    Guid SupplierId,
    string SupplierLotCode,
    string FacilityId,
    decimal Quantity,
    string Uom,
    DateTimeOffset ReceivedAt,
    string Disposition,
    DateOnly? ExpiryDate,
    string LotCode = "",
    string? StorageLocationCode = null,
    string? DeliveryNoteNumber = null,
    string? CarrierName = null,
    string? VehicleReference = null,
    decimal? TemperatureOnReceiptC = null,
    string? CertificateOfAnalysisReference = null,
    string? ReceivedBy = null,
    decimal AcceptedQuantity = 0,
    decimal RejectedQuantity = 0);
public sealed record UpdatePurchaseOrderStatusRequest(string Status, string? Actor = null);
public sealed record CostProjectionDto(string TenantKey, Guid RecipeId, string ProductSku, int RecipeVersion, string OutputUom, decimal PlannedOutputQuantity, decimal TargetYieldPercent, decimal ProjectedLossQuantity, decimal EstimatedMaterialCost, decimal EstimatedMaterialCostPerOutputUnit, IReadOnlyList<CostProjectionComponentDto> Components, IReadOnlyList<string> MissingPriceSkus, DateTimeOffset GeneratedAt);
public sealed record CostProjectionComponentDto(string IngredientSku, string Uom, decimal RequiredInputQuantity, decimal EstimatedUnitPrice, decimal EstimatedCost, bool HasPrice);
public sealed record CalculateBatchCostRequest(decimal LaborCost = 0, decimal OverheadCost = 0, string Currency = "VND", string? Actor = null);
public sealed record ProductionBatchCostDto(Guid Id, Guid ProductionBatchId, string TenantKey, decimal MaterialCost, decimal LaborCost, decimal OverheadCost, decimal LossCost, decimal TotalCost, decimal CostPerOutputUnit, string Currency, DateTimeOffset CalculatedAt, string? CalculatedBy);
public sealed record InventoryTransactionDto(Guid Id, string TenantKey, Guid LotId, string TransactionType, decimal Quantity, string Uom, string FacilityId, string StockStatus, Guid CorrelationId, DateTimeOffset OccurredAt);
