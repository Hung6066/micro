using His.Hope.Bff.Core;
using His.Hope.Bff.Core.Aggregation;
using His.Hope.PatientGrpc;
using His.Hope.ClinicalGrpc;
using His.Hope.LabGrpc;
using His.Hope.BillingGrpc;
using His.Hope.PharmacyGrpc;
using His.Hope.AppointmentGrpc;
using DashboardBff.Aggregation;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddBffCore(builder.Configuration);

builder.Services.AddGrpcClient<PatientGrpcService.PatientGrpcServiceClient>(o =>
    o.Address = new Uri(builder.Configuration["Services:Patient"]!));
builder.Services.AddGrpcClient<ClinicalGrpcService.ClinicalGrpcServiceClient>(o =>
    o.Address = new Uri(builder.Configuration["Services:Clinical"]!));
builder.Services.AddGrpcClient<LabGrpcService.LabGrpcServiceClient>(o =>
    o.Address = new Uri(builder.Configuration["Services:Lab"]!));
builder.Services.AddGrpcClient<BillingGrpcService.BillingGrpcServiceClient>(o =>
    o.Address = new Uri(builder.Configuration["Services:Billing"]!));
builder.Services.AddGrpcClient<PharmacyGrpcService.PharmacyGrpcServiceClient>(o =>
    o.Address = new Uri(builder.Configuration["Services:Pharmacy"]!));
builder.Services.AddGrpcClient<AppointmentGrpcService.AppointmentGrpcServiceClient>(o =>
    o.Address = new Uri(builder.Configuration["Services:Appointment"]!));

builder.Services.AddScoped<IAggregationHandler, DashboardStatsHandler>();
builder.Services.AddScoped<IAggregationHandler, RecentEncountersHandler>();
builder.Services.AddScoped<IAggregationHandler, UpcomingAppointmentsHandler>();

var app = builder.Build();

app.UseBffCoreMiddleware();
app.MapBffAggregation();

app.Run();

public partial class Program { }
