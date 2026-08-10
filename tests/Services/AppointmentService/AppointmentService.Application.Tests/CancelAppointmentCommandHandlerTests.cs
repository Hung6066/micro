using FluentAssertions;
using His.Hope.AppointmentService.Application.Common.Exceptions;
using His.Hope.AppointmentService.Application.UseCases.Appointments.Commands;
using His.Hope.AppointmentService.Domain.Aggregates;
using His.Hope.AppointmentService.Domain.Repositories;
using His.Hope.AppointmentService.Domain.ValueObjects;
using His.Hope.SharedKernel.Domain.Common;
using Moq;

using NotFoundException = His.Hope.AppointmentService.Application.Common.Exceptions.NotFoundException;

namespace His.Hope.AppointmentService.Application.Tests;

public class CancelAppointmentCommandHandlerTests
{
    private readonly Mock<IAppointmentRepository> _mockRepository;
    private readonly Mock<IUnitOfWork> _mockUnitOfWork;
    private readonly CancelAppointmentCommandHandler _handler;

    public CancelAppointmentCommandHandlerTests()
    {
        _mockRepository = new Mock<IAppointmentRepository>();
        _mockUnitOfWork = new Mock<IUnitOfWork>();
        _mockRepository.Setup(r => r.UnitOfWork).Returns(_mockUnitOfWork.Object);
        _handler = new CancelAppointmentCommandHandler(_mockRepository.Object);
    }

    [Fact]
    public async Task Handle_WithExistingAppointment_ShouldCancelAndSave()
    {
        var appointmentId = Guid.NewGuid();
        var appointment = Appointment.Schedule(
            Guid.NewGuid(), Guid.NewGuid(), DateTime.Today.AddDays(7),
            new TimeSpan(9, 0, 0), 30, AppointmentType.Checkup, "Reason", "Clinic");
        var command = new CancelAppointmentCommand(appointmentId, "Patient cancelled");

        _mockRepository.Setup(r => r.GetByIdAsync(
                It.IsAny<AppointmentId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(appointment);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Should().Be(Unit.Value);
        appointment.Status.Should().Be(AppointmentStatus.Cancelled);
        appointment.CancellationReason.Should().Be("Patient cancelled");
        _mockRepository.Verify(r => r.UpdateAsync(appointment, It.IsAny<CancellationToken>()), Times.Once);
        _mockUnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithNonExistingAppointment_ShouldThrowNotFoundException()
    {
        var command = new CancelAppointmentCommand(Guid.NewGuid(), "Reason");

        _mockRepository.Setup(r => r.GetByIdAsync(
                It.IsAny<AppointmentId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Appointment?)null);

        var act = async () => await _handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
        _mockUnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WithoutReason_ShouldCancelWithNullReason()
    {
        var appointment = Appointment.Schedule(
            Guid.NewGuid(), Guid.NewGuid(), DateTime.Today.AddDays(3),
            new TimeSpan(10, 0, 0), 45, AppointmentType.Consultation, null, null);

        _mockRepository.Setup(r => r.GetByIdAsync(
                It.IsAny<AppointmentId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(appointment);

        await _handler.Handle(new CancelAppointmentCommand(Guid.NewGuid(), null), CancellationToken.None);

        appointment.CancellationReason.Should().BeNull();
        appointment.Status.Should().Be(AppointmentStatus.Cancelled);
    }
}
