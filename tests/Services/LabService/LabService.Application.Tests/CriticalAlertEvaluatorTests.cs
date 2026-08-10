using FluentAssertions;
using His.Hope.LabService.Application.Common.Abstractions;
using His.Hope.LabService.Application.Services;
using His.Hope.LabService.Domain.Aggregates;
using His.Hope.LabService.Domain.Entities;
using His.Hope.LabService.Domain.Repositories;
using His.Hope.LabService.Domain.ValueObjects;
using His.Hope.SharedKernel.Domain.Common;
using Moq;

namespace His.Hope.LabService.Application.Tests;

public class CriticalAlertEvaluatorTests
{
    private readonly Mock<ICriticalAlertRuleRepository> _ruleRepository = new();
    private readonly Mock<ICriticalAlertRepository> _alertRepository = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<ICurrentUserContext> _currentUser = new();
    private readonly CriticalAlertEvaluator _evaluator;

    public CriticalAlertEvaluatorTests()
    {
        _alertRepository.Setup(r => r.UnitOfWork).Returns(_unitOfWork.Object);
        _ruleRepository.Setup(r => r.UnitOfWork).Returns(_unitOfWork.Object);
        _currentUser.SetupGet(x => x.IsAuthenticated).Returns(true);
        _currentUser.SetupGet(x => x.UserId).Returns("user-1");
        _currentUser.SetupGet(x => x.FullName).Returns("Dr. Jones");
        _alertRepository.Setup(r => r.AddAndSaveAsync(It.IsAny<CriticalAlert>(), It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((CriticalAlert alert, Guid _, Guid __, CancellationToken ___) => alert);

        _evaluator = new CriticalAlertEvaluator(_ruleRepository.Object, _alertRepository.Object, _currentUser.Object);
    }

    private static (LabOrder order, LabTest test, LabResult result) CreateCriticalResult(AbnormalFlag? flag = null, string value = "18.5", string? unit = "x10^9/L")
    {
        var order = LabOrder.Create(Guid.NewGuid(), Guid.NewGuid(), null, LabOrderPriority.Routine, null);
        var test = LabTest.Create(order.Id, "CBC", "Complete Blood Count", unit);
        order.AddTest(test);
        test.MarkCollected();
        test.MarkInProgress();

        var result = new LabResult(LabResultId.New(), value, unit, "4.0-11.0", flag, LabResultStatus.Final, "Dr. Jones", null);
        test.RecordResult(result);

        return (order, test, result);
    }

    [Fact]
    public async Task EvaluateAsync_WithCriticalFlag_ShouldCreateOpenAlert()
    {
        var (order, test, result) = CreateCriticalResult(AbnormalFlag.CriticalHigh);

        _ruleRepository.Setup(r => r.ListActiveByTestCodeAsync("CBC", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<CriticalAlertRule>());
        _alertRepository.Setup(r => r.GetCurrentAsync(order.Id.Value, test.Id.Value, It.IsAny<CancellationToken>()))
            .ReturnsAsync((CriticalAlert?)null);
        var dto = await _evaluator.EvaluateAsync(order, test, result);

        dto.Should().NotBeNull();
        dto!.Status.Should().Be(CriticalAlertStatus.Open);
        dto.TriggerType.Should().Be(CriticalAlertTriggerType.CriticalFlag);
        dto.AuditEntries.Should().ContainSingle(entry => entry.Action == "Created" && entry.ActorUserId == "user-1");
        _alertRepository.Verify(r => r.AddAndSaveAsync(It.IsAny<CriticalAlert>(), It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task EvaluateAsync_WithThresholdMatch_ShouldCreateOpenAlert()
    {
        var (order, test, result) = CreateCriticalResult(null, "12.5", "x10^9/L");
        var rule = CriticalAlertRule.Create("CBC", "Complete Blood Count", "x10^9/L", null, 10.0m, "system", "System");

        _ruleRepository.Setup(r => r.ListActiveByTestCodeAsync("CBC", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { rule });
        _alertRepository.Setup(r => r.GetCurrentAsync(order.Id.Value, test.Id.Value, It.IsAny<CancellationToken>()))
            .ReturnsAsync((CriticalAlert?)null);
        var dto = await _evaluator.EvaluateAsync(order, test, result);

        dto.Should().NotBeNull();
        dto!.Status.Should().Be(CriticalAlertStatus.Open);
        dto.TriggerType.Should().Be(CriticalAlertTriggerType.Threshold);
        dto.RuleId.Should().Be(rule.Id);
        dto.ThresholdValue.Should().Be(10.0m);
    }

    [Fact]
    public async Task EvaluateAsync_WithExistingCriticalAlert_ShouldUpdateInsteadOfCreatingDuplicate()
    {
        var (order, test, result) = CreateCriticalResult(AbnormalFlag.CriticalHigh);
        var existingAlert = CriticalAlert.Create(
            order.Id.Value,
            test.Id.Value,
            Guid.NewGuid(),
            null,
            CriticalAlertTriggerType.CriticalFlag,
            "Critical flag CRITICAL_HIGH",
            "18.5",
            "x10^9/L",
            null,
            "user-1",
            "Dr. Jones");

        _ruleRepository.Setup(r => r.ListActiveByTestCodeAsync("CBC", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<CriticalAlertRule>());
        _alertRepository.Setup(r => r.GetCurrentAsync(order.Id.Value, test.Id.Value, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingAlert);

        var dto = await _evaluator.EvaluateAsync(order, test, result);

        dto.Should().NotBeNull();
        dto!.Id.Should().Be(existingAlert.Id);
        dto.LabResultId.Should().Be(result.LabResultId.Value);
        dto.AuditEntries.Should().HaveCount(2);
        dto.AuditEntries.Last().Action.Should().Be("Updated");
        _alertRepository.Verify(r => r.AddAsync(It.IsAny<CriticalAlert>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task EvaluateAsync_WithNoncriticalCorrection_ShouldResolveExistingAlert()
    {
        var order = LabOrder.Create(Guid.NewGuid(), Guid.NewGuid(), null, LabOrderPriority.Routine, null);
        var test = LabTest.Create(order.Id, "CBC", "Complete Blood Count", "x10^9/L");
        order.AddTest(test);
        test.MarkCollected();
        test.MarkInProgress();

        var result = new LabResult(LabResultId.New(), "5.5", "x10^9/L", "4.0-11.0", AbnormalFlag.Normal, LabResultStatus.Final, "Dr. Jones", null);
        test.RecordResult(result);

        var existingAlert = CriticalAlert.Create(
            order.Id.Value,
            test.Id.Value,
            Guid.NewGuid(),
            null,
            CriticalAlertTriggerType.CriticalFlag,
            "Critical flag CRITICAL_HIGH",
            "18.5",
            "x10^9/L",
            null,
            "user-1",
            "Dr. Jones");

        _ruleRepository.Setup(r => r.ListActiveByTestCodeAsync("CBC", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<CriticalAlertRule>());
        _alertRepository.Setup(r => r.GetCurrentAsync(order.Id.Value, test.Id.Value, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingAlert);

        var dto = await _evaluator.EvaluateAsync(order, test, result);

        dto.Should().NotBeNull();
        dto!.Status.Should().Be(CriticalAlertStatus.Resolved);
        dto.AuditEntries.Should().HaveCount(2);
        dto.AuditEntries.Last().Action.Should().Be("Resolved");
        dto.ResolvedByUserId.Should().Be("user-1");
    }

    [Fact]
    public async Task ResolveAsync_WithNoCurrentAlert_ShouldReturnNull()
    {
        var (order, test, result) = CreateCriticalResult();

        _alertRepository.Setup(r => r.GetCurrentAsync(order.Id.Value, test.Id.Value, It.IsAny<CancellationToken>()))
            .ReturnsAsync((CriticalAlert?)null);

        var dto = await _evaluator.ResolveAsync(order, test, result);

        dto.Should().BeNull();
    }
}
