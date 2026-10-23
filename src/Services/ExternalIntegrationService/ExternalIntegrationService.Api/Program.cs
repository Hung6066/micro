using His.Hope.EventBus.Abstractions;
using His.Hope.EventBusRabbitMQ.Abstractions;
using His.Hope.EventBusRabbitMQ.Implementations;
using His.Hope.Infrastructure.Messaging;
using His.Hope.IntegrationEvents.Appointment;
using His.Hope.IntegrationEvents.Billing;
using His.Hope.IntegrationEvents.Clinical;
using His.Hope.IntegrationEvents.Lab;
using His.Hope.IntegrationEvents.Patient;
using His.Hope.IntegrationEvents.Pharmacy;
using His.Hope.ServiceDefaults;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHisHopeServiceDefaults(builder.Configuration, "external-integration-service");

builder.Services.AddOptions<ExternalIntegrationOptions>()
    .Bind(builder.Configuration.GetSection("ExternalIntegration"))
    .PostConfigure(options => options.Validate());

builder.Services.AddHisHopeLegacyRabbitMqEventBus(builder.Configuration);

builder.Services.AddTransient(typeof(ExternalIntegrationForwarder<>));

var app = builder.Build();
app.UseHisHopeServiceDefaults();

using (var scope = app.Services.CreateScope())
{
    var eventBus = scope.ServiceProvider.GetRequiredService<IEventBus>();
    eventBus.SubscribeAsync<AppointmentScheduledIntegrationEvent, ExternalIntegrationForwarder<AppointmentScheduledIntegrationEvent>>().GetAwaiter().GetResult();
    eventBus.SubscribeAsync<EncounterStartedIntegrationEvent, ExternalIntegrationForwarder<EncounterStartedIntegrationEvent>>().GetAwaiter().GetResult();
    eventBus.SubscribeAsync<InvoiceCreatedIntegrationEvent, ExternalIntegrationForwarder<InvoiceCreatedIntegrationEvent>>().GetAwaiter().GetResult();
    eventBus.SubscribeAsync<InvoicePaidIntegrationEvent, ExternalIntegrationForwarder<InvoicePaidIntegrationEvent>>().GetAwaiter().GetResult();
    eventBus.SubscribeAsync<LabOrderCreatedIntegrationEvent, ExternalIntegrationForwarder<LabOrderCreatedIntegrationEvent>>().GetAwaiter().GetResult();
    eventBus.SubscribeAsync<LabOrderSubmittedIntegrationEvent, ExternalIntegrationForwarder<LabOrderSubmittedIntegrationEvent>>().GetAwaiter().GetResult();
    eventBus.SubscribeAsync<PatientRegisteredIntegrationEvent, ExternalIntegrationForwarder<PatientRegisteredIntegrationEvent>>().GetAwaiter().GetResult();
    eventBus.SubscribeAsync<PatientUpdatedIntegrationEvent, ExternalIntegrationForwarder<PatientUpdatedIntegrationEvent>>().GetAwaiter().GetResult();
    eventBus.SubscribeAsync<PrescriptionCreatedIntegrationEvent, ExternalIntegrationForwarder<PrescriptionCreatedIntegrationEvent>>().GetAwaiter().GetResult();
}

app.MapGet("/health", () => Results.Ok(new { status = "healthy", service = "external-integration-service" }));
app.MapGet("/ready", () => Results.Ok(new { status = "ready" }));

app.MapHisHopeHealthEndpoints();
app.Run();

public sealed class ExternalIntegrationOptions
{
    public bool Enabled { get; set; }
    public string Provider { get; set; } = "default";
    public string ExchangeName { get; set; } = "his_hope_external_exchange";

    public void Validate()
    {
        if (Enabled && string.IsNullOrWhiteSpace(Provider))
            throw new InvalidOperationException("ExternalIntegration:Provider is required when external integration is enabled.");
        Provider = Provider.Trim().ToLowerInvariant();
    }
}

public sealed class ExternalIntegrationForwarder<TEvent>(
    IExternalEventPublisher publisher,
    IOptions<ExternalIntegrationOptions> options,
    ILogger<ExternalIntegrationForwarder<TEvent>> logger)
    : IIntegrationEventHandler<TEvent>
    where TEvent : IntegrationEvent
{
    public async Task HandleAsync(TEvent @event, CancellationToken cancellationToken = default)
    {
        if (!options.Value.Enabled)
            return;

        await publisher.PublishAsync(@event, options.Value.Provider, cancellationToken);
        logger.LogInformation("Forwarded {EventType} {EventId} to external provider {Provider}",
            typeof(TEvent).Name, @event.Id, options.Value.Provider);
    }
}
