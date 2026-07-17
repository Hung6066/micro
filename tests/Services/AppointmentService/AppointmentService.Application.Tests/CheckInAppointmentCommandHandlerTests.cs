using FluentAssertions;
using His.Hope.AppointmentService.Application.Common.Exceptions;
using His.Hope.AppointmentService.Application.UseCases.Appointments.Commands;
using His.Hope.AppointmentService.Domain.Aggregates;
using His.Hope.AppointmentService.Domain.Repositories;
using His.Hope.AppointmentService.Domain.ValueObjects;
using His.Hope.SharedKernel.Domain.Common;
using His.Hope.SharedKernel.Domain.Exceptions;
using Moq;

using NotFoundException = His.Hope.AppointmentService.Application.Common.Exceptions.NotFoundException;

namespace His.Hope.AppointmentService.Application.Tests;

public class CheckInAppointmentCommandHandlerTests
{
    private readonly Mock<IAppointmentRepository> _mockRepository;
    private readonly Mock<IUnitOfWork> _mockUnitOfWork;
    private readonly CheckInAppointmentCommandHandler _handler;

    public CheckInAppointmentCommandHandlerTests()
    {
        _mockRepository = new Mock<IAppointmentRepository>();
        _mockUnitOfWork = new Mock<IUnitOfWork>();
        _mockRepository.Setup(r => r.UnitOfWork).Returns(_mockUnitOfWork.Object);
        _handler = new CheckInAppointmentCommandHandler(_mockRepository.Object);
    }

    [Fact]
    public async Task Handle_WithTodayAppointment_ShouldCheckInAndSave()
    {
        var appointment = Appointment.Schedule(
            Guid.NewGuid(), Guid.NewGuid(), DateTime.Today,
            new TimeSpan(9, 0, 0), 30, AppointmentType.Checkup, null, "Clinic");

        _mockRepository.Setup(r => r.GetByIdAsync(
                It.IsAny<AppointmentId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(appointment);

        var result = await _handler.Handle(new CheckInAppointmentCommand(Guid.NewGuid()), CancellationToken.None);

        result.Should().Be(Unit.Value);
        appointment.Status.Should().Be(AppointmentStatus.CheckedIn);
        appointment.CheckedInAt.Should().NotBeNull();
        _mockRepository.Verify(r => r.UpdateAsync(appointment, It.IsAny<CancellationToken>()), Times.Once);
        _mockUnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithFutureAppointment_ShouldThrowDomainException()
    {
        var appointment = Appointment.Schedule(
            Guid.NewGuid(), Guid.NewGuid(), DateTime.Today.AddDays(7),
            new TimeSpan(9, 0, 0), 30, AppointmentType.Checkup, null, null);

        _mockRepository.Setup(r => r.GetByIdAsync(
                It.IsAny<AppointmentId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(appointment);

        var act = async () => await _handler.Handle(
            new CheckInAppointmentCommand(Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<DomainException>()
            .WithMessage("Can only check in appointments scheduled for today.");
    }

    [Fact]
    public async Task Handle_WithNonExistingAppointment_ShouldThrowNotFoundException()
    {
        _mockRepository.Setup(r => r.GetByIdAsync(
                It.IsAny<AppointmentId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Appointment?)null);

        var act = async () => await _handler.Handle(
            new CheckInAppointmentCommand(Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }
}
