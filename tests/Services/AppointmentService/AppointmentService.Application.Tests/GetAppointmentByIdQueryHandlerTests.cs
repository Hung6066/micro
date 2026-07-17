using AutoMapper;
using FluentAssertions;
using His.Hope.AppointmentService.Application.DTOs;
using His.Hope.AppointmentService.Application.UseCases.Appointments.Queries;
using His.Hope.AppointmentService.Domain.Aggregates;
using His.Hope.AppointmentService.Domain.Repositories;
using His.Hope.AppointmentService.Domain.ValueObjects;
using Moq;

namespace His.Hope.AppointmentService.Application.Tests;

public class GetAppointmentByIdQueryHandlerTests
{
    private readonly Mock<IAppointmentRepository> _mockRepository;
    private readonly Mock<IMapper> _mockMapper;
    private readonly GetAppointmentByIdQueryHandler _handler;

    public GetAppointmentByIdQueryHandlerTests()
    {
        _mockRepository = new Mock<IAppointmentRepository>();
        _mockMapper = new Mock<IMapper>();
        _handler = new GetAppointmentByIdQueryHandler(_mockRepository.Object, _mockMapper.Object);
    }

    [Fact]
    public async Task Handle_WithExistingAppointment_ShouldReturnMappedDto()
    {
        var appointmentId = Guid.NewGuid();
        var query = new GetAppointmentByIdQuery(appointmentId);
        var appointment = Appointment.Schedule(
            Guid.NewGuid(), Guid.NewGuid(), DateTime.Today.AddDays(7),
            new TimeSpan(9, 0, 0), 30, AppointmentType.Checkup, "Reason", "Clinic");

        var expectedDto = new AppointmentDto { Id = appointmentId, StatusCode = "SCHEDULED" };

        _mockRepository.Setup(r => r.GetByIdAsync(
                It.Is<AppointmentId>(id => id.Value == appointmentId),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(appointment);

        _mockMapper.Setup(m => m.Map<AppointmentDto>(appointment)).Returns(expectedDto);

        var result = await _handler.Handle(query, CancellationToken.None);

        result.Should().NotBeNull();
        result.Should().Be(expectedDto);
    }

    [Fact]
    public async Task Handle_WithNonExistingAppointment_ShouldReturnNull()
    {
        var query = new GetAppointmentByIdQuery(Guid.NewGuid());

        _mockRepository.Setup(r => r.GetByIdAsync(
                It.IsAny<AppointmentId>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((Appointment?)null);

        var result = await _handler.Handle(query, CancellationToken.None);

        result.Should().BeNull();
        _mockMapper.Verify(m => m.Map<AppointmentDto>(It.IsAny<Appointment>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldQueryByIdWithCorrectId()
    {
        var appointmentId = Guid.NewGuid();
        var query = new GetAppointmentByIdQuery(appointmentId);

        _mockRepository.Setup(r => r.GetByIdAsync(
                It.IsAny<AppointmentId>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((Appointment?)null);

        await _handler.Handle(query, CancellationToken.None);

        _mockRepository.Verify(r => r.GetByIdAsync(
            It.Is<AppointmentId>(id => id.Value == appointmentId),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
