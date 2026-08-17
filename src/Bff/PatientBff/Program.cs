using His.Hope.Bff.Core;
using His.Hope.Bff.Core.Aggregation;
using His.Hope.Bff.Core.Proxy;
using His.Hope.Configuration;
using His.Hope.PatientGrpc;
using His.Hope.ClinicalGrpc;
using His.Hope.LabGrpc;
using His.Hope.PharmacyGrpc;
using PatientBff.Aggregation;

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddJsonFile(
    "Routes/patient-routes.json",
    optional: false,
    reloadOnChange: false);
var runtimeEndpoints = RuntimeConfigurationExtensions.BindServiceEndpoints(builder.Configuration, "PatientBff");

// Keep the YARP destination on the canonical runtime contract. The checked-in
// route file is shared with Compose/VM, while K3s uses the service DNS name
// supplied by SERVICE_PATIENT_API_URL.
builder.Configuration["ReverseProxy:Clusters:patient-service:Destinations:patient:Address"] =
    runtimeEndpoints.GetRequired("patient-api").ToString();

builder.Services.AddBffCore(builder.Configuration, "PatientBff");
builder.Services.AddBffProxy(builder.Configuration);

builder.Services.AddGrpcClient<PatientGrpcService.PatientGrpcServiceClient>(o =>
    o.Address = runtimeEndpoints.GetRequired("patient-grpc"));
builder.Services.AddGrpcClient<ClinicalGrpcService.ClinicalGrpcServiceClient>(o =>
    o.Address = runtimeEndpoints.GetRequired("clinical-grpc"));
builder.Services.AddGrpcClient<LabGrpcService.LabGrpcServiceClient>(o =>
    o.Address = runtimeEndpoints.GetRequired("lab-grpc"));
builder.Services.AddGrpcClient<PharmacyGrpcService.PharmacyGrpcServiceClient>(o =>
    o.Address = runtimeEndpoints.GetRequired("pharmacy-grpc"));

builder.Services.AddSingleton<IAggregationHandler, PatientTimelineHandler>();

var app = builder.Build();

app.UseBffCoreMiddleware();
app.MapBffHealth();
app.MapBffReverseProxy();
app.MapBffAggregation();

app.Run();

public partial class Program { }
