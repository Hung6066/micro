using FluentAssertions;
using His.Hope.LabService.Application.UseCases.LabOrders.Commands;

namespace His.Hope.LabService.Application.Tests;

public class ValidatorTests
{
    public class CreateLabOrderCommandValidatorTests
    {
        private readonly CreateLabOrderCommandValidator _validator = new();

        [Fact]
        public void Validate_WithValidCommand_ShouldNotHaveErrors()
        {
            var command = new CreateLabOrderCommand(
                Guid.NewGuid(),
                Guid.NewGuid(),
                null,
                "ROUTINE",
                "Notes",
                new List<TestItem> { new("CBC", "Complete Blood Count", "Blood") });

            var result = _validator.Validate(command);

            result.IsValid.Should().BeTrue();
        }

        [Fact]
        public void Validate_WithEmptyPatientId_ShouldHaveError()
        {
            var command = new CreateLabOrderCommand(
                Guid.Empty,
                Guid.NewGuid(),
                null,
                "ROUTINE",
                null,
                new List<TestItem> { new("CBC", "Complete Blood Count", null) });

            var result = _validator.Validate(command);

            result.IsValid.Should().BeFalse();
            result.Errors.Should().Contain(e => e.PropertyName == "PatientId");
        }

        [Fact]
        public void Validate_WithEmptyProviderId_ShouldHaveError()
        {
            var command = new CreateLabOrderCommand(
                Guid.NewGuid(),
                Guid.Empty,
                null,
                "ROUTINE",
                null,
                new List<TestItem> { new("CBC", "Complete Blood Count", null) });

            var result = _validator.Validate(command);

            result.IsValid.Should().BeFalse();
            result.Errors.Should().Contain(e => e.PropertyName == "ProviderId");
        }

        [Fact]
        public void Validate_WithEmptyPriorityCode_ShouldHaveError()
        {
            var command = new CreateLabOrderCommand(
                Guid.NewGuid(),
                Guid.NewGuid(),
                null,
                "",
                null,
                new List<TestItem> { new("CBC", "Complete Blood Count", null) });

            var result = _validator.Validate(command);

            result.IsValid.Should().BeFalse();
            result.Errors.Should().Contain(e => e.PropertyName == "PriorityCode");
        }

        [Fact]
        public void Validate_WithNoTests_ShouldHaveError()
        {
            var command = new CreateLabOrderCommand(
                Guid.NewGuid(),
                Guid.NewGuid(),
                null,
                "ROUTINE",
                null,
                new List<TestItem>());

            var result = _validator.Validate(command);

            result.IsValid.Should().BeFalse();
            result.Errors.Should().Contain(e => e.PropertyName == "Tests");
        }

        [Fact]
        public void Validate_WithEmptyTestCode_ShouldHaveError()
        {
            var command = new CreateLabOrderCommand(
                Guid.NewGuid(),
                Guid.NewGuid(),
                null,
                "ROUTINE",
                null,
                new List<TestItem> { new("", "Complete Blood Count", null) });

            var result = _validator.Validate(command);

            result.IsValid.Should().BeFalse();
            result.Errors.Should().Contain(e => e.PropertyName.Contains("TestCode"));
        }

        [Fact]
        public void Validate_WithEmptyTestName_ShouldHaveError()
        {
            var command = new CreateLabOrderCommand(
                Guid.NewGuid(),
                Guid.NewGuid(),
                null,
                "ROUTINE",
                null,
                new List<TestItem> { new("CBC", "", null) });

            var result = _validator.Validate(command);

            result.IsValid.Should().BeFalse();
            result.Errors.Should().Contain(e => e.PropertyName.Contains("TestName"));
        }

        [Fact]
        public void Validate_WithTestCodeTooLong_ShouldHaveError()
        {
            var command = new CreateLabOrderCommand(
                Guid.NewGuid(),
                Guid.NewGuid(),
                null,
                "ROUTINE",
                null,
                new List<TestItem> { new(new string('A', 21), "Test Name", null) });

            var result = _validator.Validate(command);

            result.IsValid.Should().BeFalse();
            result.Errors.Should().Contain(e => e.PropertyName.Contains("TestCode"));
        }

        [Fact]
        public void Validate_WithTestNameTooLong_ShouldHaveError()
        {
            var command = new CreateLabOrderCommand(
                Guid.NewGuid(),
                Guid.NewGuid(),
                null,
                "ROUTINE",
                null,
                new List<TestItem> { new("CBC", new string('A', 201), null) });

            var result = _validator.Validate(command);

            result.IsValid.Should().BeFalse();
            result.Errors.Should().Contain(e => e.PropertyName.Contains("TestName"));
        }

        [Fact]
        public void Validate_WithNotesTooLong_ShouldHaveError()
        {
            var command = new CreateLabOrderCommand(
                Guid.NewGuid(),
                Guid.NewGuid(),
                null,
                "ROUTINE",
                new string('A', 1001),
                new List<TestItem> { new("CBC", "Complete Blood Count", null) });

            var result = _validator.Validate(command);

            result.IsValid.Should().BeFalse();
            result.Errors.Should().Contain(e => e.PropertyName == "Notes");
        }
    }

    public class AddLabTestCommandValidatorTests
    {
        private readonly AddLabTestCommandValidator _validator = new();

        [Fact]
        public void Validate_WithValidCommand_ShouldNotHaveErrors()
        {
            var command = new AddLabTestCommand(Guid.NewGuid(), "CBC", "Complete Blood Count", "Blood");

            var result = _validator.Validate(command);

            result.IsValid.Should().BeTrue();
        }

        [Fact]
        public void Validate_WithEmptyOrderId_ShouldHaveError()
        {
            var command = new AddLabTestCommand(Guid.Empty, "CBC", "Complete Blood Count", null);

            var result = _validator.Validate(command);

            result.IsValid.Should().BeFalse();
            result.Errors.Should().Contain(e => e.PropertyName == "OrderId");
        }

        [Fact]
        public void Validate_WithEmptyTestCode_ShouldHaveError()
        {
            var command = new AddLabTestCommand(Guid.NewGuid(), "", "Complete Blood Count", null);

            var result = _validator.Validate(command);

            result.IsValid.Should().BeFalse();
            result.Errors.Should().Contain(e => e.PropertyName == "TestCode");
        }

        [Fact]
        public void Validate_WithEmptyTestName_ShouldHaveError()
        {
            var command = new AddLabTestCommand(Guid.NewGuid(), "CBC", "", null);

            var result = _validator.Validate(command);

            result.IsValid.Should().BeFalse();
            result.Errors.Should().Contain(e => e.PropertyName == "TestName");
        }

        [Fact]
        public void Validate_WithTestCodeTooLong_ShouldHaveError()
        {
            var command = new AddLabTestCommand(Guid.NewGuid(), new string('A', 21), "Test", null);

            var result = _validator.Validate(command);

            result.IsValid.Should().BeFalse();
            result.Errors.Should().Contain(e => e.PropertyName == "TestCode");
        }

        [Fact]
        public void Validate_WithTestNameTooLong_ShouldHaveError()
        {
            var command = new AddLabTestCommand(Guid.NewGuid(), "CBC", new string('A', 201), null);

            var result = _validator.Validate(command);

            result.IsValid.Should().BeFalse();
            result.Errors.Should().Contain(e => e.PropertyName == "TestName");
        }
    }

    public class RecordLabResultCommandValidatorTests
    {
        private readonly RecordLabResultCommandValidator _validator = new();

        [Fact]
        public void Validate_WithValidCommand_ShouldNotHaveErrors()
        {
            var command = new RecordLabResultCommand(
                Guid.NewGuid(), Guid.NewGuid(), "5.5", "x10^9/L",
                "4.0-11.0", "NORMAL", "FINAL", "Dr. Smith", "Notes");

            var result = _validator.Validate(command);

            result.IsValid.Should().BeTrue();
        }

        [Fact]
        public void Validate_WithEmptyOrderId_ShouldHaveError()
        {
            var command = new RecordLabResultCommand(
                Guid.Empty, Guid.NewGuid(), "5.5", null, null, null, "FINAL", null, null);

            var result = _validator.Validate(command);

            result.IsValid.Should().BeFalse();
            result.Errors.Should().Contain(e => e.PropertyName == "OrderId");
        }

        [Fact]
        public void Validate_WithEmptyTestId_ShouldHaveError()
        {
            var command = new RecordLabResultCommand(
                Guid.NewGuid(), Guid.Empty, "5.5", null, null, null, "FINAL", null, null);

            var result = _validator.Validate(command);

            result.IsValid.Should().BeFalse();
            result.Errors.Should().Contain(e => e.PropertyName == "TestId");
        }

        [Fact]
        public void Validate_WithEmptyValue_ShouldHaveError()
        {
            var command = new RecordLabResultCommand(
                Guid.NewGuid(), Guid.NewGuid(), "", null, null, null, "FINAL", null, null);

            var result = _validator.Validate(command);

            result.IsValid.Should().BeFalse();
            result.Errors.Should().Contain(e => e.PropertyName == "Value");
        }

        [Fact]
        public void Validate_WithValueTooLong_ShouldHaveError()
        {
            var command = new RecordLabResultCommand(
                Guid.NewGuid(), Guid.NewGuid(), new string('A', 501), null, null, null, "FINAL", null, null);

            var result = _validator.Validate(command);

            result.IsValid.Should().BeFalse();
            result.Errors.Should().Contain(e => e.PropertyName == "Value");
        }

        [Fact]
        public void Validate_WithEmptyResultStatusCode_ShouldHaveError()
        {
            var command = new RecordLabResultCommand(
                Guid.NewGuid(), Guid.NewGuid(), "5.5", null, null, null, "", null, null);

            var result = _validator.Validate(command);

            result.IsValid.Should().BeFalse();
            result.Errors.Should().Contain(e => e.PropertyName == "ResultStatusCode");
        }

        [Fact]
        public void Validate_WithOptionalFields_ShouldSucceed()
        {
            var command = new RecordLabResultCommand(
                Guid.NewGuid(), Guid.NewGuid(), "5.5", null, null, null, "FINAL", null, null);

            var result = _validator.Validate(command);

            result.IsValid.Should().BeTrue();
        }
    }
}
