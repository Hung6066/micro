using AutoMapper;
using FluentAssertions;
using His.Hope.AppointmentService.Application.DTOs;
using His.Hope.AppointmentService.Application.UseCases.Appointments.Queries;
using His.Hope.AppointmentService.Domain.Aggregates;
using His.Hope.AppointmentService.Domain.Repositories;
using His.Hope.AppointmentService.Domain.ValueObjects;
using Moq;

namespace His.Hope.AppointmentService.Application.Tests;

public class GetAppointmentsByPatientQueryHandlerTests
{
    private readonly Mock<IAppointmentRepository> _mockRepository;
    private readonly Mock<IMapper> _mockMapper;
    private readonly GetAppointmentsByPatientQueryHandler _handler;

    public GetAppointmentsByPatientQueryHandlerTests()
    {
        _mockRepository = new Mock<IAppointmentRepository>();
        _mockMapper = new Mock<IMapper>();
        _handler = new GetAppointmentsByPatientQueryHandler(_mockRepository.Object, _mockMapper.Object);
    }

    [Fact]
    public async Task Handle_WithPatientId_ShouldReturnPagedResults()
    {
        var patientId = Guid.NewGuid();
        var query = new GetAppointmentsByPatientQuery(patientId, 1, 20, null, null);
        var appointments = new List<Appointment>
        {
            Appointment.Schedule(patientId, Guid.NewGuid(), DateTime.Today.AddDays(7), new TimeSpan(9, 0, 0), 30, AppointmentType.Checkup, null, null),
        };
        var dtos = new List<AppointmentDto> { new() };

        _mockRepository.Setup(r => r.GetByPatientIdAsync(patientId, 1, 20, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync((appointments, 1));

        _mockMapper.Setup(m => m.Map<List<AppointmentDto>>(appointments)).Returns(dtos);

        var result = await _handler.Handle(query, CancellationToken.None);

        result.Items.Should().BeSameAs(dtos);
        result.TotalCount.Should().Be(1);
        result.Page.Should().Be(1);
    }

    [Fact]
    public async Task Handle_WithDateRange_ShouldPassDates()
    {
        var patientId = Guid.NewGuid();
        var fromDate = DateTime.Today;
        var toDate = DateTime.Today.AddDays(30);
        var query = new GetAppointmentsByPatientQuery(patientId, 1, 20, fromDate, toDate);

        _mockRepository.Setup(r => r.GetByPatientIdAsync(patientId, 1, 20, fromDate, toDate, It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<Appointment>(), 0));

        _mockMapper.Setup(m => m.Map<List<AppointmentDto>>(It.IsAny<List<Appointment>>())).Returns(new List<AppointmentDto>());

        await _handler.Handle(query, CancellationToken.None);

        _mockRepository.Verify(r => r.GetByPatientIdAsync(patientId, 1, 20, fromDate, toDate, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithNoResults_ShouldReturnEmpty()
    {
        var query = new GetAppointmentsByPatientQuery(Guid.NewGuid(), 1, 20, null, null);

        _mockRepository.Setup(r => r.GetByPatientIdAsync(It.IsAny<Guid>(), 1, 20, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<Appointment>(), 0));

        _mockMapper.Setup(m => m.Map<List<AppointmentDto>>(It.IsAny<List<Appointment>>())).Returns(new List<AppointmentDto>());

        var result = await _handler.Handle(query, CancellationToken.None);

        result.Items.Should().BeEmpty();
        result.TotalCount.Should().Be(0);
    }
}
