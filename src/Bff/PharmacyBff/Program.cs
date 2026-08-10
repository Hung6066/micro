using His.Hope.Bff.Core;
using His.Hope.Bff.Core.Aggregation;
using His.Hope.Bff.Core.Proxy;
using His.Hope.Configuration;
using His.Hope.PharmacyGrpc;

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddJsonFile(
    "Routes/pharmacy-routes.json",
    optional: false,
    reloadOnChange: false);
var runtimeEndpoints = RuntimeConfigurationExtensions.BindServiceEndpoints(builder.Configuration, "PharmacyBff");

builder.Configuration["ReverseProxy:Clusters:pharmacy-service:Destinations:pharmacy:Address"] =
    runtimeEndpoints.GetRequired("pharmacy-api").ToString();

builder.Services.AddBffCore(builder.Configuration, "PharmacyBff");
builder.Services.AddBffProxy(builder.Configuration);

builder.Services.AddGrpcClient<PharmacyGrpcService.PharmacyGrpcServiceClient>(o =>
    o.Address = runtimeEndpoints.GetRequired("pharmacy-grpc"));

builder.Services.AddSingleton<PharmacyBff.Aggregation.MedicationFullHandler>();
builder.Services.AddSingleton<IAggregationHandler>(sp =>
    sp.GetRequiredService<PharmacyBff.Aggregation.MedicationFullHandler>());

var app = builder.Build();

app.UseBffCoreMiddleware();
app.MapBffHealth();
app.MapBffReverseProxy();
app.MapBffAggregation();

app.Run();
