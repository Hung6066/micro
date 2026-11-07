using Microsoft.EntityFrameworkCore;
using His.Hope.ManufacturingService.Application.Ports;
using His.Hope.AspNetCore.Tenancy;

public sealed class ManufacturingMasterDataStore(IDbContextFactory<ManufacturingDbContext> dbFactory) : IManufacturingMasterDataStore
{
    public IReadOnlyList<MaterialDto> GetMaterials(string? materialType, bool? active, int limit) =>
        GetMaterials(HisHopeTenantScope.Current ?? throw new InvalidOperationException("Tenant context is required."), materialType, active, limit);

    public IReadOnlyList<UomDto> GetUoms(bool? active, int limit)
    {
        using var db = dbFactory.CreateDbContext(); var query = db.Uoms.AsNoTracking(); if (active.HasValue) query = query.Where(x => x.Active == active.Value);
        return query.OrderBy(x => x.Code).Take(Math.Clamp(limit, 1, 200)).AsEnumerable().Select(x => new UomDto(x.Id, x.Code, x.Name, x.Dimension, x.Active, x.CreatedAt)).ToList();
    }

    public (UomDto? Uom, string? Error) CreateUom(CreateUomRequest request)
    {
        using var db = dbFactory.CreateDbContext(); var code = request.Code.Trim().ToLowerInvariant();
        if (db.Uoms.Any(x => x.Code == code)) return (null, "uom_code_exists");
        var entity = new ManufacturingUomEntity { Id = Guid.NewGuid(), Code = code, Name = request.Name.Trim(), Dimension = request.Dimension.Trim(), Active = request.Active, CreatedAt = DateTimeOffset.UtcNow };
        db.Uoms.Add(entity); db.SaveChanges(); return (new UomDto(entity.Id, entity.Code, entity.Name, entity.Dimension, entity.Active, entity.CreatedAt), null);
    }

    public (UomDto? Uom, string? Error) UpdateUom(Guid uomId, UpdateUomRequest request)
    {
        using var db = dbFactory.CreateDbContext(); var entity = db.Uoms.SingleOrDefault(x => x.Id == uomId);
        if (entity is null) return (null, "uom_not_found");
        var code = request.Code.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.Dimension)) return (null, ManufacturingErrorCodes.InvalidUom);
        if (db.Uoms.Any(x => x.Id != uomId && x.Code == code)) return (null, "uom_code_exists");
        entity.Code = code; entity.Name = request.Name.Trim(); entity.Dimension = request.Dimension.Trim(); entity.Active = request.Active; db.SaveChanges();
        return (new UomDto(entity.Id, entity.Code, entity.Name, entity.Dimension, entity.Active, entity.CreatedAt), null);
    }

    public IReadOnlyList<UomConversionDto> GetUomConversions(bool? active, int limit)
    {
        using var db = dbFactory.CreateDbContext(); var query = db.UomConversions.AsNoTracking(); if (active.HasValue) query = query.Where(x => x.Active == active.Value);
        return query.OrderBy(x => x.FromCode).ThenBy(x => x.ToCode).Take(Math.Clamp(limit, 1, 500)).AsEnumerable().Select(x => new UomConversionDto(x.Id, x.FromCode, x.ToCode, x.Factor, x.Active, x.CreatedAt)).ToList();
    }

    public (UomConversionDto? Conversion, string? Error) CreateUomConversion(CreateUomConversionRequest request)
    {
        using var db = dbFactory.CreateDbContext(); var from = request.FromCode.Trim().ToLowerInvariant(); var to = request.ToCode.Trim().ToLowerInvariant();
        if (request.Factor <= 0 || string.IsNullOrWhiteSpace(from) || string.IsNullOrWhiteSpace(to) || from == to) return (null, "invalid_uom_conversion");
        if (!db.Uoms.Any(x => x.Code == from && x.Active) || !db.Uoms.Any(x => x.Code == to && x.Active)) return (null, "uom_not_found");
        if (db.UomConversions.Any(x => x.FromCode == from && x.ToCode == to)) return (null, "uom_conversion_exists");
        var entity = new ManufacturingUomConversionEntity { Id = Guid.NewGuid(), FromCode = from, ToCode = to, Factor = request.Factor, Active = request.Active, CreatedAt = DateTimeOffset.UtcNow };
        db.UomConversions.Add(entity); db.SaveChanges(); return (new UomConversionDto(entity.Id, entity.FromCode, entity.ToCode, entity.Factor, entity.Active, entity.CreatedAt), null);
    }

    public (UomConversionDto? Conversion, string? Error) UpdateUomConversion(Guid conversionId, UpdateUomConversionRequest request)
    {
        using var db = dbFactory.CreateDbContext(); var entity = db.UomConversions.SingleOrDefault(x => x.Id == conversionId);
        if (entity is null) return (null, "uom_conversion_not_found");
        var from = request.FromCode.Trim().ToLowerInvariant(); var to = request.ToCode.Trim().ToLowerInvariant();
        if (request.Factor <= 0 || from == to || !db.Uoms.Any(x => x.Code == from && x.Active) || !db.Uoms.Any(x => x.Code == to && x.Active)) return (null, "invalid_uom_conversion");
        if (db.UomConversions.Any(x => x.Id != conversionId && x.FromCode == from && x.ToCode == to)) return (null, "uom_conversion_exists");
        entity.FromCode = from; entity.ToCode = to; entity.Factor = request.Factor; entity.Active = request.Active; db.SaveChanges();
        return (new UomConversionDto(entity.Id, entity.FromCode, entity.ToCode, entity.Factor, entity.Active, entity.CreatedAt), null);
    }

    public IReadOnlyList<MaterialDto> GetMaterials(string tenantKey, string? materialType, bool? active, int limit)
    {
        using var db = dbFactory.CreateDbContext(); var query = db.Materials.AsNoTracking().Where(x => x.TenantKey == tenantKey); if (!string.IsNullOrWhiteSpace(materialType)) query = query.Where(x => x.MaterialType == materialType); if (active.HasValue) query = query.Where(x => x.Active == active.Value);
        return query.OrderBy(x => x.Sku).Take(Math.Clamp(limit, 1, 500)).AsEnumerable().Select(x => new MaterialDto(x.Id, x.TenantKey, x.Sku, x.Name, x.BaseUomCode, x.MaterialType, x.Active, x.CreatedAt)).ToList();
    }

    public (MaterialDto? Material, string? Error) CreateMaterial(CreateMaterialRequest request)
    {
        using var db = dbFactory.CreateDbContext(); var sku = request.Sku.Trim().ToUpperInvariant(); var uom = request.BaseUomCode.Trim().ToLowerInvariant();
        if (!db.Uoms.Any(x => x.Code == uom && x.Active)) return (null, "uom_not_found");
        if (db.Materials.Any(x => x.TenantKey == request.TenantKey && x.Sku == sku)) return (null, "material_sku_exists");
        var entity = new ManufacturingMaterialEntity { Id = Guid.NewGuid(), TenantKey = request.TenantKey.Trim(), Sku = sku, Name = request.Name.Trim(), BaseUomCode = uom, MaterialType = request.MaterialType.Trim(), Active = request.Active, CreatedAt = DateTimeOffset.UtcNow };
        db.Materials.Add(entity); db.SaveChanges(); return (new MaterialDto(entity.Id, entity.TenantKey, entity.Sku, entity.Name, entity.BaseUomCode, entity.MaterialType, entity.Active, entity.CreatedAt), null);
    }

    public (MaterialDto? Material, string? Error) UpdateMaterial(string tenantKey, Guid materialId, UpdateMaterialRequest request)
    {
        using var db = dbFactory.CreateDbContext(); var entity = db.Materials.SingleOrDefault(x => x.Id == materialId && x.TenantKey == tenantKey);
        if (entity is null) return (null, ManufacturingErrorCodes.MaterialNotFound);
        var uom = request.BaseUomCode.Trim().ToLowerInvariant(); if (!db.Uoms.Any(x => x.Code == uom && x.Active)) return (null, "uom_not_found");
        if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.MaterialType)) return (null, "invalid_material");
        entity.Name = request.Name.Trim(); entity.BaseUomCode = uom; entity.MaterialType = request.MaterialType.Trim(); entity.Active = request.Active; db.SaveChanges();
        return (new MaterialDto(entity.Id, entity.TenantKey, entity.Sku, entity.Name, entity.BaseUomCode, entity.MaterialType, entity.Active, entity.CreatedAt), null);
    }

    public IReadOnlyList<ProductDto> GetProducts(string tenantKey, bool? active, int limit)
    {
        using var db = dbFactory.CreateDbContext(); var query = db.Products.AsNoTracking().Where(x => x.TenantKey == tenantKey); if (active.HasValue) query = query.Where(x => x.Active == active.Value);
        return query.OrderBy(x => x.Sku).Take(Math.Clamp(limit, 1, 500)).AsEnumerable().Select(x => new ProductDto(x.Id, x.TenantKey, x.Sku, x.Name, x.BaseUomCode, x.Active, x.CreatedAt)).ToList();
    }

    public (ProductDto? Product, string? Error) CreateProduct(CreateProductRequest request)
    {
        using var db = dbFactory.CreateDbContext(); var sku = request.Sku.Trim().ToUpperInvariant(); var uom = request.BaseUomCode.Trim().ToLowerInvariant();
        if (!db.Uoms.Any(x => x.Code == uom && x.Active)) return (null, "uom_not_found");
        if (db.Products.Any(x => x.TenantKey == request.TenantKey && x.Sku == sku)) return (null, "product_sku_exists");
        var entity = new ManufacturingProductEntity { Id = Guid.NewGuid(), TenantKey = request.TenantKey.Trim(), Sku = sku, Name = request.Name.Trim(), BaseUomCode = uom, Active = request.Active, CreatedAt = DateTimeOffset.UtcNow };
        db.Products.Add(entity); db.SaveChanges(); return (new ProductDto(entity.Id, entity.TenantKey, entity.Sku, entity.Name, entity.BaseUomCode, entity.Active, entity.CreatedAt), null);
    }

    public (ProductDto? Product, string? Error) UpdateProduct(string tenantKey, Guid productId, UpdateProductRequest request)
    {
        using var db = dbFactory.CreateDbContext(); var entity = db.Products.SingleOrDefault(x => x.Id == productId && x.TenantKey == tenantKey);
        if (entity is null) return (null, ManufacturingErrorCodes.ProductNotFound);
        var uom = request.BaseUomCode.Trim().ToLowerInvariant(); if (!db.Uoms.Any(x => x.Code == uom && x.Active)) return (null, "uom_not_found");
        if (string.IsNullOrWhiteSpace(request.Name)) return (null, "invalid_product");
        entity.Name = request.Name.Trim(); entity.BaseUomCode = uom; entity.Active = request.Active; db.SaveChanges();
        return (new ProductDto(entity.Id, entity.TenantKey, entity.Sku, entity.Name, entity.BaseUomCode, entity.Active, entity.CreatedAt), null);
    }

    public IReadOnlyList<SupplierRfqDto> GetSupplierRfqs(string tenantKey, string? status, int limit)
    {
        using var db = dbFactory.CreateDbContext(); var query = db.SupplierRfqs.AsNoTracking().Where(x => x.TenantKey == tenantKey); if (!string.IsNullOrWhiteSpace(status)) query = query.Where(x => x.Status == status);
        return query.OrderByDescending(x => x.CreatedAt).Take(Math.Clamp(limit, 1, 200)).AsEnumerable().Select(x => new SupplierRfqDto(x.Id, x.TenantKey, x.RfqNumber, x.MaterialSku, x.Quantity, x.Uom, x.Status, x.NeededBy, x.CreatedAt)).ToList();
    }

    public IReadOnlyList<SupplierQuotationDto> GetSupplierQuotations(string tenantKey, Guid rfqId, int limit)
    {
        using var db = dbFactory.CreateDbContext();
        return db.SupplierQuotations.AsNoTracking().Where(x => x.TenantKey == tenantKey && x.SupplierRfqId == rfqId).OrderBy(x => x.UnitPrice).Take(Math.Clamp(limit, 1, 200)).AsEnumerable().Select(x => new SupplierQuotationDto(x.Id, x.TenantKey, x.SupplierRfqId, x.SupplierId, x.UnitPrice, x.Currency, x.LeadTimeDays, x.Status, x.Notes, x.CreatedAt)).ToList();
    }

    public (SupplierRfqDto? Rfq, string? Error) CreateSupplierRfq(CreateSupplierRfqRequest request)
    {
        using var db = dbFactory.CreateDbContext(); var sku = request.MaterialSku.Trim().ToUpperInvariant();
        if (request.Quantity <= 0 || string.IsNullOrWhiteSpace(request.RfqNumber) || string.IsNullOrWhiteSpace(sku) || string.IsNullOrWhiteSpace(request.Uom)) return (null, "invalid_supplier_rfq");
        if (db.SupplierRfqs.Any(x => x.TenantKey == request.TenantKey && x.RfqNumber == request.RfqNumber.Trim())) return (null, "supplier_rfq_exists");
        var entity = new ManufacturingSupplierRfqEntity { Id = Guid.NewGuid(), TenantKey = request.TenantKey.Trim(), RfqNumber = request.RfqNumber.Trim(), MaterialSku = sku, Quantity = request.Quantity, Uom = request.Uom.Trim().ToLowerInvariant(), NeededBy = request.NeededBy, CreatedAt = DateTimeOffset.UtcNow };
        db.SupplierRfqs.Add(entity); db.SaveChanges(); return (new SupplierRfqDto(entity.Id, entity.TenantKey, entity.RfqNumber, entity.MaterialSku, entity.Quantity, entity.Uom, entity.Status, entity.NeededBy, entity.CreatedAt), null);
    }

    public (SupplierQuotationDto? Quotation, string? Error) CreateSupplierQuotation(string tenantKey, CreateSupplierQuotationRequest request)
    {
        using var db = dbFactory.CreateDbContext(); var rfq = db.SupplierRfqs.SingleOrDefault(x => x.Id == request.SupplierRfqId && x.TenantKey == tenantKey); if (rfq is null) return (null, "supplier_rfq_not_found");
        var supplier = db.Suppliers.SingleOrDefault(x => x.Id == request.SupplierId && x.TenantKey == tenantKey && x.Active); if (supplier is null) return (null, ManufacturingErrorCodes.SupplierNotFound);
        if (request.UnitPrice < 0 || request.LeadTimeDays < 0) return (null, "invalid_supplier_quotation");
        if (db.SupplierQuotations.Any(x => x.TenantKey == tenantKey && x.SupplierRfqId == request.SupplierRfqId && x.SupplierId == request.SupplierId)) return (null, "supplier_quotation_exists");
        var entity = new ManufacturingSupplierQuotationEntity { Id = Guid.NewGuid(), TenantKey = tenantKey, SupplierRfqId = rfq.Id, SupplierId = supplier.Id, UnitPrice = request.UnitPrice, Currency = request.Currency.Trim().ToUpperInvariant(), LeadTimeDays = request.LeadTimeDays, Notes = request.Notes?.Trim(), CreatedAt = DateTimeOffset.UtcNow };
        db.SupplierQuotations.Add(entity); db.SaveChanges(); return (new SupplierQuotationDto(entity.Id, entity.TenantKey, entity.SupplierRfqId, entity.SupplierId, entity.UnitPrice, entity.Currency, entity.LeadTimeDays, entity.Status, entity.Notes, entity.CreatedAt), null);
    }
    public IReadOnlyList<FacilityDto> GetFacilities(string tenantKey, bool? active, int limit)
    {
        using var db = dbFactory.CreateDbContext();
        var query = db.Facilities.AsNoTracking().Where(x => x.TenantKey == tenantKey);
        if (active.HasValue) query = query.Where(x => x.Active == active.Value);
        return query.OrderBy(x => x.Code).Take(Math.Clamp(limit, 1, 200)).AsEnumerable().Select(ToDto).ToList();
    }

    public (FacilityDto? Facility, string? Error) CreateFacility(CreateFacilityRequest request)
    {
        using var db = dbFactory.CreateDbContext();
        if (db.Facilities.Any(x => x.TenantKey == request.TenantKey && x.Code == request.Code.Trim())) return (null, "facility_code_exists");
        var entity = new ManufacturingFacilityEntity { Id = Guid.NewGuid(), TenantKey = request.TenantKey.Trim(), Code = request.Code.Trim(), Name = request.Name.Trim(), Active = request.Active, CreatedAt = DateTimeOffset.UtcNow };
        db.Facilities.Add(entity);
        db.SaveChanges();
        return (ToDto(entity), null);
    }
    public (SupplierQuotationDto? Quotation, string? Error) UpdateSupplierQuotationStatus(string tenantKey, Guid quotationId, string status)
    {
        using var db = dbFactory.CreateDbContext(); var entity = db.SupplierQuotations.SingleOrDefault(x => x.Id == quotationId && x.TenantKey == tenantKey);
        if (entity is null) return (null, "supplier_quotation_not_found");
        var normalized = status.Trim(); if (normalized is not (ManufacturingStatusCodes.Submitted or "Selected" or ManufacturingStatusCodes.Rejected or ManufacturingStatusCodes.Closed)) return (null, "invalid_supplier_quotation_status");
        if (normalized == "Selected") db.SupplierQuotations.Where(x => x.SupplierRfqId == entity.SupplierRfqId && x.TenantKey == tenantKey).ToList().ForEach(x => x.Status = x.Id == quotationId ? "Selected" : ManufacturingStatusCodes.Rejected); else entity.Status = normalized;
        db.SaveChanges(); return (new SupplierQuotationDto(entity.Id, entity.TenantKey, entity.SupplierRfqId, entity.SupplierId, entity.UnitPrice, entity.Currency, entity.LeadTimeDays, entity.Status, entity.Notes, entity.CreatedAt), null);
    }

    public (FacilityDto? Facility, string? Error) UpdateFacility(string tenantKey, Guid facilityId, UpdateFacilityRequest request)
    {
        using var db = dbFactory.CreateDbContext(); var entity = db.Facilities.SingleOrDefault(x => x.Id == facilityId && x.TenantKey == tenantKey);
        if (entity is null) return (null, ManufacturingErrorCodes.FacilityNotFound);
        var code = request.Code.Trim(); if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(request.Name)) return (null, "invalid_facility");
        if (db.Facilities.Any(x => x.Id != facilityId && x.TenantKey == tenantKey && x.Code == code)) return (null, "facility_code_exists");
        entity.Code = code; entity.Name = request.Name.Trim(); entity.Active = request.Active; db.SaveChanges(); return (ToDto(entity), null);
    }

    public IReadOnlyList<WarehouseDto> GetWarehouses(string tenantKey, Guid? facilityId, bool? active, int limit)
    {
        using var db = dbFactory.CreateDbContext();
        var query = db.Warehouses.AsNoTracking().Where(x => x.TenantKey == tenantKey);
        if (facilityId.HasValue) query = query.Where(x => x.FacilityId == facilityId.Value);
        if (active.HasValue) query = query.Where(x => x.Active == active.Value);
        return query.OrderBy(x => x.Code).Take(Math.Clamp(limit, 1, 200)).AsEnumerable().Select(x => new WarehouseDto(x.Id, x.TenantKey, x.FacilityId, x.Code, x.Name, x.Active, x.CreatedAt)).ToList();
    }

    public (WarehouseDto? Warehouse, string? Error) CreateWarehouse(CreateWarehouseRequest request)
    {
        using var db = dbFactory.CreateDbContext();
        var facility = db.Facilities.SingleOrDefault(x => x.Id == request.FacilityId && x.TenantKey == request.TenantKey && x.Active);
        if (facility is null) return (null, ManufacturingErrorCodes.FacilityNotFound);
        if (db.Warehouses.Any(x => x.TenantKey == request.TenantKey && x.Code == request.Code.Trim())) return (null, "warehouse_code_exists");
        var entity = new ManufacturingWarehouseEntity { Id = Guid.NewGuid(), TenantKey = request.TenantKey.Trim(), FacilityId = facility.Id, Code = request.Code.Trim(), Name = request.Name.Trim(), Active = request.Active, CreatedAt = DateTimeOffset.UtcNow };
        db.Warehouses.Add(entity); db.SaveChanges();
        return (new WarehouseDto(entity.Id, entity.TenantKey, entity.FacilityId, entity.Code, entity.Name, entity.Active, entity.CreatedAt), null);
    }

    public (WarehouseDto? Warehouse, string? Error) UpdateWarehouse(string tenantKey, Guid warehouseId, UpdateWarehouseRequest request)
    {
        using var db = dbFactory.CreateDbContext(); var entity = db.Warehouses.SingleOrDefault(x => x.Id == warehouseId && x.TenantKey == tenantKey);
        if (entity is null) return (null, ManufacturingErrorCodes.WarehouseNotFound);
        if (!db.Facilities.Any(x => x.Id == request.FacilityId && x.TenantKey == tenantKey && x.Active)) return (null, ManufacturingErrorCodes.FacilityNotFound);
        var code = request.Code.Trim(); if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(request.Name)) return (null, ManufacturingErrorCodes.InvalidWarehouse);
        if (db.Warehouses.Any(x => x.Id != warehouseId && x.TenantKey == tenantKey && x.Code == code)) return (null, "warehouse_code_exists");
        entity.FacilityId = request.FacilityId; entity.Code = code; entity.Name = request.Name.Trim(); entity.Active = request.Active; db.SaveChanges(); return (new WarehouseDto(entity.Id, entity.TenantKey, entity.FacilityId, entity.Code, entity.Name, entity.Active, entity.CreatedAt), null);
    }

    public IReadOnlyList<StorageLocationDto> GetStorageLocations(string tenantKey, Guid? warehouseId, bool? active, int limit)
    {
        using var db = dbFactory.CreateDbContext();
        var query = db.StorageLocations.AsNoTracking().Where(x => x.TenantKey == tenantKey);
        if (warehouseId.HasValue) query = query.Where(x => x.WarehouseId == warehouseId.Value);
        if (active.HasValue) query = query.Where(x => x.Active == active.Value);
        return query.OrderBy(x => x.Code).Take(Math.Clamp(limit, 1, 500)).AsEnumerable().Select(x => new StorageLocationDto(x.Id, x.TenantKey, x.WarehouseId, x.Code, x.Name, x.Active, x.CreatedAt)).ToList();
    }

    public (StorageLocationDto? Location, string? Error) CreateStorageLocation(CreateStorageLocationRequest request)
    {
        using var db = dbFactory.CreateDbContext();
        var warehouse = db.Warehouses.SingleOrDefault(x => x.Id == request.WarehouseId && x.TenantKey == request.TenantKey && x.Active);
        if (warehouse is null) return (null, ManufacturingErrorCodes.WarehouseNotFound);
        if (db.StorageLocations.Any(x => x.TenantKey == request.TenantKey && x.WarehouseId == request.WarehouseId && x.Code == request.Code.Trim())) return (null, "location_code_exists");
        var entity = new ManufacturingStorageLocationEntity { Id = Guid.NewGuid(), TenantKey = request.TenantKey.Trim(), WarehouseId = warehouse.Id, Code = request.Code.Trim(), Name = request.Name.Trim(), Active = request.Active, CreatedAt = DateTimeOffset.UtcNow };
        db.StorageLocations.Add(entity); db.SaveChanges();
        return (new StorageLocationDto(entity.Id, entity.TenantKey, entity.WarehouseId, entity.Code, entity.Name, entity.Active, entity.CreatedAt), null);
    }

    public (StorageLocationDto? Location, string? Error) UpdateStorageLocation(string tenantKey, Guid locationId, UpdateStorageLocationRequest request)
    {
        using var db = dbFactory.CreateDbContext(); var entity = db.StorageLocations.SingleOrDefault(x => x.Id == locationId && x.TenantKey == tenantKey);
        if (entity is null) return (null, "storage_location_not_found");
        if (!db.Warehouses.Any(x => x.Id == request.WarehouseId && x.TenantKey == tenantKey && x.Active)) return (null, ManufacturingErrorCodes.WarehouseNotFound);
        var code = request.Code.Trim(); if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(request.Name)) return (null, "invalid_storage_location");
        if (db.StorageLocations.Any(x => x.Id != locationId && x.TenantKey == tenantKey && x.WarehouseId == request.WarehouseId && x.Code == code)) return (null, "location_code_exists");
        entity.WarehouseId = request.WarehouseId; entity.Code = code; entity.Name = request.Name.Trim(); entity.Active = request.Active; db.SaveChanges(); return (new StorageLocationDto(entity.Id, entity.TenantKey, entity.WarehouseId, entity.Code, entity.Name, entity.Active, entity.CreatedAt), null);
    }

    private static FacilityDto ToDto(ManufacturingFacilityEntity x) => new(x.Id, x.TenantKey, x.Code, x.Name, x.Active, x.CreatedAt);
}
