using AutoMapper;
using FluentAssertions;
using His.Hope.AppointmentService.Application.DTOs;
using His.Hope.AppointmentService.Application.UseCases.Appointments.Commands;
using His.Hope.AppointmentService.Domain.Aggregates;
using His.Hope.AppointmentService.Domain.Repositories;
using His.Hope.AppointmentService.Domain.ValueObjects;
using His.Hope.SharedKernel.Domain.Common;
using Moq;

namespace His.Hope.AppointmentService.Application.Tests;

public class CreateAppointmentCommandHandlerTests
{
    private readonly Mock<IAppointmentRepository> _mockRepository;
    private readonly Mock<IMapper> _mockMapper;
    private readonly Mock<IUnitOfWork> _mockUnitOfWork;
    private readonly CreateAppointmentCommandHandler _handler;

    public CreateAppointmentCommandHandlerTests()
    {
        _mockRepository = new Mock<IAppointmentRepository>();
        _mockMapper = new Mock<IMapper>();
        _mockUnitOfWork = new Mock<IUnitOfWork>();
        _mockRepository.Setup(r => r.UnitOfWork).Returns(_mockUnitOfWork.Object);
        _handler = new CreateAppointmentCommandHandler(_mockRepository.Object, _mockMapper.Object);
    }

    [Fact]
    public async Task Handle_WithValidCommand_ShouldCreateAppointmentAndReturnDto()
    {
        var command = new CreateAppointmentCommand(
            PatientId: Guid.NewGuid(),
            ProviderId: Guid.NewGuid(),
            ScheduledDate: DateTime.Today.AddDays(7),
            StartTime: new TimeSpan(9, 0, 0),
            DurationMinutes: 30,
            TypeCode: "CHECKUP",
            Reason: "Annual checkup",
            Location: "Clinic A");

        var expectedDto = new AppointmentDto
        {
            Id = Guid.NewGuid(),
            StatusCode = "SCHEDULED",
            TypeCode = "CHECKUP"
        };

        _mockMapper.Setup(m => m.Map<AppointmentDto>(It.IsAny<Appointment>()))
            .Returns(expectedDto);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Should().NotBeNull();
        result.Should().Be(expectedDto);

        _mockRepository.Verify(r => r.AddAsync(
            It.Is<Appointment>(a =>
                a.PatientId == command.PatientId &&
                a.ProviderId == command.ProviderId &&
                a.Type == AppointmentType.Checkup &&
                a.Status == AppointmentStatus.Scheduled),
            It.IsAny<CancellationToken>()), Times.Once);

        _mockUnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithConsultationType_ShouldCreateConsultationAppointment()
    {
        var command = new CreateAppointmentCommand(
            Guid.NewGuid(), Guid.NewGuid(), DateTime.Today.AddDays(1),
            new TimeSpan(14, 0, 0), 45, "CONSULT", null, null);

        _mockMapper.Setup(m => m.Map<AppointmentDto>(It.IsAny<Appointment>()))
            .Returns(new AppointmentDto());

        await _handler.Handle(command, CancellationToken.None);

        _mockRepository.Verify(r => r.AddAsync(
            It.Is<Appointment>(a => a.Type == AppointmentType.Consultation),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithInvalidTypeCode_ShouldThrow()
    {
        var command = new CreateAppointmentCommand(
            Guid.NewGuid(), Guid.NewGuid(), DateTime.Today.AddDays(1),
            new TimeSpan(9, 0, 0), 30, "INVALID", null, null);

        var act = async () => await _handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
        _mockRepository.Verify(r => r.AddAsync(It.IsAny<Appointment>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WithLocation_ShouldSetLocation()
    {
        var command = new CreateAppointmentCommand(
            Guid.NewGuid(), Guid.NewGuid(), DateTime.Today.AddDays(3),
            new TimeSpan(10, 0, 0), 60, "FOLLOWUP", "Follow-up", "Room 202");

        _mockMapper.Setup(m => m.Map<AppointmentDto>(It.IsAny<Appointment>()))
            .Returns(new AppointmentDto());

        await _handler.Handle(command, CancellationToken.None);

        _mockRepository.Verify(r => r.AddAsync(
            It.Is<Appointment>(a => a.Location == "Room 202" && a.Reason == "Follow-up"),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
