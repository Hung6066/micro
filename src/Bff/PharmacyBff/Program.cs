using His.Hope.Bff.Core;
using His.Hope.Bff.Core.Authentication;
using His.Hope.Bff.Core.Aggregation;
using His.Hope.Bff.Core.Proxy;
using His.Hope.PharmacyGrpc;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddBffCore(builder.Configuration);
builder.Services.AddBffProxy(builder.Configuration);

builder.Services.AddGrpcClient<PharmacyGrpcService.PharmacyGrpcServiceClient>(o =>
    o.Address = new Uri(builder.Configuration["Services:Pharmacy"]!));

builder.Services.AddSingleton<PharmacyBff.Aggregation.MedicationFullHandler>();
builder.Services.AddSingleton<IAggregationHandler>(sp =>
    sp.GetRequiredService<PharmacyBff.Aggregation.MedicationFullHandler>());

var app = builder.Build();

app.UseBffSessionAuth();
app.UseBffCsrfProtection();
app.MapBffReverseProxy();
app.MapBffAggregation();

app.Run();
