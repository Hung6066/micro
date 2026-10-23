using His.Hope.Bff.Core;
using His.Hope.Bff.Core.Aggregation;
using His.Hope.Bff.Core.Proxy;
using His.Hope.Configuration;
using His.Hope.BillingGrpc;
using BillingBff.Aggregation;

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddJsonFile(
    "Routes/billing-routes.json",
    optional: false,
    reloadOnChange: false);
var runtimeEndpoints = RuntimeConfigurationExtensions.BindServiceEndpoints(builder.Configuration, "BillingBff");

builder.Configuration["ReverseProxy:Clusters:billing-service:Destinations:billing:Address"] =
    runtimeEndpoints.GetRequired("billing-api").ToString();

builder.Services.AddBffCore(builder.Configuration, "BillingBff");
builder.Services.AddBffProxy(builder.Configuration);

builder.Services.AddHisHopeGrpcClient<BillingGrpcService.BillingGrpcServiceClient>(runtimeEndpoints, "billing-grpc");

builder.Services.AddTransient<IAggregationHandler, InvoiceDetailedHandler>();

var app = builder.Build();

app.UseBffCoreMiddleware();
app.MapBffHealth();
app.MapBffReverseProxy();
app.MapBffAggregation();

app.Run();
