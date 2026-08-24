using System.Collections.Concurrent;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton<ManufacturingStore>();
builder.Services.AddHealthChecks();
builder.Services.AddProblemDetails();

var app = builder.Build();
app.UseExceptionHandler();

app.MapHealthChecks("/health").AllowAnonymous();

var api = app.MapGroup("/api/v1/manufacturing");

api.MapGet("/lots/{lotId:guid}/genealogy", (Guid lotId, string? direction, ManufacturingStore store) =>
{
    if (!store.Lots.ContainsKey(lotId))
        return Results.NotFound(new { error = "lot_not_found", lotId });

    var upstream = !string.Equals(direction, "downstream", StringComparison.OrdinalIgnoreCase);
    return Results.Ok(store.GetGenealogy(lotId, upstream));
});

api.MapPost("/lots", (CreateLotRequest request, ManufacturingStore store) =>
{
    if (string.IsNullOrWhiteSpace(request.TenantKey) || string.IsNullOrWhiteSpace(request.Sku) ||
        request.Quantity <= 0 || string.IsNullOrWhiteSpace(request.Uom))
        return Results.BadRequest(new { error = "invalid_lot", message = "tenantKey, sku, quantity and uom are required" });

    var lot = store.CreateLot(request);
    return Results.Created($"/api/v1/manufacturing/lots/{lot.Id}", lot);
});

api.MapPost("/transformations", (CreateTransformationRequest request, ManufacturingStore store) =>
{
    if (request.Inputs is null || request.Inputs.Count == 0 || request.OutputQuantity <= 0 ||
        string.IsNullOrWhiteSpace(request.OutputSku))
        return Results.BadRequest(new { error = "invalid_transformation" });

    var result = store.CreateTransformation(request);
    return result.Error is not null
        ? Results.BadRequest(new { error = result.Error })
        : Results.Created($"/api/v1/manufacturing/transformations/{result.Transformation!.Id}", result.Transformation);
});

api.MapGet("/products/{sku}/availability", (string sku, string? tenantKey, ManufacturingStore store) =>
{
    var tenant = string.IsNullOrWhiteSpace(tenantKey) ? "customer-factory-x" : tenantKey;
    return Results.Ok(store.GetAvailability(tenant, sku));
});

app.Run();

public sealed record CreateLotRequest(
    string TenantKey,
    string Sku,
    decimal Quantity,
    string Uom,
    string Disposition = "Released",
    DateOnly? BestBefore = null);

public sealed record CreateTransformationRequest(
    string TenantKey,
    string OutputSku,
    decimal OutputQuantity,
    string OutputUom,
    IReadOnlyList<TransformationInput> Inputs,
    string ProcessStep = "production");

public sealed record TransformationInput(Guid LotId, decimal Quantity);

public sealed record LotDto(
    Guid Id,
    string TenantKey,
    string Sku,
    decimal Quantity,
    string Uom,
    string Disposition,
    DateOnly? BestBefore,
    DateTimeOffset CreatedAt);

public sealed record TransformationDto(
    Guid Id,
    string TenantKey,
    string ProcessStep,
    IReadOnlyList<TransformationInput> Inputs,
    LotDto Output,
    decimal InputQuantity,
    decimal YieldPercent,
    decimal LossQuantity,
    DateTimeOffset CreatedAt);

public sealed record GenealogyDto(LotDto Lot, IReadOnlyList<LotRelationDto> Relations);
public sealed record LotRelationDto(Guid TransformationId, Guid LotId, string Sku, string Role, decimal Quantity);
public sealed record AvailabilityDto(string TenantKey, string Sku, decimal ReleasedQuantity, string Uom, DateTimeOffset AsOf);

public sealed class ManufacturingStore
{
    public ConcurrentDictionary<Guid, LotDto> Lots { get; } = new();
    private readonly ConcurrentDictionary<Guid, TransformationDto> transformations = new();
    private readonly ConcurrentDictionary<Guid, List<LotRelationDto>> relations = new();

    public ManufacturingStore()
    {
        var seed = CreateLot(new CreateLotRequest("customer-factory-x", "RM-MANGO-001", 1000, "kg"));
        CreateTransformation(new CreateTransformationRequest(
            "customer-factory-x", "FX-MANGO-SOFT", 320, "kg",
            new[] { new TransformationInput(seed.Id, 400) }, "drying"));
    }

    public LotDto CreateLot(CreateLotRequest request)
    {
        var lot = new LotDto(Guid.NewGuid(), request.TenantKey.Trim(), request.Sku.Trim(), request.Quantity,
            request.Uom.Trim(), request.Disposition.Trim(), request.BestBefore, DateTimeOffset.UtcNow);
        Lots[lot.Id] = lot;
        return lot;
    }

    public (TransformationDto? Transformation, string? Error) CreateTransformation(CreateTransformationRequest request)
    {
        var inputs = new List<(LotDto Lot, decimal Quantity)>();
        foreach (var input in request.Inputs)
        {
            if (!Lots.TryGetValue(input.LotId, out var lot)) return (null, "input_lot_not_found");
            if (!string.Equals(lot.TenantKey, request.TenantKey, StringComparison.OrdinalIgnoreCase)) return (null, "tenant_mismatch");
            if (input.Quantity <= 0 || input.Quantity > lot.Quantity) return (null, "input_quantity_exceeds_lot");
            inputs.Add((lot, input.Quantity));
        }

        foreach (var (lot, quantity) in inputs)
            Lots[lot.Id] = lot with { Quantity = lot.Quantity - quantity };

        var output = CreateLot(new CreateLotRequest(request.TenantKey, request.OutputSku, request.OutputQuantity, request.OutputUom));
        var inputQuantity = inputs.Sum(x => x.Quantity);
        var transformation = new TransformationDto(Guid.NewGuid(), request.TenantKey, request.ProcessStep,
            request.Inputs, output, inputQuantity, decimal.Round(request.OutputQuantity / inputQuantity * 100, 2),
            inputQuantity - request.OutputQuantity, DateTimeOffset.UtcNow);
        transformations[transformation.Id] = transformation;

        var links = inputs.Select(x => new LotRelationDto(transformation.Id, x.Lot.Id, x.Lot.Sku, "input", x.Quantity))
            .Append(new LotRelationDto(transformation.Id, output.Id, output.Sku, "output", output.Quantity)).ToList();
        relations[transformation.Id] = links;
        return (transformation, null);
    }

    public GenealogyDto GetGenealogy(Guid lotId, bool upstream)
    {
        var result = relations.Values.SelectMany(x => x)
            .Where(x => x.LotId == lotId && (upstream ? x.Role == "output" : x.Role == "input"))
            .ToList();
        var ids = result.Select(x => x.TransformationId).ToHashSet();
        var linked = relations.Values.SelectMany(x => x).Where(x => ids.Contains(x.TransformationId)).ToList();
        return new GenealogyDto(Lots[lotId], linked);
    }

    public AvailabilityDto GetAvailability(string tenantKey, string sku)
    {
        var quantity = Lots.Values.Where(x => x.TenantKey.Equals(tenantKey, StringComparison.OrdinalIgnoreCase)
            && x.Sku.Equals(sku, StringComparison.OrdinalIgnoreCase)
            && x.Disposition.Equals("Released", StringComparison.OrdinalIgnoreCase)).Sum(x => x.Quantity);
        return new AvailabilityDto(tenantKey, sku, quantity, "kg", DateTimeOffset.UtcNow);
    }
}
