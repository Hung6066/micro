using His.Hope.Contracts.Manufacturing;

namespace His.Hope.ManufacturingService.Application.Ports;

public interface IManufacturingMasterDataStore
{
    IReadOnlyList<UomDto> GetUoms(bool? active, int limit);
    (UomDto? Uom, string? Error) CreateUom(CreateUomRequest request);
    (UomDto? Uom, string? Error) UpdateUom(Guid uomId, UpdateUomRequest request);
    IReadOnlyList<UomConversionDto> GetUomConversions(bool? active, int limit);
    (UomConversionDto? Conversion, string? Error) CreateUomConversion(CreateUomConversionRequest request);
    (UomConversionDto? Conversion, string? Error) UpdateUomConversion(Guid conversionId, UpdateUomConversionRequest request);
    IReadOnlyList<MaterialDto> GetMaterials(string? materialType, bool? active, int limit);
    IReadOnlyList<MaterialDto> GetMaterials(string tenantKey, string? materialType, bool? active, int limit);
    (MaterialDto? Material, string? Error) CreateMaterial(CreateMaterialRequest request);
    (MaterialDto? Material, string? Error) UpdateMaterial(string tenantKey, Guid materialId, UpdateMaterialRequest request);
    IReadOnlyList<ProductDto> GetProducts(string tenantKey, bool? active, int limit);
    (ProductDto? Product, string? Error) CreateProduct(CreateProductRequest request);
    (ProductDto? Product, string? Error) UpdateProduct(string tenantKey, Guid productId, UpdateProductRequest request);
    IReadOnlyList<SupplierRfqDto> GetSupplierRfqs(string tenantKey, string? status, int limit);
    IReadOnlyList<SupplierQuotationDto> GetSupplierQuotations(string tenantKey, Guid rfqId, int limit);
    (SupplierRfqDto? Rfq, string? Error) CreateSupplierRfq(CreateSupplierRfqRequest request);
    (SupplierQuotationDto? Quotation, string? Error) CreateSupplierQuotation(string tenantKey, CreateSupplierQuotationRequest request);
    (SupplierQuotationDto? Quotation, string? Error) UpdateSupplierQuotationStatus(string tenantKey, Guid quotationId, string status);
    IReadOnlyList<FacilityDto> GetFacilities(string tenantKey, bool? active, int limit);
    (FacilityDto? Facility, string? Error) CreateFacility(CreateFacilityRequest request);
    (FacilityDto? Facility, string? Error) UpdateFacility(string tenantKey, Guid facilityId, UpdateFacilityRequest request);
    IReadOnlyList<WarehouseDto> GetWarehouses(string tenantKey, Guid? facilityId, bool? active, int limit);
    (WarehouseDto? Warehouse, string? Error) CreateWarehouse(CreateWarehouseRequest request);
    (WarehouseDto? Warehouse, string? Error) UpdateWarehouse(string tenantKey, Guid warehouseId, UpdateWarehouseRequest request);
    IReadOnlyList<StorageLocationDto> GetStorageLocations(string tenantKey, Guid? warehouseId, bool? active, int limit);
    (StorageLocationDto? Location, string? Error) CreateStorageLocation(CreateStorageLocationRequest request);
    (StorageLocationDto? Location, string? Error) UpdateStorageLocation(string tenantKey, Guid locationId, UpdateStorageLocationRequest request);
}
