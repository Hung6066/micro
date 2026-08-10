using His.Hope.Bff.Core;
using His.Hope.Bff.Core.Proxy;
using His.Hope.Configuration;
using His.Hope.LabGrpc;

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddJsonFile(
    "Routes/lab-routes.json",
    optional: false,
    reloadOnChange: false);
var runtimeEndpoints = RuntimeConfigurationExtensions.BindServiceEndpoints(builder.Configuration, "LabBff");

builder.Configuration["ReverseProxy:Clusters:lab-service:Destinations:lab:Address"] =
    runtimeEndpoints.GetRequired("lab-api").ToString();

builder.Services.AddBffCore(builder.Configuration, "LabBff");
builder.Services.AddBffProxy(builder.Configuration);

builder.Services.AddGrpcClient<LabGrpcService.LabGrpcServiceClient>(o =>
    o.Address = runtimeEndpoints.GetRequired("lab-grpc"));

var app = builder.Build();

app.UseBffCoreMiddleware();
app.MapBffHealth();
app.MapBffReverseProxy();

app.Run();
