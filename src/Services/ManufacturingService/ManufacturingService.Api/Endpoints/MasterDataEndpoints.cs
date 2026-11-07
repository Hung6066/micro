using His.Hope.AspNetCore.Authentication;
using His.Hope.AspNetCore.Tenancy;
using His.Hope.ServiceDefaults;
using His.Hope.ManufacturingService.Application.Ports;
using His.Hope.ManufacturingService.Application;
using His.Hope.ManufacturingService.Infrastructure.Persistence;
using His.Hope.Authorization;
using His.Hope.Infrastructure.Security;
using His.Hope.Infrastructure.Caching;
using StackExchange.Redis;
using His.Hope.SharedKernel.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Builder;
using static ManufacturingEndpointHelpers;

internal static class MasterDataEndpoints
{
    public static RouteGroupBuilder MapMasterDataEndpoints(this RouteGroupBuilder api)
    {
                
                api.MapGet("/products/{sku}/fefo", (string sku, int? limit, HttpContext context, IManufacturingReservationStore store) =>
                {
                    var tenantKey = TenantClaim(context);
                    return string.IsNullOrWhiteSpace(tenantKey)
                        ? Results.Forbid()
                        : Results.Ok(store.GetFefo(tenantKey, sku, limit ?? 50));
                });

                api.MapGet("/products/{sku}/availability", (string sku, string? tenantKey, HttpContext context, IManufacturingProductionStore store) =>
                {
                    if (!TryResolveTenant(context, tenantKey, out var scopedTenant)) return Results.Forbid();
                    return Results.Ok(store.GetAvailability(scopedTenant, sku));
                });

                api.MapGet("/facilities", (string? tenantKey, bool? active, int? limit, HttpContext context, IManufacturingMasterDataStore store) =>
                {
                    if (!TryResolveTenant(context, tenantKey, out var scopedTenant)) return Results.Forbid();
                    return Results.Ok(store.GetFacilities(scopedTenant, active, limit ?? 100));
                });

                api.MapPost("/facilities", (CreateFacilityRequest request, HttpContext context, IManufacturingMasterDataStore store) =>
                {
                    if (string.IsNullOrWhiteSpace(request.TenantKey) || string.IsNullOrWhiteSpace(request.Code) || string.IsNullOrWhiteSpace(request.Name)) return ManufacturingProblem(StatusCodes.Status400BadRequest, "invalid_facility");
                    if (!TenantMatches(context, request.TenantKey)) return Results.Forbid();
                    var result = store.CreateFacility(request);
                    return result.Error is null ? Results.Created("/api/v1/manufacturing/facilities", result.Facility) : ManufacturingProblem(StatusCodes.Status409Conflict, result.Error);
                });

                api.MapPatch("/facilities/{facilityId:guid}", (Guid facilityId, UpdateFacilityRequest request, HttpContext context, IManufacturingMasterDataStore store) =>
                {
                    var tenantKey = TenantClaim(context); if (string.IsNullOrWhiteSpace(tenantKey)) return Results.Forbid(); var result = store.UpdateFacility(tenantKey, facilityId, request);
                    return result.Error switch { ManufacturingErrorCodes.FacilityNotFound => ManufacturingProblem(StatusCodes.Status404NotFound, result.Error!), "facility_code_exists" => ManufacturingProblem(StatusCodes.Status409Conflict, result.Error!), _ => Results.Ok(result.Facility) };
                });

                api.MapGet("/warehouses", (string? tenantKey, Guid? facilityId, bool? active, int? limit, HttpContext context, IManufacturingMasterDataStore store) =>
                {
                    if (!TryResolveTenant(context, tenantKey, out var scopedTenant)) return Results.Forbid();
                    return Results.Ok(store.GetWarehouses(scopedTenant, facilityId, active, limit ?? 100));
                });

                api.MapPost("/warehouses", (CreateWarehouseRequest request, HttpContext context, IManufacturingMasterDataStore store) =>
                {
                    if (string.IsNullOrWhiteSpace(request.TenantKey) || request.FacilityId == Guid.Empty || string.IsNullOrWhiteSpace(request.Code) || string.IsNullOrWhiteSpace(request.Name)) return ManufacturingProblem(StatusCodes.Status400BadRequest, ManufacturingErrorCodes.InvalidWarehouse);
                    if (!TenantMatches(context, request.TenantKey)) return Results.Forbid();
                    var result = store.CreateWarehouse(request);
                    return result.Error switch { ManufacturingErrorCodes.FacilityNotFound => ManufacturingProblem(StatusCodes.Status404NotFound, result.Error!), "warehouse_code_exists" => ManufacturingProblem(StatusCodes.Status409Conflict, result.Error!), _ => Results.Created("/api/v1/manufacturing/warehouses", result.Warehouse) };
                });

                api.MapPatch("/warehouses/{warehouseId:guid}", (Guid warehouseId, UpdateWarehouseRequest request, HttpContext context, IManufacturingMasterDataStore store) =>
                {
                    var tenantKey = TenantClaim(context); if (string.IsNullOrWhiteSpace(tenantKey)) return Results.Forbid(); var result = store.UpdateWarehouse(tenantKey, warehouseId, request);
                    return result.Error switch { ManufacturingErrorCodes.WarehouseNotFound or ManufacturingErrorCodes.FacilityNotFound => ManufacturingProblem(StatusCodes.Status404NotFound, result.Error!), "warehouse_code_exists" => ManufacturingProblem(StatusCodes.Status409Conflict, result.Error!), _ => Results.Ok(result.Warehouse) };
                });

                api.MapGet("/storage-locations", (string? tenantKey, Guid? warehouseId, bool? active, int? limit, HttpContext context, IManufacturingMasterDataStore store) =>
                {
                    if (!TryResolveTenant(context, tenantKey, out var scopedTenant)) return Results.Forbid();
                    return Results.Ok(store.GetStorageLocations(scopedTenant, warehouseId, active, limit ?? 200));
                });

                api.MapPost("/storage-locations", (CreateStorageLocationRequest request, HttpContext context, IManufacturingMasterDataStore store) =>
                {
                    if (string.IsNullOrWhiteSpace(request.TenantKey) || request.WarehouseId == Guid.Empty || string.IsNullOrWhiteSpace(request.Code) || string.IsNullOrWhiteSpace(request.Name)) return ManufacturingProblem(StatusCodes.Status400BadRequest, "invalid_storage_location");
                    if (!TenantMatches(context, request.TenantKey)) return Results.Forbid();
                    var result = store.CreateStorageLocation(request);
                    return result.Error switch { ManufacturingErrorCodes.WarehouseNotFound => ManufacturingProblem(StatusCodes.Status404NotFound, result.Error!), "location_code_exists" => ManufacturingProblem(StatusCodes.Status409Conflict, result.Error!), _ => Results.Created("/api/v1/manufacturing/storage-locations", result.Location) };
                });

                api.MapPatch("/storage-locations/{locationId:guid}", (Guid locationId, UpdateStorageLocationRequest request, HttpContext context, IManufacturingMasterDataStore store) =>
                {
                    var tenantKey = TenantClaim(context); if (string.IsNullOrWhiteSpace(tenantKey)) return Results.Forbid(); var result = store.UpdateStorageLocation(tenantKey, locationId, request);
                    return result.Error switch { "storage_location_not_found" or ManufacturingErrorCodes.WarehouseNotFound => ManufacturingProblem(StatusCodes.Status404NotFound, result.Error!), "location_code_exists" => ManufacturingProblem(StatusCodes.Status409Conflict, result.Error!), _ => Results.Ok(result.Location) };
                });

                api.MapGet("/uoms", (bool? active, IManufacturingMasterDataStore store) => Results.Ok(store.GetUoms(active, 200)));

                api.MapPost("/uoms", (CreateUomRequest request, IManufacturingMasterDataStore store) =>
                {
                    if (string.IsNullOrWhiteSpace(request.Code) || string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.Dimension)) return ManufacturingProblem(StatusCodes.Status400BadRequest, ManufacturingErrorCodes.InvalidUom);
                    var result = store.CreateUom(request);
                    return result.Error is null ? Results.Created("/api/v1/manufacturing/uoms", result.Uom) : ManufacturingProblem(StatusCodes.Status409Conflict, result.Error!);
                });

                api.MapPatch("/uoms/{uomId:guid}", (Guid uomId, UpdateUomRequest request, IManufacturingMasterDataStore store) =>
                {
                    var result = store.UpdateUom(uomId, request); return result.Error switch { "uom_not_found" => ManufacturingProblem(StatusCodes.Status404NotFound, result.Error!), "uom_code_exists" => ManufacturingProblem(StatusCodes.Status409Conflict, result.Error!), _ => Results.Ok(result.Uom) };
                });

                api.MapGet("/uom-conversions", (bool? active, IManufacturingMasterDataStore store) => Results.Ok(store.GetUomConversions(active, 500)));

                api.MapPost("/uom-conversions", (CreateUomConversionRequest request, IManufacturingMasterDataStore store) =>
                {
                    var result = store.CreateUomConversion(request);
                    return result.Error switch { "invalid_uom_conversion" => ManufacturingProblem(StatusCodes.Status400BadRequest, result.Error!), "uom_not_found" => ManufacturingProblem(StatusCodes.Status404NotFound, result.Error!), "uom_conversion_exists" => ManufacturingProblem(StatusCodes.Status409Conflict, result.Error!), _ => Results.Created("/api/v1/manufacturing/uom-conversions", result.Conversion) };
                });

                api.MapPatch("/uom-conversions/{conversionId:guid}", (Guid conversionId, UpdateUomConversionRequest request, IManufacturingMasterDataStore store) =>
                {
                    var result = store.UpdateUomConversion(conversionId, request); return result.Error switch { "uom_conversion_not_found" => ManufacturingProblem(StatusCodes.Status404NotFound, result.Error!), "uom_conversion_exists" => ManufacturingProblem(StatusCodes.Status409Conflict, result.Error!), _ => Results.Ok(result.Conversion) };
                });

                api.MapGet("/materials", (string? tenantKey, string? materialType, bool? active, int? limit, HttpContext context, IManufacturingMasterDataStore store) =>
                {
                    if (!TryResolveTenant(context, tenantKey, out _)) return Results.Forbid(); return Results.Ok(store.GetMaterials(materialType, active, limit ?? 500));
                });

                api.MapPost("/materials", (CreateMaterialRequest request, HttpContext context, IManufacturingMasterDataStore store) =>
                {
                    if (string.IsNullOrWhiteSpace(request.TenantKey) || string.IsNullOrWhiteSpace(request.Sku) || string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.BaseUomCode)) return ManufacturingProblem(StatusCodes.Status400BadRequest, "invalid_material"); if (!TenantMatches(context, request.TenantKey)) return Results.Forbid(); var result = store.CreateMaterial(request); return result.Error switch { "uom_not_found" => ManufacturingProblem(StatusCodes.Status404NotFound, result.Error!), "material_sku_exists" => ManufacturingProblem(StatusCodes.Status409Conflict, result.Error!), _ => Results.Created("/api/v1/manufacturing/materials", result.Material) };
                });

                api.MapPatch("/materials/{materialId:guid}", (Guid materialId, UpdateMaterialRequest request, HttpContext context, IManufacturingMasterDataStore store) =>
                {
                    var tenantKey = TenantClaim(context); if (string.IsNullOrWhiteSpace(tenantKey)) return Results.Forbid(); var result = store.UpdateMaterial(tenantKey, materialId, request);
                    return result.Error switch { ManufacturingErrorCodes.MaterialNotFound or "uom_not_found" => ManufacturingProblem(StatusCodes.Status404NotFound, result.Error!), _ => Results.Ok(result.Material) };
                });

                api.MapGet("/products", (string? tenantKey, bool? active, int? limit, HttpContext context, IManufacturingMasterDataStore store) =>
                {
                    if (!TryResolveTenant(context, tenantKey, out var scopedTenant)) return Results.Forbid(); return Results.Ok(store.GetProducts(scopedTenant, active, limit ?? 500));
                });

                api.MapPost("/products", (CreateProductRequest request, HttpContext context, IManufacturingMasterDataStore store) =>
                {
                    if (string.IsNullOrWhiteSpace(request.TenantKey) || string.IsNullOrWhiteSpace(request.Sku) || string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.BaseUomCode)) return ManufacturingProblem(StatusCodes.Status400BadRequest, "invalid_product"); if (!TenantMatches(context, request.TenantKey)) return Results.Forbid(); var result = store.CreateProduct(request); return result.Error switch { "uom_not_found" => ManufacturingProblem(StatusCodes.Status404NotFound, result.Error!), "product_sku_exists" => ManufacturingProblem(StatusCodes.Status409Conflict, result.Error!), _ => Results.Created("/api/v1/manufacturing/products", result.Product) };
                });

                api.MapPatch("/products/{productId:guid}", (Guid productId, UpdateProductRequest request, HttpContext context, IManufacturingMasterDataStore store) =>
                {
                    var tenantKey = TenantClaim(context); if (string.IsNullOrWhiteSpace(tenantKey)) return Results.Forbid(); var result = store.UpdateProduct(tenantKey, productId, request);
                    return result.Error switch { ManufacturingErrorCodes.ProductNotFound or "uom_not_found" => ManufacturingProblem(StatusCodes.Status404NotFound, result.Error!), _ => Results.Ok(result.Product) };
                });

        return api;
    }
}




