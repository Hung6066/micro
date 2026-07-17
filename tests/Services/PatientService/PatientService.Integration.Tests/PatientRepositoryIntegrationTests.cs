using FluentAssertions;
using His.Hope.Infrastructure.Outbox;
using His.Hope.PatientService.Domain.Aggregates;
using His.Hope.PatientService.Domain.Entities;
using His.Hope.PatientService.Infrastructure.Persistence;
using His.Hope.PatientService.Infrastructure.Persistence.Repositories;
using His.Hope.PatientService.Domain.ValueObjects;
using His.Hope.SharedKernel.Domain.ValueObjects;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;

namespace His.Hope.PatientService.Integration.Tests;

public class PatientRepositoryIntegrationTests : IAsyncLifetime
{
    private PostgreSqlContainer _container = null!;
    private PatientDbContext _context = null!;
    private PatientRepository _repository = null!;

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
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(PatientRepositoryIntegrationTests).Assembly));
        services.AddDbContext<PatientDbContext>(options =>
            options.UseNpgsql(_container.GetConnectionString()));
        services.AddScoped<PatientRepository>();

        var provider = services.BuildServiceProvider();
        _context = provider.GetRequiredService<PatientDbContext>();
        _repository = provider.GetRequiredService<PatientRepository>();

        await _context.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync()
    {
        await _context.DisposeAsync();
        await _container.DisposeAsync();
    }

    [Fact]
    public async Task AddAndRetrieve_ValidPatient_ReturnsPatientWithProperties()
    {
        var patient = Patient.Register(
            PersonName.Create("John", "Michael", "Doe"),
            new DateTime(1990, 5, 15),
            Gender.Male,
            ContactInfo.Create("+1234567890", "john.doe@example.com"),
            Address.Create("123 Main St", "", "Springfield", "IL", "62701", "USA"));

        var added = await _repository.AddAsync(patient);
        await _repository.UnitOfWork.SaveChangesAsync();

        var retrieved = await _repository.GetByIdAsync(patient.Id);

        retrieved.Should().NotBeNull();
        retrieved!.Id.Should().Be(patient.Id);
        retrieved.Name.FullName.Should().Be("John Michael Doe");
        retrieved.Name.FirstName.Should().Be("John");
        retrieved.Name.LastName.Should().Be("Doe");
        retrieved.DateOfBirth.Should().Be(new DateTime(1990, 5, 15));
        retrieved.Gender.Code.Should().Be("M");
        retrieved.ContactInfo.Phone.Should().Be("+1234567890");
        retrieved.ContactInfo.Email.Should().Be("john.doe@example.com");
        retrieved.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task SearchPatients_ByFirstName_ReturnsMatchingPatients()
    {
        var patient1 = Patient.Register(
            PersonName.Create("Alice", null, "Johnson"),
            new DateTime(1985, 3, 10), Gender.Female,
            ContactInfo.Create("+1111111111", "alice@example.com"),
            Address.Create("1 Oak Ave", "", "Portland", "OR", "97201", "USA"));

        var patient2 = Patient.Register(
            PersonName.Create("Bob", null, "Smith"),
            new DateTime(1978, 7, 22), Gender.Male,
            ContactInfo.Create("+2222222222", "bob@example.com"),
            Address.Create("2 Elm St", "", "Portland", "OR", "97202", "USA"));

        await _repository.AddAsync(patient1);
        await _repository.AddAsync(patient2);
        await _repository.UnitOfWork.SaveChangesAsync();

        var (items, totalCount) = await _repository.SearchAsync("Alice", 1, 10);

        items.Should().HaveCount(1);
        totalCount.Should().Be(1);
        items[0].Name.FirstName.Should().Be("Alice");
    }

    [Fact]
    public async Task SearchPatients_ByPhone_ReturnsMatchingPatients()
    {
        var patient = Patient.Register(
            PersonName.Create("Charlie", null, "Brown"),
            new DateTime(1995, 1, 5), Gender.Male,
            ContactInfo.Create("+3333333333", "charlie@example.com"),
            Address.Create("3 Pine Rd", "", "Denver", "CO", "80201", "USA"));

        await _repository.AddAsync(patient);
        await _repository.UnitOfWork.SaveChangesAsync();

        var (items, totalCount) = await _repository.SearchAsync("+3333333333", 1, 10);

        items.Should().HaveCount(1);
        totalCount.Should().Be(1);
        items[0].ContactInfo.Phone.Should().Be("+3333333333");
    }

    [Fact]
    public async Task GetPatients_WithPagination_ReturnsCorrectPage()
    {
        for (int i = 1; i <= 5; i++)
        {
            var p = Patient.Register(
                PersonName.Create($"First{i}", null, $"Last{i}"),
                new DateTime(2000, 1, i), Gender.Male,
                ContactInfo.Create($"+100000000{i}", $"user{i}@example.com"),
                Address.Create($"{i} St", "", "City", "ST", "00001", "USA"));
            await _repository.AddAsync(p);
        }
        await _repository.UnitOfWork.SaveChangesAsync();

        var (page1, total1) = await _repository.SearchAsync("", 1, 2);
        page1.Should().HaveCount(2);
        total1.Should().Be(5);

        var (page2, total2) = await _repository.SearchAsync("", 2, 2);
        page2.Should().HaveCount(2);
        total2.Should().Be(5);

        var (page3, total3) = await _repository.SearchAsync("", 3, 2);
        page3.Should().HaveCount(1);
        total3.Should().Be(5);
    }

    [Fact]
    public async Task UpdatePatient_ChangesFields_RetainsUpdates()
    {
        var patient = Patient.Register(
            PersonName.Create("David", null, "Jones"),
            new DateTime(1988, 11, 30), Gender.Male,
            ContactInfo.Create("+4444444444", "david.old@example.com"),
            Address.Create("4 Lake Dr", "", "Dallas", "TX", "75201", "USA"));

        await _repository.AddAsync(patient);
        await _repository.UnitOfWork.SaveChangesAsync();

        patient.UpdatePersonalInfo(
            PersonName.Create("David", "Robert", "Jones-Renamed"),
            new DateTime(1988, 11, 30), Gender.Male,
            ContactInfo.Create("+4444444444", "david.new@example.com"),
            Address.Create("4 Lake Dr", "", "Dallas", "TX", "75201", "USA"));

        await _repository.UpdateAsync(patient);
        await _repository.UnitOfWork.SaveChangesAsync();

        var retrieved = await _repository.GetByIdAsync(patient.Id);

        retrieved.Should().NotBeNull();
        retrieved!.Name.FullName.Should().Be("David Robert Jones-Renamed");
        retrieved.ContactInfo.Email.Should().Be("david.new@example.com");
    }
}
