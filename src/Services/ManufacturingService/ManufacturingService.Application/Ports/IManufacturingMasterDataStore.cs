using His.Hope.Contracts.Manufacturing;

namespace His.Hope.ManufacturingService.Application.Ports;

public interface IManufacturingMasterDataStore
{
    IReadOnlyList<UomDto> GetUoms(bool? active, int limit);
    (UomDto? Uom, string? Error) CreateUom(CreateUomRequest request);
    IReadOnlyList<UomConversionDto> GetUomConversions(bool? active, int limit);
    (UomConversionDto? Conversion, string? Error) CreateUomConversion(CreateUomConversionRequest request);
    IReadOnlyList<MaterialDto> GetMaterials(string tenantKey, string? materialType, bool? active, int limit);
    (MaterialDto? Material, string? Error) CreateMaterial(CreateMaterialRequest request);
    IReadOnlyList<ProductDto> GetProducts(string tenantKey, bool? active, int limit);
    (ProductDto? Product, string? Error) CreateProduct(CreateProductRequest request);
    IReadOnlyList<SupplierRfqDto> GetSupplierRfqs(string tenantKey, string? status, int limit);
    IReadOnlyList<SupplierQuotationDto> GetSupplierQuotations(string tenantKey, Guid rfqId, int limit);
    (SupplierRfqDto? Rfq, string? Error) CreateSupplierRfq(CreateSupplierRfqRequest request);
    (SupplierQuotationDto? Quotation, string? Error) CreateSupplierQuotation(string tenantKey, CreateSupplierQuotationRequest request);
    IReadOnlyList<FacilityDto> GetFacilities(string tenantKey, bool? active, int limit);
    (FacilityDto? Facility, string? Error) CreateFacility(CreateFacilityRequest request);
    IReadOnlyList<WarehouseDto> GetWarehouses(string tenantKey, Guid? facilityId, bool? active, int limit);
    (WarehouseDto? Warehouse, string? Error) CreateWarehouse(CreateWarehouseRequest request);
    IReadOnlyList<StorageLocationDto> GetStorageLocations(string tenantKey, Guid? warehouseId, bool? active, int limit);
    (StorageLocationDto? Location, string? Error) CreateStorageLocation(CreateStorageLocationRequest request);
}
