using His.Hope.Bff.Core;
using His.Hope.Bff.Core.Aggregation;
using His.Hope.Bff.Core.Authentication;
using His.Hope.Bff.Core.Proxy;
using His.Hope.BillingGrpc;
using BillingBff.Aggregation;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddBffCore(builder.Configuration);
builder.Services.AddBffProxy(builder.Configuration);

builder.Services.AddGrpcClient<BillingGrpcService.BillingGrpcServiceClient>(o =>
    o.Address = new Uri(builder.Configuration["Services:Billing"]!));

builder.Services.AddTransient<IAggregationHandler, InvoiceDetailedHandler>();

var app = builder.Build();

app.UseBffSessionAuth();
app.UseBffCsrfProtection();
app.MapBffReverseProxy();
app.MapBffAggregation();

app.Run();
