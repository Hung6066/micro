using His.Hope.AspNetCore.Authentication;
using His.Hope.AspNetCore.Tenancy;
using His.Hope.ServiceDefaults;
using His.Hope.ManufacturingService.Infrastructure.Persistence;
using His.Hope.Infrastructure.Caching;
using His.Hope.Infrastructure.Security;
using StackExchange.Redis;

public static class ManufacturingApiExtensions
{
    public static void MapManufacturingServiceEndpoints(this WebApplication app)
    {
        var api = app.MapGroup("/api/v1/manufacturing")
            .RequireAuthorization()
            .RequireTenantContext()
            .AddEndpointFilter<MobileOperationReplayFilter>();

        api.MapInventoryEndpoints()
            .MapProductionEndpoints()
            .MapQualityEndpoints()
            .MapProcurementEndpoints()
            .MapMaintenanceEndpoints()
            .MapPlanningEndpoints()
            .MapMasterDataEndpoints()
            .MapDashboardEndpoints()
            .MapWorkflowEndpoints()
            .MapIntegrationEndpoints();
    }
}
