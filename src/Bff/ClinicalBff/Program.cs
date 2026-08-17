using ClinicalBff.Aggregation;
using His.Hope.Bff.Core;
using His.Hope.Bff.Core.Aggregation;
using His.Hope.Bff.Core.Proxy;
using His.Hope.Configuration;
using His.Hope.ClinicalGrpc;
using His.Hope.PatientGrpc;

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddJsonFile(
    "Routes/clinical-routes.json",
    optional: false,
    reloadOnChange: false);
var runtimeEndpoints = RuntimeConfigurationExtensions.BindServiceEndpoints(builder.Configuration, "ClinicalBff");

builder.Configuration["ReverseProxy:Clusters:clinical-service:Destinations:clinical:Address"] =
    runtimeEndpoints.GetRequired("clinical-api").ToString();

builder.WebHost.UseUrls("http://0.0.0.0:5200");

builder.Services.AddBffCore(builder.Configuration, "ClinicalBff");
builder.Services.AddBffProxy(builder.Configuration);

builder.Services.AddGrpcClient<ClinicalGrpcService.ClinicalGrpcServiceClient>(o =>
    o.Address = runtimeEndpoints.GetRequired("clinical-grpc"));

builder.Services.AddGrpcClient<PatientGrpcService.PatientGrpcServiceClient>(o =>
    o.Address = runtimeEndpoints.GetRequired("patient-grpc"));

builder.Services.AddScoped<IAggregationHandler, EncounterFullHandler>();
builder.Services.AddScoped<IAggregationHandler, EncounterVitalsHandler>();

var app = builder.Build();

app.UseBffCoreMiddleware();
app.MapBffHealth();
app.MapBffAggregation();
app.MapBffReverseProxy();

app.Run();
