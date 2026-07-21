using His.Hope.Bff.Core;
using His.Hope.Bff.Core.Aggregation;
using His.Hope.Bff.Core.Proxy;
using His.Hope.PatientGrpc;
using His.Hope.ClinicalGrpc;
using His.Hope.LabGrpc;
using His.Hope.PharmacyGrpc;
using PatientBff.Aggregation;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddBffCore(builder.Configuration);
builder.Services.AddBffProxy(builder.Configuration);

builder.Services.AddGrpcClient<PatientGrpcService.PatientGrpcServiceClient>(o =>
    o.Address = new Uri(builder.Configuration["Services:Patient"]!));
builder.Services.AddGrpcClient<ClinicalGrpcService.ClinicalGrpcServiceClient>(o =>
    o.Address = new Uri(builder.Configuration["Services:Clinical"]!));
builder.Services.AddGrpcClient<LabGrpcService.LabGrpcServiceClient>(o =>
    o.Address = new Uri(builder.Configuration["Services:Lab"]!));
builder.Services.AddGrpcClient<PharmacyGrpcService.PharmacyGrpcServiceClient>(o =>
    o.Address = new Uri(builder.Configuration["Services:Pharmacy"]!));

builder.Services.AddSingleton<IAggregationHandler, PatientTimelineHandler>();

var app = builder.Build();

app.UseBffCoreMiddleware();
app.MapBffReverseProxy();
app.MapBffAggregation();

app.Run();

public partial class Program { }
