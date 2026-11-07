namespace His.Hope.ManufacturingService.Domain;

/// <summary>
/// Stable machine-readable error codes owned by the manufacturing bounded
/// context. These values are not shared protocol constants and must not leak
/// into unrelated services.
/// </summary>
public static class ManufacturingErrorCodes
{
    public const string ActiveProductSpecificationExists = "active_product_specification_exists";
    public const string ApprovedRecipeNotFound = "approved_recipe_not_found";
    public const string BatchNotStarted = "batch_not_started";
    public const string BusinessSignatureAlreadyExists = "business_signature_already_exists";
    public const string ConcurrencyConflict = "concurrency_conflict";
    public const string DatasetKeyMismatch = "dataset_key_mismatch";
    public const string DeviationNotFound = "deviation_not_found";
    public const string DowntimeNotFound = "downtime_not_found";
    public const string DowntimeNotOpen = "downtime_not_open";
    public const string DuplicateInputLot = "duplicate_input_lot";
    public const string FacilityNotFound = "facility_not_found";
    public const string ForecastNotFound = "forecast_not_found";
    public const string InputLotNotReleased = "input_lot_not_released";
    public const string InputQuantityInsufficient = "input_quantity_insufficient";
    public const string InputReservationMismatch = "input_reservation_mismatch";
    public const string InputReservationUnavailable = "input_reservation_unavailable";
    public const string InspectionPlanMismatch = "inspection_plan_mismatch";
    public const string InvalidBatchTransition = "invalid_batch_transition";
    public const string InvalidBusinessSignature = "invalid_business_signature";
    public const string InvalidDisposition = "invalid_disposition";
    public const string InvalidDowntime = "invalid_downtime";
    public const string InvalidInboundBatch = "invalid_inbound_batch";
    public const string InvalidLot = "invalid_lot";
    public const string InvalidMachine = "invalid_machine";
    public const string InvalidMaintenancePlan = "invalid_maintenance_plan";
    public const string InvalidProductSpecification = "invalid_product_specification";
    public const string InvalidProductionBatch = "invalid_production_batch";
    public const string InvalidProductionOrder = "invalid_production_order";
    public const string InvalidRecipe = "invalid_recipe";
    public const string InvalidRecipeStatus = "invalid_recipe_status";
    public const string InvalidSalesAllocation = "invalid_sales_allocation";
    public const string InvalidSupplier = "invalid_supplier";
    public const string InvalidSupplierCertificate = "invalid_supplier_certificate";
    public const string InvalidSupplierMaterialApproval = "invalid_supplier_material_approval";
    public const string InvalidTransformation = "invalid_transformation";
    public const string InvalidUom = "invalid_uom";
    public const string InvalidWarehouse = "invalid_warehouse";
    public const string InsufficientAtp = "insufficient_atp";
    public const string LotExpired = "lot_expired";
    public const string LotNotFound = "lot_not_found";
    public const string LotNotReleased = "lot_not_released";
    public const string MachineCalibrationExists = "machine_calibration_exists";
    public const string MachineUnavailable = "machine_unavailable";
    public const string MaterialMismatch = "material_mismatch";
    public const string MaterialNotFound = "material_not_found";
    public const string OperationNotFound = "operation_not_found";
    public const string OperationSequenceExists = "operation_sequence_exists";
    public const string OutputQuantityExceedsInput = "output_quantity_exceeds_input";
    public const string PendingQuality = "pending_quality";
    public const string ProductNotFound = "product_not_found";
    public const string ProductSpecificationNotFound = "product_specification_not_found";
    public const string ProductionBatchExists = "production_batch_exists";
    public const string ProductionBatchNotCancellable = "production_batch_not_cancellable";
    public const string ProductionOrderExists = "production_order_exists";
    public const string ProductionOrderNotFound = "production_order_not_found";
    public const string QualityGateIncomplete = "quality_gate_incomplete";
    public const string QualityInspectionNotFound = "quality_inspection_not_found";
    public const string QualitySampleExists = "quality_sample_exists";
    public const string QualitySampleNotFound = "quality_sample_not_found";
    public const string RecipeNotFound = "recipe_not_found";
    public const string RecipeProductMismatch = "recipe_product_mismatch";
    public const string RecipeUnavailable = "recipe_unavailable";
    public const string ReservationExceedsAvailable = "reservation_exceeds_available";
    public const string ReservationExpired = "reservation_expired";
    public const string ReservationMismatch = "reservation_mismatch";
    public const string ReservationNotFound = "reservation_not_found";
    public const string ReservationUnavailable = "reservation_unavailable";
    public const string RequiredOperationIncomplete = "required_operation_incomplete";
    public const string SupplierInactive = "supplier_inactive";
    public const string SupplierMaterialNotApproved = "supplier_material_not_approved";
    public const string SupplierNotApproved = "supplier_not_approved";
    public const string SupplierCodeExists = "supplier_code_exists";
    public const string TenantMismatch = "tenant_mismatch";
    public const string TenantScopeDenied = "tenant_scope_denied";
    public const string WarehouseNotFound = "warehouse_not_found";
    public const string MachineNotFound = "machine_not_found";
    public const string ProductionBatchNotFound = "production_batch_not_found";
    public const string SupplierNotFound = "supplier_not_found";
}
