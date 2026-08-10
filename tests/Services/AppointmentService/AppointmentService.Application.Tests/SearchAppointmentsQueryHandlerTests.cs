using AutoMapper;
using FluentAssertions;
using His.Hope.AppointmentService.Application.DTOs;
using His.Hope.AppointmentService.Application.UseCases.Appointments.Queries;
using His.Hope.AppointmentService.Domain.Aggregates;
using His.Hope.AppointmentService.Domain.Repositories;
using His.Hope.AppointmentService.Domain.ValueObjects;
using Moq;

namespace His.Hope.AppointmentService.Application.Tests;

public class SearchAppointmentsQueryHandlerTests
{
    private readonly Mock<IAppointmentRepository> _mockRepository;
    private readonly Mock<IMapper> _mockMapper;
    private readonly SearchAppointmentsQueryHandler _handler;

    public SearchAppointmentsQueryHandlerTests()
    {
        _mockRepository = new Mock<IAppointmentRepository>();
        _mockMapper = new Mock<IMapper>();
        _handler = new SearchAppointmentsQueryHandler(_mockRepository.Object, _mockMapper.Object);
    }

    [Fact]
    public async Task Handle_WithSearchTerm_ShouldReturnPagedResults()
    {
        var query = new SearchAppointmentsQuery("John", 1, 20);
        var appointments = new List<Appointment>
        {
            Appointment.Schedule(Guid.NewGuid(), Guid.NewGuid(), DateTime.Today.AddDays(7), new TimeSpan(9, 0, 0), 30, AppointmentType.Checkup, null, null),
            Appointment.Schedule(Guid.NewGuid(), Guid.NewGuid(), DateTime.Today.AddDays(8), new TimeSpan(10, 0, 0), 45, AppointmentType.Consultation, null, null),
        };
        var dtos = new List<AppointmentDto> { new(), new() };

        _mockRepository.Setup(r => r.SearchAsync("John", 1, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync((appointments, 2));

        _mockMapper.Setup(m => m.Map<List<AppointmentDto>>(appointments)).Returns(dtos);

        var result = await _handler.Handle(query, CancellationToken.None);

        result.Items.Should().BeSameAs(dtos);
        result.TotalCount.Should().Be(2);
        result.Page.Should().Be(1);
        result.PageSize.Should().Be(20);
    }

    [Fact]
    public async Task Handle_WithNoResults_ShouldReturnEmptyPagedResult()
    {
        var query = new SearchAppointmentsQuery("NonExistent", 1, 20);

        _mockRepository.Setup(r => r.SearchAsync("NonExistent", 1, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<Appointment>(), 0));

        _mockMapper.Setup(m => m.Map<List<AppointmentDto>>(It.IsAny<List<Appointment>>())).Returns(new List<AppointmentDto>());

        var result = await _handler.Handle(query, CancellationToken.None);

        result.Items.Should().BeEmpty();
        result.TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_WithDefaultPaging_ShouldUseDefaults()
    {
        var query = new SearchAppointmentsQuery("test");

        _mockRepository.Setup(r => r.SearchAsync("test", 1, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<Appointment>(), 0));

        _mockMapper.Setup(m => m.Map<List<AppointmentDto>>(It.IsAny<List<Appointment>>())).Returns(new List<AppointmentDto>());

        await _handler.Handle(query, CancellationToken.None);

        _mockRepository.Verify(r => r.SearchAsync("test", 1, 20, It.IsAny<CancellationToken>()), Times.Once);
    }
}
