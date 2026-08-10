using His.Hope.Bff.Core;
using His.Hope.Bff.Core.Aggregation;
using His.Hope.Configuration;
using His.Hope.PatientGrpc;
using His.Hope.ClinicalGrpc;
using His.Hope.LabGrpc;
using His.Hope.BillingGrpc;
using His.Hope.PharmacyGrpc;
using DashboardBff.Aggregation;

var builder = WebApplication.CreateBuilder(args);
var runtimeEndpoints = RuntimeConfigurationExtensions.BindServiceEndpoints(builder.Configuration, "DashboardBff");

builder.Services.AddBffCore(builder.Configuration, "DashboardBff");

builder.Services.AddGrpcClient<PatientGrpcService.PatientGrpcServiceClient>(o =>
    o.Address = runtimeEndpoints.GetRequired("patient-grpc"));
builder.Services.AddGrpcClient<ClinicalGrpcService.ClinicalGrpcServiceClient>(o =>
    o.Address = runtimeEndpoints.GetRequired("clinical-grpc"));
builder.Services.AddGrpcClient<LabGrpcService.LabGrpcServiceClient>(o =>
    o.Address = runtimeEndpoints.GetRequired("lab-grpc"));
builder.Services.AddGrpcClient<BillingGrpcService.BillingGrpcServiceClient>(o =>
    o.Address = runtimeEndpoints.GetRequired("billing-grpc"));
builder.Services.AddGrpcClient<PharmacyGrpcService.PharmacyGrpcServiceClient>(o =>
    o.Address = runtimeEndpoints.GetRequired("pharmacy-grpc"));
builder.Services.AddHttpClient("appointment-api", client =>
    client.BaseAddress = runtimeEndpoints.GetRequired("appointment-api"));

builder.Services.AddScoped<IAggregationHandler, DashboardStatsHandler>();
builder.Services.AddScoped<IAggregationHandler, RecentEncountersHandler>();
builder.Services.AddScoped<IAggregationHandler, UpcomingAppointmentsHandler>();

var app = builder.Build();

app.UseBffCoreMiddleware();
app.MapBffHealth();
app.MapBffAggregation();

app.Run();

public partial class Program { }
