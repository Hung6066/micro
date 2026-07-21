using His.Hope.Bff.Core;
using His.Hope.Bff.Core.Proxy;
using His.Hope.LabGrpc;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddBffCore(builder.Configuration);
builder.Services.AddBffProxy(builder.Configuration);

builder.Services.AddGrpcClient<LabGrpcService.LabGrpcServiceClient>(o =>
    o.Address = new Uri(builder.Configuration["Services:Lab"]!));

var app = builder.Build();

app.UseBffCoreMiddleware();
app.MapBffReverseProxy();

app.Run();
