using His.Hope.Bff.Core;
using His.Hope.Bff.Core.Aggregation;
using His.Hope.Configuration;
using His.Hope.ServiceDefaults;
using His.Hope.PatientGrpc;
using His.Hope.ClinicalGrpc;
using His.Hope.LabGrpc;
using His.Hope.BillingGrpc;
using His.Hope.PharmacyGrpc;
using DashboardBff.Aggregation;

var builder = WebApplication.CreateBuilder(args);
var runtimeEndpoints = RuntimeConfigurationExtensions.BindServiceEndpoints(builder.Configuration, "DashboardBff");

builder.Services.AddBffCore(builder.Configuration, "DashboardBff");

builder.Services.AddHisHopeGrpcClient<PatientGrpcService.PatientGrpcServiceClient>(runtimeEndpoints, "patient-grpc");
builder.Services.AddHisHopeGrpcClient<ClinicalGrpcService.ClinicalGrpcServiceClient>(runtimeEndpoints, "clinical-grpc");
builder.Services.AddHisHopeGrpcClient<LabGrpcService.LabGrpcServiceClient>(runtimeEndpoints, "lab-grpc");
builder.Services.AddHisHopeGrpcClient<BillingGrpcService.BillingGrpcServiceClient>(runtimeEndpoints, "billing-grpc");
builder.Services.AddHisHopeGrpcClient<PharmacyGrpcService.PharmacyGrpcServiceClient>(runtimeEndpoints, "pharmacy-grpc");
builder.Services.AddHisHopeServiceHttpClient(
    "appointment-api",
    "dashboard.appointment",
    client => client.BaseAddress = runtimeEndpoints.GetRequired("appointment-api"));

// Aggregation handlers are stateless and use thread-safe gRPC/HTTP clients. Registering
// them as singletons also lets the route map be built once at startup without resolving
// scoped services from the root provider.
builder.Services.AddSingleton<IAggregationHandler, DashboardStatsHandler>();
builder.Services.AddSingleton<IAggregationHandler, RecentEncountersHandler>();
builder.Services.AddSingleton<IAggregationHandler, UpcomingAppointmentsHandler>();

var app = builder.Build();

app.UseBffCoreMiddleware();
app.MapBffHealth();
app.MapBffAggregation();

app.Run();

public partial class Program { }
