using ClinicalBff.Aggregation;
using His.Hope.Bff.Core;
using His.Hope.Bff.Core.Aggregation;
using His.Hope.Bff.Core.Authentication;
using His.Hope.Bff.Core.Proxy;
using His.Hope.ClinicalGrpc;
using His.Hope.PatientGrpc;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.UseUrls("http://0.0.0.0:5200");

builder.Services.AddBffCore(builder.Configuration);
builder.Services.AddBffProxy(builder.Configuration);

builder.Services.AddGrpcClient<ClinicalGrpcService.ClinicalGrpcServiceClient>(o =>
    o.Address = new Uri(builder.Configuration["Services:Clinical"]!));

builder.Services.AddGrpcClient<PatientGrpcService.PatientGrpcServiceClient>(o =>
    o.Address = new Uri(builder.Configuration["Services:Patient"]!));

builder.Services.AddScoped<IAggregationHandler, EncounterFullHandler>();
builder.Services.AddScoped<IAggregationHandler, EncounterVitalsHandler>();

var app = builder.Build();

app.UseBffSessionAuth();
app.UseBffCsrfProtection();
app.MapBffAggregation();
app.MapBffReverseProxy();

app.Run();
