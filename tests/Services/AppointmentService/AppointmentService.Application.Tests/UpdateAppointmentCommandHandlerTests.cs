using AutoMapper;
using FluentAssertions;
using His.Hope.AppointmentService.Application.Common.Exceptions;
using His.Hope.AppointmentService.Application.DTOs;
using His.Hope.AppointmentService.Application.UseCases.Appointments.Commands;
using His.Hope.AppointmentService.Domain.Aggregates;
using His.Hope.AppointmentService.Domain.Repositories;
using His.Hope.AppointmentService.Domain.ValueObjects;
using His.Hope.SharedKernel.Domain.Common;
using His.Hope.SharedKernel.Domain.Exceptions;
using Moq;

using NotFoundException = His.Hope.AppointmentService.Application.Common.Exceptions.NotFoundException;

namespace His.Hope.AppointmentService.Application.Tests;

public class UpdateAppointmentCommandHandlerTests
{
    private readonly Mock<IAppointmentRepository> _mockRepository;
    private readonly Mock<IMapper> _mockMapper;
    private readonly Mock<IUnitOfWork> _mockUnitOfWork;
    private readonly UpdateAppointmentCommandHandler _handler;

    public UpdateAppointmentCommandHandlerTests()
    {
        _mockRepository = new Mock<IAppointmentRepository>();
        _mockMapper = new Mock<IMapper>();
        _mockUnitOfWork = new Mock<IUnitOfWork>();
        _mockRepository.Setup(r => r.UnitOfWork).Returns(_mockUnitOfWork.Object);
        _handler = new UpdateAppointmentCommandHandler(_mockRepository.Object, _mockMapper.Object);
    }

    [Fact]
    public async Task Handle_WithExistingAppointment_ShouldRescheduleAndReturnDto()
    {
        var appointmentId = Guid.NewGuid();
        var appointment = Appointment.Schedule(
            Guid.NewGuid(), Guid.NewGuid(), DateTime.Today.AddDays(7),
            new TimeSpan(9, 0, 0), 30, AppointmentType.Checkup, "Original", "Clinic");

        var command = new UpdateAppointmentCommand(
            appointmentId,
            DateTime.Today.AddDays(14),
            new TimeSpan(14, 0, 0),
            45);

        _mockRepository.Setup(r => r.GetByIdAsync(
                It.IsAny<AppointmentId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(appointment);

        var expectedDto = new AppointmentDto { Id = appointmentId };
        _mockMapper.Setup(m => m.Map<AppointmentDto>(appointment)).Returns(expectedDto);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Should().Be(expectedDto);
        appointment.ScheduledDate.Should().Be(DateTime.Today.AddDays(14));
        appointment.StartTime.Should().Be(new TimeSpan(14, 0, 0));
        appointment.Status.Should().Be(AppointmentStatus.Rescheduled);
        _mockRepository.Verify(r => r.UpdateAsync(appointment, It.IsAny<CancellationToken>()), Times.Once);
        _mockUnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithNonExistingAppointment_ShouldThrowNotFoundException()
    {
        var command = new UpdateAppointmentCommand(
            Guid.NewGuid(), DateTime.Today.AddDays(7), new TimeSpan(9, 0, 0), 30);

        _mockRepository.Setup(r => r.GetByIdAsync(
                It.IsAny<AppointmentId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Appointment?)null);

        var act = async () => await _handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
        _mockUnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WithPastDate_ShouldThrowDomainException()
    {
        var appointment = Appointment.Schedule(
            Guid.NewGuid(), Guid.NewGuid(), DateTime.Today.AddDays(7),
            new TimeSpan(9, 0, 0), 30, AppointmentType.Checkup, null, null);

        _mockRepository.Setup(r => r.GetByIdAsync(
                It.IsAny<AppointmentId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(appointment);

        var command = new UpdateAppointmentCommand(
            Guid.NewGuid(), DateTime.Today.AddDays(-1), new TimeSpan(10, 0, 0), 30);

        var act = async () => await _handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<DomainException>()
            .WithMessage("Appointment date must be today or in the future.");
    }
}
