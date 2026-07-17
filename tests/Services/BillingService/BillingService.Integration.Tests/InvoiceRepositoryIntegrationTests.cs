using FluentAssertions;
using His.Hope.BillingService.Domain.Aggregates;
using His.Hope.BillingService.Domain.Entities;
using His.Hope.BillingService.Domain.ValueObjects;
using His.Hope.BillingService.Infrastructure.Persistence;
using His.Hope.BillingService.Infrastructure.Persistence.Repositories;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;

namespace His.Hope.BillingService.Integration.Tests;

public class InvoiceRepositoryIntegrationTests : IAsyncLifetime
{
    private PostgreSqlContainer _container = null!;
    private BillingDbContext _context = null!;
    private InvoiceRepository _repository = null!;

    public async Task InitializeAsync()
    {
        _container = new PostgreSqlBuilder()
            .WithImage("postgres:16-alpine")
            .WithDatabase("hishopetest")
            .WithUsername("testuser")
            .WithPassword("testpass123!")
            .WithCleanUp(true)
            .Build();

        await _container.StartAsync();

        var services = new ServiceCollection();
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(InvoiceRepositoryIntegrationTests).Assembly));
        services.AddDbContext<BillingDbContext>(options =>
            options.UseNpgsql(_container.GetConnectionString()));
        services.AddScoped<InvoiceRepository>();

        var provider = services.BuildServiceProvider();
        _context = provider.GetRequiredService<BillingDbContext>();
        _repository = provider.GetRequiredService<InvoiceRepository>();

        await _context.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync()
    {
        await _context.DisposeAsync();
        await _container.DisposeAsync();
    }

    [Fact]
    public async Task AddAndRetrieve_ValidInvoice_ReturnsInvoiceWithProperties()
    {
        var invoice = Invoice.Create(
            Guid.NewGuid(), null, "INV-001",
            DateTime.UtcNow, DateTime.UtcNow.AddDays(30), null);

        var added = await _repository.AddAsync(invoice);
        await _repository.UnitOfWork.SaveChangesAsync();

        var retrieved = await _repository.GetByIdAsync(invoice.Id);

        retrieved.Should().NotBeNull();
        retrieved!.Id.Should().Be(invoice.Id);
        retrieved.InvoiceNumber.Should().Be("INV-001");
        retrieved.Status.Code.Should().Be("DRAFT");
        retrieved.SubTotal.Should().Be(0);
    }

    [Fact]
    public async Task AddInvoice_WithLineItems_CalculatesSubTotal()
    {
        var patientId = Guid.NewGuid();
        var invoice = Invoice.Create(
            patientId, null, "INV-002",
            DateTime.UtcNow, null, null);

        invoice.AddLineItem(InvoiceLineItem.Create(
            invoice.Id, "Consultation fee", 1, 150.00m,
            "CONS-001", InvoiceLineItemType.Consultation));

        invoice.AddLineItem(InvoiceLineItem.Create(
            invoice.Id, "Lab test - CBC", 1, 75.00m,
            "LAB-CBC", InvoiceLineItemType.Lab));

        await _repository.AddAsync(invoice);
        await _repository.UnitOfWork.SaveChangesAsync();

        var retrieved = await _repository.GetByIdAsync(invoice.Id);

        retrieved.Should().NotBeNull();
        retrieved!.LineItems.Should().HaveCount(2);
        retrieved.SubTotal.Should().Be(225.00m);
    }

    [Fact]
    public async Task GetByInvoiceNumber_ExistingNumber_ReturnsInvoice()
    {
        var invoice = Invoice.Create(
            Guid.NewGuid(), null, "INV-UNIQUE-999",
            DateTime.UtcNow, null, "Test notes");

        await _repository.AddAsync(invoice);
        await _repository.UnitOfWork.SaveChangesAsync();

        var retrieved = await _repository.GetByInvoiceNumberAsync("INV-UNIQUE-999");

        retrieved.Should().NotBeNull();
        retrieved!.InvoiceNumber.Should().Be("INV-UNIQUE-999");
        retrieved.Notes.Should().Be("Test notes");
    }

    [Fact]
    public async Task GetByPatient_ReturnsPatientInvoices()
    {
        var patientId = Guid.NewGuid();

        var invoice1 = Invoice.Create(patientId, null, "INV-P1", DateTime.UtcNow, null, null);
        var invoice2 = Invoice.Create(patientId, null, "INV-P2", DateTime.UtcNow, null, null);

        await _repository.AddAsync(invoice1);
        await _repository.AddAsync(invoice2);
        await _repository.UnitOfWork.SaveChangesAsync();

        var invoices = await _repository.GetByPatientAsync(patientId);

        invoices.Should().HaveCount(2);
        invoices.Should().Contain(i => i.InvoiceNumber == "INV-P1");
        invoices.Should().Contain(i => i.InvoiceNumber == "INV-P2");
    }

    [Fact]
    public async Task RemoveInvoice_DeletesFromDatabase()
    {
        var invoice = Invoice.Create(
            Guid.NewGuid(), null, "INV-DEL",
            DateTime.UtcNow, null, null);

        await _repository.AddAsync(invoice);
        await _repository.UnitOfWork.SaveChangesAsync();

        await _repository.RemoveAsync(invoice);
        await _repository.UnitOfWork.SaveChangesAsync();

        var retrieved = await _repository.GetByIdAsync(invoice.Id);
        retrieved.Should().BeNull();
    }
}
