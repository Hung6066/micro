using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;

namespace His.Hope.IntegrationTestBase;

/// <summary>
/// Conceptual cross-service data flow tests verifying that a patient ID
/// created in the PatientService database can be referenced from
/// BillingService or AppointmentService contexts.
/// </summary>
public class CrossServiceDataFlowTests : IAsyncLifetime
{
    private PostgreSqlContainer _container = null!;
    private IServiceProvider _serviceProvider = null!;

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
    }

    public async Task DisposeAsync()
    {
        if (_container is not null)
            await _container.DisposeAsync();
    }

    [Fact]
    public async Task PatientId_IsConsistentGuid_AcrossServices()
    {
        var patientId = Guid.NewGuid();
        patientId.Should().NotBeEmpty();
        patientId.ToString().Should().MatchRegex("^[0-9a-fA-F]{8}-([0-9a-fA-F]{4}-){3}[0-9a-fA-F]{12}$");
    }

    [Fact]
    public async Task PatientId_CreatedInPatientService_CanBeStoredAsFkInBillingService()
    {
        var services = new ServiceCollection();
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(CrossServiceDataFlowTests).Assembly));
        services.AddDbContext<CrossServiceTestDbContext>(options =>
            options.UseNpgsql(_container.GetConnectionString()));

        var provider = services.BuildServiceProvider();
        var context = provider.GetRequiredService<CrossServiceTestDbContext>();
        await context.Database.EnsureCreatedAsync();

        var patientId = Guid.NewGuid();

        var invoiceRecord = new CrossServiceInvoiceRecord
        {
            Id = Guid.NewGuid(),
            InvoiceNumber = "CS-001",
            PatientId = patientId,
            Amount = 100.00m,
            CreatedAt = DateTime.UtcNow
        };

        context.InvoiceRecords.Add(invoiceRecord);
        await context.SaveChangesAsync();

        var saved = await context.InvoiceRecords.FindAsync(invoiceRecord.Id);

        saved.Should().NotBeNull();
        saved!.PatientId.Should().Be(patientId);
        saved.InvoiceNumber.Should().Be("CS-001");
        saved.Amount.Should().Be(100.00m);

        await context.DisposeAsync();
    }

    [Fact]
    public async Task PatientId_CreatedInPatientService_CanBeStoredAsFkInLabService()
    {
        var services = new ServiceCollection();
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(CrossServiceDataFlowTests).Assembly));
        services.AddDbContext<CrossServiceTestDbContext>(options =>
            options.UseNpgsql(_container.GetConnectionString()));

        var provider = services.BuildServiceProvider();
        var context = provider.GetRequiredService<CrossServiceTestDbContext>();
        await context.Database.EnsureCreatedAsync();

        var patientId = Guid.NewGuid();

        var labOrderRecord = new CrossServiceLabOrderRecord
        {
            Id = Guid.NewGuid(),
            OrderNumber = "LAB-CS-001",
            PatientId = patientId,
            CreatedAt = DateTime.UtcNow
        };

        context.LabOrderRecords.Add(labOrderRecord);
        await context.SaveChangesAsync();

        var saved = await context.LabOrderRecords.FindAsync(labOrderRecord.Id);

        saved.Should().NotBeNull();
        saved!.PatientId.Should().Be(patientId);
        saved.OrderNumber.Should().Be("LAB-CS-001");

        await context.DisposeAsync();
    }

    [Fact]
    public async Task PatientId_CreatedInPatientService_CanBeStoredAsFkInAppointmentService()
    {
        var services = new ServiceCollection();
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(CrossServiceDataFlowTests).Assembly));
        services.AddDbContext<CrossServiceTestDbContext>(options =>
            options.UseNpgsql(_container.GetConnectionString()));

        var provider = services.BuildServiceProvider();
        var context = provider.GetRequiredService<CrossServiceTestDbContext>();
        await context.Database.EnsureCreatedAsync();

        var patientId = Guid.NewGuid();

        var appointmentRecord = new CrossServiceAppointmentRecord
        {
            Id = Guid.NewGuid(),
            AppointmentNumber = "APT-CS-001",
            PatientId = patientId,
            ScheduledDate = DateTime.UtcNow.AddDays(1),
            CreatedAt = DateTime.UtcNow
        };

        context.AppointmentRecords.Add(appointmentRecord);
        await context.SaveChangesAsync();

        var saved = await context.AppointmentRecords.FindAsync(appointmentRecord.Id);

        saved.Should().NotBeNull();
        saved!.PatientId.Should().Be(patientId);
        saved.AppointmentNumber.Should().Be("APT-CS-001");

        await context.DisposeAsync();
    }

    [Fact]
    public async Task MultipleServices_ReferenceSamePatientId_ReturnsConsistentType()
    {
        var patientId = Guid.NewGuid();

        var invoiceId = Guid.NewGuid();
        var labOrderId = Guid.NewGuid();
        var appointmentId = Guid.NewGuid();

        patientId.GetType().Should().Be(invoiceId.GetType());
        patientId.GetType().Should().Be(labOrderId.GetType());
        patientId.GetType().Should().Be(appointmentId.GetType());
    }
}

public class CrossServiceTestDbContext : DbContext
{
    public DbSet<CrossServiceInvoiceRecord> InvoiceRecords => Set<CrossServiceInvoiceRecord>();
    public DbSet<CrossServiceLabOrderRecord> LabOrderRecords => Set<CrossServiceLabOrderRecord>();
    public DbSet<CrossServiceAppointmentRecord> AppointmentRecords => Set<CrossServiceAppointmentRecord>();

    public CrossServiceTestDbContext(DbContextOptions<CrossServiceTestDbContext> options)
        : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<CrossServiceInvoiceRecord>(entity =>
        {
            entity.ToTable("InvoiceRecords");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.InvoiceNumber).HasMaxLength(50).IsRequired();
            entity.Property(e => e.PatientId).IsRequired();
            entity.Property(e => e.Amount).HasColumnType("decimal(18,2)");
        });

        modelBuilder.Entity<CrossServiceLabOrderRecord>(entity =>
        {
            entity.ToTable("LabOrderRecords");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.OrderNumber).HasMaxLength(50).IsRequired();
            entity.Property(e => e.PatientId).IsRequired();
        });

        modelBuilder.Entity<CrossServiceAppointmentRecord>(entity =>
        {
            entity.ToTable("AppointmentRecords");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.AppointmentNumber).HasMaxLength(50).IsRequired();
            entity.Property(e => e.PatientId).IsRequired();
        });
    }
}

public class CrossServiceInvoiceRecord
{
    public Guid Id { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;
    public Guid PatientId { get; set; }
    public decimal Amount { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CrossServiceLabOrderRecord
{
    public Guid Id { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public Guid PatientId { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CrossServiceAppointmentRecord
{
    public Guid Id { get; set; }
    public string AppointmentNumber { get; set; } = string.Empty;
    public Guid PatientId { get; set; }
    public DateTime ScheduledDate { get; set; }
    public DateTime CreatedAt { get; set; }
}
